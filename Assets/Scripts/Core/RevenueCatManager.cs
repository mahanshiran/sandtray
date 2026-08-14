// ============================================================
//  RevenueCatManager.cs
//  Sandplay – Subscription management via RevenueCat
// ============================================================
//
//  SETUP INSTRUCTIONS
//  ------------------
//  1. Download the RevenueCat Unity SDK (.unitypackage) from:
//       https://github.com/RevenueCat/purchases-unity/releases
//     and import it into the project.
//
//  2. After importing, add  REVENUECAT_INSTALLED  to:
//       Edit > Project Settings > Player > Scripting Define Symbols
//     (do this for iOS AND Android build targets separately)
//
//  3. Fill in your API keys in GameConfig (Inspector) or via
//     RevenueCatManager.Initialize(appleKey, googleKey).
//
//  4. In the RevenueCat dashboard:
//     - Create a Product (monthly + annual)
//     - Create an Entitlement with identifier  "premium"
//     - Attach the products to the entitlement
//     - Create an Offering called "default" with Monthly + Annual packages
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;


namespace Sandplay.Core
{
    /// <summary>
    /// Singleton manager for RevenueCat subscription handling.
    /// Works on iOS and Android. On other platforms IsSubscribed is always false
    /// unless overridden (useful for editor testing via SetEditorMockSubscribed).
    /// </summary>
    public class RevenueCatManager : MonoBehaviour
    {
        public static RevenueCatManager Instance { get; private set; }

        // ── Runtime events ──────────────────────────────────────────────────
        /// <summary>Fired whenever the subscription status changes.</summary>
        public event Action<bool> OnSubscriptionStatusChanged;

        // ── Debug ───────────────────────────────────────────────────────────
        /// <summary>When true, all subscription checks return "subscribed" regardless of real status.</summary>
        public static bool IsDebugMode = false;

        // ── Status ──────────────────────────────────────────────────────────
        public bool IsSubscribed => IsDebugMode || _isSubscribed;
        private bool _isSubscribed;
        public bool IsInitialized { get; private set; }

        // ── Cached offering data (platform-agnostic wrapper) ─────────────
        public List<SubscriptionPackage> AvailablePackages { get; } = new List<SubscriptionPackage>();

        private string _entitlementId = "premium";
        private string _identifiedAppUserId = "";
        private bool _identityChangeInProgress;
        private int _identityGeneration;
        private readonly List<Action<bool, string>> _identityCallbacks =
            new List<Action<bool, string>>();

#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        private Purchases _purchases;
        private Purchases.Offerings _cachedOfferings;
#endif

        // ── Unity lifecycle ─────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Initialization ──────────────────────────────────────────────────

        /// <summary>
        /// Call once from SceneBootstrapper.Bootstrap() with keys from GameConfig.
        /// </summary>
        public void Initialize(string appleApiKey, string googleApiKey, string entitlementId = "premium")
        {
            _entitlementId = entitlementId;

#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
#if UNITY_IOS
            string apiKey = appleApiKey;
#else
            string apiKey = googleApiKey;
#endif
            if (string.IsNullOrEmpty(apiKey) || apiKey.StartsWith("REPLACE"))
            {
                Debug.LogWarning("[RevenueCat] API key not set — skipping initialization.");
                IsInitialized = true;
                return;
            }

            var go = new GameObject("Purchases");
            DontDestroyOnLoad(go);
            _purchases = go.AddComponent<Purchases>();
            // Set API keys as fields — Purchases.Start() will call Configure() automatically
#if UNITY_IOS
            _purchases.revenueCatAPIKeyApple = apiKey;
#else
            _purchases.revenueCatAPIKeyGoogle = apiKey;
#endif
            var backend = BackendClient.Instance;
            if (backend.IsLoggedIn && !string.IsNullOrEmpty(backend.RevenueCatAppUserId))
            {
                _identifiedAppUserId = backend.RevenueCatAppUserId;
                _purchases.appUserID = _identifiedAppUserId;
            }
            IsInitialized = true;
            // RefreshSubscriptionStatus after a frame so Purchases.Start() has run
            StartCoroutine(RefreshAfterStart());
            Debug.Log("[RevenueCat] Initialized.");
#else
            IsInitialized = true;
            Debug.Log("[RevenueCat] Not available on this platform / SDK not installed. Using mock mode.");
#endif
        }

        // ── Account identity ────────────────────────────────────────────────

