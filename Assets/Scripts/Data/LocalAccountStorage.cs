using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Data
{
    /// <summary>A process-pinned account scope. Identity changes latch it closed until restart.</summary>
    public sealed class LocalStorageScope
    {
        private readonly Func<int> identity;
        private readonly int owner;
        private bool invalidated;
        public string Directory { get; }
        public LocalStorageScope(string directory, int user, Func<int> currentIdentity)
        { Directory = directory; owner = user; identity = currentIdentity; }
        public bool IsCurrent
        {
            get { if (identity() != owner) invalidated = true; return !invalidated; }
        }
        public void Invalidate() => invalidated = true;
        public void RequireCurrent()
        { if (!IsCurrent) throw new UnauthorizedAccessException("Account changed. Reopen Sandtray to access this account's records."); }
    }

    public static class LocalAccountStorage
    {
        [Serializable] public sealed class Activation { public int Schema = 1; public string Authority; public int Owner; }
        private static LocalStorageScope scope;
        private static bool enabled;
        private static bool forcedRestart;
        public static int Epoch { get; private set; }
        private static bool transitioning;
        public static Action CaptureGuard()
        {
            RequireCurrent(); var pinned = scope; int epoch = Epoch;
            return () => { pinned.RequireCurrent(); if (epoch != Epoch || RequiresRestart) throw new UnauthorizedAccessException("Local account changed."); };
        }
        public static void CompleteWorkspaceTransition()
        {
            scope?.Invalidate(); Epoch++; scope = null; forcedRestart = false; transitioning = false;
            Initialize();
            if (activationStatus.NeedsRecovery) throw new InvalidDataException("Ownership settings need recovery.");
            shield = null;
        }
        private static LocalOwnershipActivation.Status activationStatus;
        public static string Marker(string root) => Path.Combine(root, "LocalOwnershipV1", "active.json");
        private static int User => BackendClient.Instance.IsLoggedIn ? BackendClient.Instance.UserId : 0;
        public static string AccountPath(string root, string authority, int user)
        {
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(authority.TrimEnd('/')))).Replace("-", "").ToLowerInvariant();
            return Path.Combine(root, "LocalOwnershipV1", "accounts", hash,
                user > 0 ? user.ToString(System.Globalization.CultureInfo.InvariantCulture) : "guest");
        }
        private static void Initialize()
        {
            if (scope != null) return;
            string root = Application.persistentDataPath;
            activationStatus = LocalOwnershipActivation.Inspect(root);
            enabled = activationStatus.Enabled;
            int user = User;
            scope = new LocalStorageScope(enabled ? AccountPath(root, BackendClient.BaseUrl, user) : root, user, () => User);
        }
        public static bool RequiresRestart
        {
            // Even the legacy shared directory has account-pinned consumers and callbacks.
            // Changing identity must rebuild those consumers, regardless of storage layout.
            get { Initialize(); return forcedRestart || activationStatus.NeedsRecovery || !scope.IsCurrent; }
        }
        public static bool SaveBeforeIdentityChange()
        {
            if (scope == null || !scope.IsCurrent) return true;
            if (NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline && !NetworkBootstrapper.Instance.IsHost) return true;
            var manager = SessionManager.Instance;
            if (manager == null || !manager.HasOpenBoard) return true;
            return manager.BoardReady && manager.TrySaveCurrentBoard();
        }
        public static void ObserveIdentity()
        {
            if (scope != null && !scope.IsCurrent) ShowRestartShield();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { scope = null; Epoch = 0; transitioning = false; activationStatus = null; enabled = forcedRestart = false; shield = null; }
        private static GameObject shield;
        public static void ShowRestartShield()
        {
            SessionManager.Instance?.EndAutoSave();
            SessionRecorder.Instance?.StopRecording();
            if (shield != null) return;
            if (activationStatus != null && !activationStatus.NeedsRecovery && !transitioning)
            { scope?.Invalidate(); Epoch++; transitioning = true; }
            shield = new GameObject("LocalAccountRestart", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(shield);
            var canvas = shield.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
            var panel = new GameObject("PrivacyCover", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(shield.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.08f,.12f,.16f,1);
            var message = new GameObject("Message", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            message.transform.SetParent(panel.transform, false);
            var label = message.GetComponent<TMPro.TextMeshProUGUI>();
            label.text = Localization.Current == Language.Chinese
                ? "请关闭并重新打开 Sandtray，以安全切换本地账号。已保存的记录不会丢失。"
                : "Close and reopen Sandtray to finish switching local accounts safely. Saved records are preserved.";
            label.fontSize = 24; label.alignment = TMPro.TextAlignmentOptions.Center;
            var area = message.GetComponent<RectTransform>(); area.anchorMin = new Vector2(.1f,.25f); area.anchorMax = new Vector2(.9f,.75f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            if (activationStatus != null && activationStatus.NeedsRecovery)
            {
                bool zh = Localization.Current == Language.Chinese;
                label.text = zh
                    ? "本地账号设置需要恢复。为保护隐私，资料库暂时锁定。原始记录未改变。"
                    : activationStatus.Reason;
                if (activationStatus.CanRecover)
                {
                    var buttonObject = new GameObject("RestoreAccountSettings", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                    buttonObject.transform.SetParent(panel.transform, false);
                    var buttonRect = buttonObject.GetComponent<RectTransform>();
                    buttonRect.anchorMin = new Vector2(.15f,.12f); buttonRect.anchorMax = new Vector2(.85f,.23f);
                    buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
                    buttonObject.GetComponent<UnityEngine.UI.Image>().color = new Color(.05f,.4f,.38f,1);
                    var caption = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                    caption.transform.SetParent(buttonObject.transform, false);
                    var text = caption.GetComponent<TMPro.TextMeshProUGUI>();
                    text.text = zh ? "从恢复副本还原账号设置" : "Restore account settings from recovery copy";
                    text.fontSize = 20; text.alignment = TMPro.TextAlignmentOptions.Center;
                    text.raycastTarget = false;
                    var captionRect = caption.GetComponent<RectTransform>();
                    captionRect.anchorMin = Vector2.zero; captionRect.anchorMax = Vector2.one;
                    captionRect.offsetMin = new Vector2(12,4); captionRect.offsetMax = new Vector2(-12,-4);
                    var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
                    button.onClick.AddListener(() =>
                    {
                        try
                        {
                            LocalOwnershipActivation.Restore(Application.persistentDataPath);
                            button.interactable = false;
                            label.text = zh ? "设置已恢复。请关闭并重新打开 Sandtray。记录未移动或覆盖。"
                                : "Settings recovered. Close and reopen Sandtray. No records were moved or overwritten.";
                        }
                        catch (Exception) { label.text = zh ? "无法安全恢复。请保留文件并联系 contact@sandtraypro.com。"
                            : "Recovery could not finish safely. Keep your files and contact contact@sandtraypro.com."; }
                    });
                }
                else label.text += "\ncontact@sandtraypro.com";
                if (UnityEngine.EventSystems.EventSystem.current == null)
                {
                    var events = new GameObject("RecoveryEventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                    events.transform.SetParent(shield.transform, false);
                }
            }
            try { NetworkBootstrapper.Instance?.Disconnect(); }
            catch (Exception ex) { Debug.LogWarning("[Local ownership] Session cleanup: " + ex.GetType().Name); }
            try { AgoraManager.Instance?.LeaveChannel(); }
            catch (Exception ex) { Debug.LogWarning("[Local ownership] Audio cleanup: " + ex.GetType().Name); }
            if (transitioning)
            {
                label.text = Localization.Current == Language.Chinese ? "正在安全切换账号…" : "Switching accounts safely…";
                shield.AddComponent<LocalAccountTransition>().Begin();
            }
            // Private views and their delayed callbacks must not remain interactive underneath.
            foreach (var bootstrap in UnityEngine.Object.FindObjectsOfType<SceneBootstrapper>()) bootstrap.StopAllCoroutines();
        }
        public static void ActivationCompleted() { forcedRestart = true; ShowRestartShield(); }
        public static void RequireCurrent()
        { if (RequiresRestart) throw new UnauthorizedAccessException("Reopen Sandtray to finish switching local accounts."); }
        public static LocalCapacityJournal ExistingCapacityJournal()
        {
            RequireCurrent();
            if (!enabled || User <= 0) return null;
            string path = Path.Combine(scope.Directory, ".capacity-v1", "journal.json");
            return File.Exists(path) || File.Exists(path + ".bak") ? OpenCapacityJournal() : null;
        }
        // Opt-in ownership is a prerequisite, never an implicit capacity activation.
        public static LocalCapacityJournal OpenCapacityJournal()
        {
            RequireCurrent();
            if (!enabled || User <= 0) throw new InvalidOperationException("Activate account ownership and sign in before using capacity reservations.");
            int owner = User; string authority = BackendClient.BaseUrl;
            var pinned = scope;
            return new LocalCapacityJournal(pinned.Directory, authority, owner, () =>
            {
                RequireCurrent(); pinned.RequireCurrent();
                if (User != owner || BackendClient.BaseUrl != authority)
                    throw new UnauthorizedAccessException("Capacity journal account or backend changed.");
            });
        }
        public static bool IsIsolated { get { RequireCurrent(); return enabled; } }
        public static void ActivateEmptyWorkspace()
        {
            RequireCurrent();
            if (enabled || User <= 0) throw new InvalidOperationException("Sign in to start a private library.");
            string root = Application.persistentDataPath;
            string directory = Path.Combine(root, "LocalOwnershipV1");
            LocalTableImport.Safe(root, directory); Directory.CreateDirectory(directory);
            string lockPath = Path.Combine(directory, "migration.lock"); LocalTableImport.Safe(root, lockPath);
            using (var held = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var state = LocalOwnershipActivation.Inspect(root);
                if (state.Enabled) throw new InvalidOperationException("Existing ownership settings need review.");
                string account = AccountPath(root, BackendClient.BaseUrl, User);
                LocalTableImport.Safe(root, account); Directory.CreateDirectory(account);
                LocalOwnershipActivation.Commit(root, new Activation { Owner = User, Authority = BackendClient.BaseUrl });
            }
            ActivationCompleted();
        }
        public static string Root { get { RequireCurrent(); return scope.Directory; } }

        /// <summary>
        /// Remove the current account's isolated on-device records after a
        /// server-side account deletion. The legacy shared directory is never
        /// removed because it may contain records belonging to other accounts.
        /// </summary>
        public static bool DeleteCurrentAccountData()
        {
            RequireCurrent();
            if (!enabled || User <= 0 || scope == null) return false;
            string directory = scope.Directory;
            scope.Invalidate();
            Epoch++;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            return true;
        }
    }
}
