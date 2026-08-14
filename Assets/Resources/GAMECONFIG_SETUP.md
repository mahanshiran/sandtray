# GameConfig Setup

## 🔑 API Keys Required

This project requires API keys for third-party services. These keys are stored in a `GameConfig` ScriptableObject asset that you must create locally.

**⚠️ IMPORTANT: Never commit `GameConfig.asset` to Git!** It's already in `.gitignore` but double-check before committing.

---

## Setup Instructions

### 1. Create GameConfig Asset

1. In Unity, right-click in the Project window (`Assets/Resources/` folder)
2. Select **Create → Sandplay → Game Config**
3. Name it `GameConfig` (it will create `GameConfig.asset`)

### 2. Configure API Keys

Select the `GameConfig` asset in the Inspector and fill in the following fields:

#### **Bailian AI Analysis** (optional)
- **BailianApiKey**: Get from [Aliyun Bailian Console](https://bailian.console.aliyun.com/)
- Format: `sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Leave blank to disable AI analysis features

#### **RevenueCat Subscriptions** (optional for development)
- **RevenueCatAppleApiKey**: Get from [RevenueCat Dashboard](https://app.revenuecat.com/) → Settings → API Keys
- **RevenueCatGoogleApiKey**: Same as above
- Format: `appl_XXXXXXXXXXXXXXXXXXXXXXXXXXXXX` / `goog_XXXXXXXXXXXXXXXXXXXXXXXXXXXXX`
- Leave blank to disable subscription/paywall features

#### **Agora Voice/Video** (optional)
- **AgoraAppId**: Get from [Agora Console](https://console.agora.io/)
- Format: 32-character hexadecimal string
- Leave blank to disable voice/video features

### 3. Assign to SceneBootstrapper

1. Open the main scene (e.g., `Assets/Scenes/Sandbox.unity`)
2. Find the `SceneBootstrapper` GameObject in the Hierarchy
3. In the Inspector, drag `GameConfig.asset` to the **Config** field

---

## Security Checklist

Before every commit:

- [ ] `GameConfig.asset` is NOT staged (`git status` should be clean)
- [ ] No API keys appear in `.cs` files (`git diff` clean)
- [ ] Shared any keys with team via secure channels (1Password, not Slack)

**Read the full security guide:** [`API_KEY_SECURITY.md`](/API_KEY_SECURITY.md)

---

## Troubleshooting

**"Bailian API key not configured — AI analysis disabled"**
→ This is normal if you haven't set up Bailian yet. Feature gracefully degrades.

**"GameConfig not found" error**
→ You forgot step 3 (assign to SceneBootstrapper Inspector field).

**Keys not working after updating**
→ Unity caches ScriptableObject changes. File → Save Project, then restart Unity.

---

## Template Config (for new team members)

If you're new to the project:

1. Ask a team member to share their **local** `GameConfig.asset` file (via secure channel, not Git)
2. Or create a fresh one and obtain your own API keys from the dashboards above
3. For development, you can leave all keys blank — the app will run with limited features

**Do not** ask for keys via Slack, email, or public channels.