        /// <summary>
        /// Associates RevenueCat purchases with the authenticated Sandtray account.
        /// The backend-provided identifier is stable across devices and platforms.
        /// </summary>
        public void IdentifyUser(string appUserId, Action<bool, string> onResult = null)
        {
            if (string.IsNullOrWhiteSpace(appUserId))
            {
                onResult?.Invoke(false, "A valid account is required.");
                return;
            }

#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            if (_purchases == null)
            {
                onResult?.Invoke(false, "RevenueCat is not initialized.");
                return;
            }

            if (_identifiedAppUserId == appUserId && !_identityChangeInProgress)
            {
                RefreshSubscriptionStatus();
                onResult?.Invoke(true, null);
                return;
            }

            if (_identityChangeInProgress)
            {
                if (_identifiedAppUserId == appUserId)
                {
                    if (onResult != null) _identityCallbacks.Add(onResult);
                }
                else
                {
                    onResult?.Invoke(false, "Another account change is still in progress.");
                }
                return;
            }

            _identifiedAppUserId = appUserId;
            _identityChangeInProgress = true;
            int operationGeneration = ++_identityGeneration;
            if (onResult != null) _identityCallbacks.Add(onResult);
            _purchases.LogIn(appUserId, (info, created, error) =>
            {
                if (operationGeneration != _identityGeneration) return;
                _identityChangeInProgress = false;
                bool succeeded = error == null;
                string message = error?.Message;
                if (succeeded)
                {
                    ApplyCustomerInfo(info);
                    Debug.Log($"[RevenueCat] Account linked ({(created ? "new" : "existing")} customer).");
                }
                else
                {
                    _identifiedAppUserId = "";
                    Debug.LogWarning($"[RevenueCat] Account link failed: {message}");
                }

                var callbacks = _identityCallbacks.ToArray();
                _identityCallbacks.Clear();
                foreach (var callback in callbacks)
                    callback?.Invoke(succeeded, message);
            });
#else
            _identifiedAppUserId = appUserId;
            onResult?.Invoke(true, null);
#endif
        }

        /// <summary>Returns RevenueCat to an anonymous customer after account sign-out.</summary>
        public void ClearUserIdentity(Action<bool, string> onResult = null)
        {
            _identityGeneration++;
            _identifiedAppUserId = "";
            _identityChangeInProgress = false;
            _identityCallbacks.Clear();

#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            if (_purchases == null)
            {
                SetSubscriptionStatus(false);
                onResult?.Invoke(true, null);
                return;
            }
            if (_purchases.IsAnonymous())
            {
                SetSubscriptionStatus(false);
                onResult?.Invoke(true, null);
                return;
            }

            int operationGeneration = _identityGeneration;
            _purchases.LogOut((info, error) =>
            {
                if (operationGeneration != _identityGeneration) return;
                SetSubscriptionStatus(false);
                if (error != null)
                {
                    Debug.LogWarning($"[RevenueCat] Sign-out identity reset failed: {error.Message}");
                    onResult?.Invoke(false, error.Message);
                    return;
                }
                onResult?.Invoke(true, null);
            });
#else
            SetSubscriptionStatus(false);
            onResult?.Invoke(true, null);
#endif
        }

        // ── Subscription status ─────────────────────────────────────────────

#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        private System.Collections.IEnumerator RefreshAfterStart()
        {
            yield return null; // wait one frame for Purchases.Start() to run
            RefreshSubscriptionStatus();
        }
#endif

        public void RefreshSubscriptionStatus()
        {
#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            _purchases.GetCustomerInfo((info, error) =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"[RevenueCat] GetCustomerInfo error: {error.Message}");
                    return;
                }
                ApplyCustomerInfo(info);
            });
#endif
        }

#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        private void ApplyCustomerInfo(Purchases.CustomerInfo info)
        {
            SetSubscriptionStatus(
                info != null && info.Entitlements.Active.ContainsKey(_entitlementId));
        }
#endif

        private void SetSubscriptionStatus(bool subscribed)
        {
            bool wasSubscribed = IsSubscribed;
            _isSubscribed = subscribed;
            if (wasSubscribed != IsSubscribed)
                OnSubscriptionStatusChanged?.Invoke(IsSubscribed);
        }

        private void EnsureAccountIdentity(Action onSuccess, Action<string> onError)
        {
            var backend = BackendClient.Instance;
            if (!backend.IsLoggedIn || string.IsNullOrEmpty(backend.RevenueCatAppUserId))
            {
                onError?.Invoke("Please sign in before purchasing or restoring.");
                return;
            }

            IdentifyUser(backend.RevenueCatAppUserId, (ok, error) =>
            {
                if (ok) onSuccess?.Invoke();
                else onError?.Invoke(error ?? "Could not link the subscription to your account.");
            });
        }

        // ── Offerings ───────────────────────────────────────────────────────

        /// <summary>Fetch available subscription packages from RevenueCat.</summary>
        public void FetchOfferings(Action onSuccess, Action<string> onError)
        {
            AvailablePackages.Clear();

#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            _purchases.GetOfferings((offerings, error) =>
            {
                if (error != null)
                {
                    onError?.Invoke(error.Message);
                    return;
                }

                _cachedOfferings = offerings;
                var current = offerings?.Current;
                if (current != null)
                {
                    foreach (var pkg in current.AvailablePackages)
                    {
                        AvailablePackages.Add(new SubscriptionPackage
                        {
                            Identifier  = pkg.Identifier,
                            Title       = pkg.StoreProduct.Title,
                            Description = pkg.StoreProduct.Description,
                            PriceString = pkg.StoreProduct.PriceString,
                            PackageType = MapPackageType(pkg.PackageType, pkg.Identifier),
                            NativePackage = pkg
                        });
                    }
                }
                onSuccess?.Invoke();
            });
