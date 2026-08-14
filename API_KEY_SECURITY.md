# API Key Security Guide

## ⚠️ CRITICAL: Never Commit Real API Keys to Source Control

This project requires several third-party API keys:
- **Bailian AI Analysis**: `BailianApiKey` (format: `sk-...`)
- **RevenueCat Subscriptions**: `RevenueCatAppleApiKey` / `RevenueCatGoogleApiKey` (format: `appl_...` / `goog_...`)
- **Agora Voice/Video**: `AgoraAppId`

**These keys must NEVER be committed to Git.** Exposed keys can be scraped by bots and abused within hours.

---

## Local Development Setup

### 1. Create a Local GameConfig Asset

The keys are stored in a `GameConfig` ScriptableObject asset. Unity stores ScriptableObject data in `.asset` files (binary/YAML).

**Steps:**
1. In Unity, create a `GameConfig` asset:
   - Right-click in `Assets/Resources/` → Create → Sandplay → Game Config
   - Name it `GameConfig.asset`
2. Select the asset in the Inspector
3. Fill in your API keys (obtain from respective dashboards):
   - **Bailian**: [Aliyun Bailian Console](https://bailian.console.aliyun.com/)
   - **RevenueCat**: [RevenueCat Dashboard](https://app.revenuecat.com/)
   - **Agora**: [Agora Console](https://console.agora.io/)
4. **DO NOT** commit this asset to Git (see `.gitignore` section below)

---

## Protecting Your Keys

### 2. Add GameConfig.asset to .gitignore

Ensure your `.gitignore` includes:

```gitignore
# API Keys - DO NOT COMMIT
Assets/Resources/GameConfig.asset
Assets/Resources/GameConfig.asset.meta

# Alternative: Ignore all Resources configs
Assets/Resources/*Config.asset
Assets/Resources/*Config.asset.meta
```

**Verify exclusion:**
```bash
git status  # GameConfig.asset should NOT appear
git check-ignore -v Assets/Resources/GameConfig.asset  # Should show .gitignore match
```

### 3. Create a Template Config

Commit a **template** config with empty keys as documentation:

```bash
# Create template
cp Assets/Resources/GameConfig.asset Assets/Resources/GameConfig.template.asset

# Manually clear all API keys in the template to empty strings ""
# Then commit the template:
git add Assets/Resources/GameConfig.template.asset
git commit -m "Add GameConfig template (no secrets)"
```

Team members can copy `GameConfig.template.asset` → `GameConfig.asset` and fill in their own keys.

---

## CI/CD & Build Automation

### 4. Environment Variables for Unity Cloud Build

For automated builds (CI/CD), inject keys at build time via environment variables:

#### A. Unity Cloud Build (CloudBuild)

1. In Unity Dashboard → Cloud Build → Config Variables, set:
   - `BAILIAN_API_KEY` = `sk-...`
   - `REVENUECAT_APPLE_KEY` = `appl_...`
   - `AGORA_APP_ID` = `...`

2. Create a pre-build script (`Assets/Editor/CloudBuildKeyInjector.cs`):

```csharp
#if UNITY_CLOUD_BUILD
using UnityEditor;
using UnityEngine;
using Sandplay.Core;

public static class CloudBuildKeyInjector
{
    public static void InjectKeys()
    {
        var config = Resources.Load<GameConfig>("GameConfig");
        if (config == null)
        {
            Debug.LogError("GameConfig not found! Create a template in Resources/");
            return;
        }

        config.BailianApiKey = System.Environment.GetEnvironmentVariable("BAILIAN_API_KEY") ?? "";
        config.RevenueCatAppleApiKey = System.Environment.GetEnvironmentVariable("REVENUECAT_APPLE_KEY") ?? "";
        config.AgoraAppId = System.Environment.GetEnvironmentVariable("AGORA_APP_ID") ?? "";

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[CloudBuild] API keys injected from environment variables");
    }
}
#endif
```

3. Configure Cloud Build to run `CloudBuildKeyInjector.InjectKeys()` as a pre-build step.

#### B. GitHub Actions / Jenkins

Use secret management:

```yaml
# .github/workflows/build.yml
name: Build Unity Project
on: [push]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Inject API Keys
        run: |
          cat > Assets/Resources/GameConfig.asset << EOF
          %YAML 1.1
          %TAG !u! tag:unity3d.com,2011:
          --- !u!114 &11400000
          MonoBehaviour:
            m_Script: {fileID: 11500000, guid: YOUR_GAMECONFIG_GUID}
            BailianApiKey: ${{ secrets.BAILIAN_API_KEY }}
            RevenueCatAppleApiKey: ${{ secrets.REVENUECAT_APPLE_KEY }}
            AgoraAppId: ${{ secrets.AGORA_APP_ID }}
          EOF
      - name: Build Unity
        uses: game-ci/unity-builder@v2
```

Store keys in GitHub Settings → Secrets → Actions.

---

## Key Rotation & Revocation

### If Keys Are Accidentally Committed

**Act immediately:**

1. **Revoke the exposed keys** in the respective dashboards:
   - Bailian: Regenerate API key
   - RevenueCat: Rotate API keys (Dashboard → Settings → API Keys)
   - Agora: Disable project or regenerate credentials
   

2. **Remove from Git history** (requires force-push):

```bash
# Using BFG Repo-Cleaner (faster than git filter-branch)
brew install bfg  # macOS
bfg --replace-text <(echo 'sk-9bd706bdd1634cc99f72f2834301a96a==>REMOVED') --no-blob-protection
git reflog expire --expire=now --all
git gc --prune=now --aggressive
git push --force
```

3. **Scan for exposure**:
   - Check [GitHub's secret scanning alerts](https://docs.github.com/en/code-security/secret-scanning)
   - Search public mirrors (GitLab, Bitbucket forks)

4. **Update all local clones** (team members must re-clone or `git pull --force`)

---

## Verification Checklist

Before every commit:

- [ ] `git diff` does not contain `sk-`, `appl_`, `goog_` strings
- [ ] `GameConfig.asset` is not staged (`git status` clean)
- [ ] Template config has empty strings for all keys
- [ ] Pre-commit hook runs (optional, see below)

### Optional: Pre-Commit Hook

Create `.git/hooks/pre-commit`:

```bash
#!/bin/bash
# Prevent committing real API keys
if git diff --cached --name-only | grep -q "GameConfig.asset"; then
    echo "❌ ERROR: GameConfig.asset is staged for commit!"
    echo "   This file contains API keys and must not be committed."
    echo "   Add it to .gitignore."
    exit 1
fi

if git diff --cached | grep -qE 'sk-[a-z0-9]{32}|appl_[A-Za-z0-9]{32}'; then
    echo "❌ ERROR: Detected API key pattern in staged changes!"
    echo "   Review your commit for hard-coded secrets."
    exit 1
fi
exit 0
```

Make executable: `chmod +x .git/hooks/pre-commit`

---

## Best Practices Summary

✅ **DO:**
- Store keys in `GameConfig.asset` (not in `.gitignore`)
- Use environment variables for CI/CD
- Rotate keys regularly (every 90 days)
- Use separate keys for dev/staging/production
- Commit a template config with empty keys

❌ **DON'T:**
- Hard-code keys in `.cs` files
- Share keys via Slack/email (use 1Password, LastPass, AWS Secrets Manager)
- Use production keys for local development
- Commit `.asset` files with real keys

---

## Current Status

✅ **Fixed (as of this commit):**
- Moved Bailian key from hard-coded string to `GameConfig.BailianApiKey`
- Cleared default values for `AgoraAppId`, `RevenueCatAppleApiKey`, `BailianApiKey` in code
- Added safety check in `CreateBailianClient()` (gracefully disables if key missing)

⚠️ **Action Required:**
1. Add `Assets/Resources/GameConfig.asset` to `.gitignore`
2. Revoke the exposed keys listed at the top of this document
3. Generate new keys from respective dashboards
4. Update your local `GameConfig.asset` with new keys
5. Configure CI/CD build systems with environment variables

---

## References

- [Unity Manual: ScriptableObject](https://docs.unity3d.com/Manual/class-ScriptableObject.html)
- [OWASP: Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)
- [GitHub: Removing Sensitive Data](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository)
- [Unity Cloud Build: Environment Variables](https://docs.unity.com/ugs/en-us/manual/cloud-build/manual/configure/environment-variables)
