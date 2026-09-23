using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;

namespace Sandplay.UI
{
    /// <summary>
    /// Helper component to add "Upload Custom Object" functionality to any button.
    /// Attach this to a button in your catalog UI to enable uploads.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CatalogUploadButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CatalogUploadUI _uploadUI;

        [Header("Settings")]
        [Tooltip("The catalog ID to upload objects to. Leave empty to use the user's first catalog.")]
        [SerializeField] private string _catalogId;

        [Tooltip("If true, will only show button when user is subscribed")]
        [SerializeField] private bool _requiresSubscription = false;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnButtonClick);
        }

        private void Start()
        {
            UpdateButtonVisibility();
        }

        private void OnButtonClick()
        {
            if (_uploadUI == null)
            {
                Debug.LogError("[CatalogUploadButton] No CatalogUploadUI assigned!");
                return;
            }

            // Check if user is logged in
            if (BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn)
            {
                Debug.LogWarning("[CatalogUploadButton] User must be logged in to upload objects");
                // Optionally show a login prompt here
                return;
            }

            BackendClient.Instance.FetchAccessSnapshot(snapshot =>
            {
                if (this == null) return;
                if (AccessPolicy.Evaluate(snapshot, "catalog.custom.capacity") != AccessDecision.Allowed)
                { Debug.LogWarning("[CatalogUploadButton] Custom object access or available capacity is required."); return; }
                OpenUpload();
            }, error => Debug.LogWarning("[CatalogUploadButton] " + error));
        }

        private void OpenUpload()
        {
            // Use provided catalog ID or fetch user's first catalog
            string catalogId = _catalogId;
            if (string.IsNullOrEmpty(catalogId))
            {
                // TODO: Implement fetching user's catalogs from backend
                // For now, you can hardcode a default catalog or implement
                // BackendClient.FetchUserCatalogs() method
                Debug.LogWarning("[CatalogUploadButton] No catalog ID provided. Set it in the Inspector or implement catalog fetching.");
                return;
            }

            _uploadUI.Show(catalogId);
        }

        private void UpdateButtonVisibility()
        {
            if (_button == null) return;

            // Only show button if logged in (and subscribed if required)
            bool shouldShow = BackendClient.Instance != null &&
                            BackendClient.Instance.IsLoggedIn;

            if (_requiresSubscription && shouldShow)
            {
                shouldShow = BackendClient.Instance.IsLoggedIn; // Actual capability is checked on tap.
            }

            _button.gameObject.SetActive(shouldShow);
        }

        /// <summary>
        /// Call this to set the catalog ID dynamically at runtime.
        /// </summary>
        public void SetCatalogId(string catalogId)
        {
            _catalogId = catalogId;
        }

        /// <summary>
        /// Call this when login state changes to update button visibility.
        /// </summary>
        public void RefreshVisibility()
        {
            UpdateButtonVisibility();
        }
    }
}