#else
            // RevenueCat SDK not installed or unsupported platform — no packages available.
            onError?.Invoke("RevenueCat SDK not installed. Follow setup instructions.");
#endif
        }

        // ── Purchase ────────────────────────────────────────────────────────

        public void PurchasePackage(SubscriptionPackage package, Action<bool, string> onResult)
        {
#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            if (package?.NativePackage == null)
            {
                onResult?.Invoke(false, "Invalid package.");
                return;
            }

            EnsureAccountIdentity(() =>
            {
                _purchases.PurchasePackage(
                    (Purchases.Package)package.NativePackage,
                    (result) =>
                    {
                        if (result.UserCancelled) { onResult?.Invoke(false, null); return; }
                        if (result.Error != null) { onResult?.Invoke(false, result.Error.Message); return; }
                        ApplyCustomerInfo(result.CustomerInfo);
                        onResult?.Invoke(IsSubscribed, null);
                    });
            }, error => onResult?.Invoke(false, error));
#else
            onResult?.Invoke(false, "RevenueCat SDK not installed.");
#endif
        }

        // ── Restore ─────────────────────────────────────────────────────────

        public void RestorePurchases(Action<bool, string> onResult)
        {
#if REVENUECAT_INSTALLED && (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            EnsureAccountIdentity(() =>
            {
                _purchases.RestorePurchases((info, error) =>
                {
                    if (error != null) { onResult?.Invoke(false, error.Message); return; }
                    ApplyCustomerInfo(info);
                    onResult?.Invoke(IsSubscribed, null);
                });
            }, error => onResult?.Invoke(false, error));
#else
            onResult?.Invoke(false, "Not available in Editor.");
#endif
        }

        // ── Editor testing helper ────────────────────────────────────────────

        /// <summary>Override subscription status for Editor / QA testing.</summary>
        public void SetEditorMockSubscribed(bool subscribed)
        {
            bool changed = IsSubscribed != subscribed;
            _isSubscribed = subscribed;
            if (changed) OnSubscriptionStatusChanged?.Invoke(IsSubscribed);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static SubPackageType MapPackageType(string packageType, string identifier = null)
        {
            // RevenueCat Unity may return enum names ("MONTHLY") or identifiers ("$rc_monthly").
            string key = (packageType ?? "").Trim();
            if (string.IsNullOrEmpty(key) || key.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase)
                || key.Equals("CUSTOM", StringComparison.OrdinalIgnoreCase))
                key = identifier ?? "";

            key = key.Trim().ToUpperInvariant().Replace("$RC_", "").Replace("-", "_");

            return key switch
            {
                "MONTHLY" or "MONTH" => SubPackageType.Monthly,
                "ANNUAL" or "YEARLY" or "YEAR" => SubPackageType.Annual,
                "WEEKLY" or "WEEK" => SubPackageType.Weekly,
                "TWO_MONTH" or "TWOMONTH" => SubPackageType.TwoMonth,
                "THREE_MONTH" or "THREEMONTH" => SubPackageType.ThreeMonth,
                "SIX_MONTH" or "SIXMONTH" => SubPackageType.SixMonth,
                "LIFETIME" or "LIFE_TIME" => SubPackageType.Lifetime,
                _ => SubPackageType.Custom
            };
        }
    }

    // ── Platform-agnostic data classes ──────────────────────────────────────

    public enum SubPackageType { Monthly, Annual, Weekly, TwoMonth, ThreeMonth, SixMonth, Lifetime, Custom }

    public class SubscriptionPackage
    {
        public string Identifier;
        public string Title;
        public string Description;
        public string PriceString;
        public SubPackageType PackageType;
        /// <summary>The native RevenueCat Package object. Null on unsupported platforms.</summary>
        public object NativePackage;
    }
}
