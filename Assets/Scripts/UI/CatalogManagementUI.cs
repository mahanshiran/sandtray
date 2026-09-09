using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.UI
{
    /// <summary>
    /// UI panel for managing user's catalogs and catalog objects.
    /// Shows user's catalogs, allows selecting one, and displays/manages its objects.
    /// </summary>
    public class CatalogManagementUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Dropdown _catalogDropdown;
        [SerializeField] private TMP_Dropdown _visibilityDropdown;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _createCatalogButton;
        [SerializeField] private Transform _objectListContent;
        [SerializeField] private GameObject _objectItemPrefab;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private GameObject _loadingIndicator;

        [Header("Upload UI")]
        [SerializeField] private CatalogUploadUI _uploadUI;
        [SerializeField] private Button _uploadObjectButton;

        private List<UserCatalog> _userCatalogs = new List<UserCatalog>();
        private string _selectedCatalogId;
        private List<CatalogObjectData> _currentObjects = new List<CatalogObjectData>();

        private bool _initialized = false;
        private bool _settingVisibility;

        private void Start()
        {
            Debug.Log($"CatalogManagementUI.Start() called - _panel: {_panel != null}, _catalogDropdown: {_catalogDropdown != null}");
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;

            Debug.Log($"CatalogManagementUI.Initialize() - _panel: {_panel != null}, _catalogDropdown: {_catalogDropdown != null}");

            if (_closeButton) _closeButton.onClick.AddListener(Hide);
            if (_refreshButton) _refreshButton.onClick.AddListener(() => LoadUserCatalogs());
            if (_createCatalogButton) _createCatalogButton.onClick.AddListener(OnCreateCatalog);
            if (_uploadObjectButton)
            {
                Debug.Log($"Wiring upload button - interactable: {_uploadObjectButton.interactable}, targetGraphic: {_uploadObjectButton.targetGraphic}");
                _uploadObjectButton.onClick.AddListener(() =>
                {
                    Debug.Log("Upload button clicked!");
                    OnUploadObject();
                });
            }
            if (_catalogDropdown) _catalogDropdown.onValueChanged.AddListener(OnCatalogSelected);
            if (_visibilityDropdown)
            {
                _visibilityDropdown.ClearOptions();
                _visibilityDropdown.AddOptions(new List<string> { "Private", "Clinic", "Public", "Marketplace" });
                _visibilityDropdown.onValueChanged.AddListener(OnVisibilityChanged);
            }
            EventBus.Subscribe<CatalogRefreshRequestedEvent>(OnCatalogRefreshRequested);

            _initialized = true;
            Debug.Log($"Initialize() completed - buttons wired: close={_closeButton != null}, refresh={_refreshButton != null}, upload={_uploadObjectButton != null}");
        }

        private void OnDestroy()
        {
            if (_initialized)
                EventBus.Unsubscribe<CatalogRefreshRequestedEvent>(OnCatalogRefreshRequested);
        }

        private void OnCatalogRefreshRequested(CatalogRefreshRequestedEvent _)
        {
            LoadUserCatalogs();
        }

        public void Show()
        {
            Debug.Log($"CatalogManagementUI.Show() called - _panel: {_panel != null}");
            if (_panel)
            {
                _panel.SetActive(true);
                Debug.Log($"Panel set to active: {_panel.activeSelf}");
            }
            else
            {
                Debug.LogWarning("_panel is null in Show()!");
            }
            Debug.Log($"Component enabled: {enabled}, gameObject active: {gameObject.activeSelf}, gameObject name: {gameObject.name}");
            Debug.Log($"Can start coroutines: {enabled && gameObject.activeInHierarchy}");
            LoadUserCatalogs();
        }

        public void Hide()
        {
            if (_panel) _panel.SetActive(false);
        }

        private void LoadUserCatalogs()
        {
            Debug.Log("LoadUserCatalogs() called");
            StartCoroutine(LoadUserCatalogsCoroutine());
            Debug.Log("StartCoroutine(LoadUserCatalogsCoroutine) called");
        }

        private IEnumerator LoadUserCatalogsCoroutine()
        {
            Debug.Log("LoadUserCatalogsCoroutine started");
            SetLoadingState(true);
            UpdateStatus("Loading catalogs...");
            Debug.Log($"Status text updated, _statusText null: {_statusText == null}");

            var backend = BackendClient.Instance;
            Debug.Log($"BackendClient.Instance: {backend != null}, IsLoggedIn: {backend?.IsLoggedIn}");
            if (backend == null || !backend.IsLoggedIn)
            {
                UpdateStatus("Not logged in");
                SetLoadingState(false);
                Debug.LogWarning("Not logged in - catalog loading aborted");
                yield break;
            }

            Debug.Log("Calling BackendClient.FetchUserCatalogs()");
            var result = backend.FetchUserCatalogs();
            Debug.Log($"FetchUserCatalogs returned, yielding...");
            yield return result;
            Debug.Log($"Yield completed, result.Success: {result.Success}");

            if (result.Success)
            {
                _userCatalogs = result.Catalogs;
                Debug.Log($"Fetched {_userCatalogs.Count} catalogs");
                PopulateCatalogDropdown();
                UpdateStatus($"Loaded {_userCatalogs.Count} catalog(s)");
            }
            else
            {
                Debug.LogError($"Failed to load catalogs: {result.Error}");
                UpdateStatus($"Failed to load catalogs: {result.Error}");
                _userCatalogs.Clear();
                PopulateCatalogDropdown();
            }

            SetLoadingState(false);
            Debug.Log("LoadUserCatalogsCoroutine completed");
        }

        private void PopulateCatalogDropdown()
        {
            Debug.Log($"PopulateCatalogDropdown() called, dropdown null: {_catalogDropdown == null}, catalog count: {_userCatalogs.Count}");
            if (_catalogDropdown == null) return;

            _catalogDropdown.ClearOptions();

            if (_userCatalogs.Count == 0)
            {
                _catalogDropdown.AddOptions(new List<string> { "No catalogs" });
                _catalogDropdown.interactable = false;
                if (_visibilityDropdown) _visibilityDropdown.interactable = false;
                // Don't disable upload button - user can still create catalog by uploading
                // if (_uploadObjectButton) _uploadObjectButton.interactable = false;
                UpdateStatus("No catalogs found. Upload an object to create one.");
                ClearObjectList();
                return;
            }

            var options = new List<string>();
            foreach (var catalog in _userCatalogs)
            {
                options.Add($"{catalog.name} ({catalog.object_count} objects)");
            }

            _catalogDropdown.AddOptions(options);
            _catalogDropdown.interactable = true;
            if (_visibilityDropdown) _visibilityDropdown.interactable = true;
            if (_uploadObjectButton) _uploadObjectButton.interactable = true;

            // Select first catalog
            if (_userCatalogs.Count > 0)
            {
                _selectedCatalogId = _userCatalogs[0].id;
                ShowSelectedVisibility(_userCatalogs[0].visibility);
                LoadCatalogObjects(_selectedCatalogId);
            }
        }

        private void OnCatalogSelected(int index)
        {
            if (index < 0 || index >= _userCatalogs.Count) return;
            _selectedCatalogId = _userCatalogs[index].id;
            ShowSelectedVisibility(_userCatalogs[index].visibility);
            LoadCatalogObjects(_selectedCatalogId);
        }

        private void ShowSelectedVisibility(string visibility)
        {
            if (_visibilityDropdown == null) return;
            string[] values = { "private", "clinic", "public", "marketplace" };
            int index = Array.IndexOf(values, visibility ?? "private");
            _settingVisibility = true;
            _visibilityDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
            _settingVisibility = false;
        }

        private void OnVisibilityChanged(int index)
        {
            if (_settingVisibility || string.IsNullOrEmpty(_selectedCatalogId)) return;
            string[] values = { "private", "clinic", "public", "marketplace" };
            if (index < 0 || index >= values.Length) return;
            StartCoroutine(UpdateVisibilityCoroutine(values[index]));
        }

        private IEnumerator UpdateVisibilityCoroutine(string visibility)
        {
            UpdateStatus("Updating sharing...");
            var result = BackendClient.Instance.UpdateCatalogVisibility(_selectedCatalogId, visibility);
            yield return result;
            if (result.Success)
            {
                var selected = _userCatalogs.Find(c => c.id == _selectedCatalogId);
                if (selected != null) selected.visibility = visibility;
                UpdateStatus($"Sharing set to {visibility}.");
            }
            else
            {
                UpdateStatus($"Could not update sharing: {result.Error}");
                var selected = _userCatalogs.Find(c => c.id == _selectedCatalogId);
                if (selected != null) ShowSelectedVisibility(selected.visibility);
            }
        }

        private void LoadCatalogObjects(string catalogId)
        {
            StartCoroutine(LoadCatalogObjectsCoroutine(catalogId));
        }

        private IEnumerator LoadCatalogObjectsCoroutine(string catalogId)
        {
            SetLoadingState(true);
            UpdateStatus("Loading objects...");

            var backend = BackendClient.Instance;
            var result = backend.FetchCatalogObjects(catalogId);
            yield return result;

            if (result.Success)
            {
                _currentObjects = result.Objects;
                PopulateObjectList();
                UpdateStatus($"Loaded {_currentObjects.Count} object(s)");
            }
            else
            {
                UpdateStatus($"Failed to load objects: {result.Error}");
                _currentObjects.Clear();
                PopulateObjectList();
            }

            SetLoadingState(false);
        }

        private void PopulateObjectList()
        {
            if (_objectListContent == null) return;

            // Clear existing
            foreach (Transform child in _objectListContent)
            {
                Destroy(child.gameObject);
            }

            if (_currentObjects.Count == 0)
            {
                var emptyGo = new GameObject("EmptyMessage");
                emptyGo.transform.SetParent(_objectListContent, false);
                var emptyText = emptyGo.AddComponent<TextMeshProUGUI>();
                emptyText.text = "No objects in this catalog";
                emptyText.alignment = TextAlignmentOptions.Center;
                emptyText.color = new Color(0.5f, 0.5f, 0.5f);
                var emptyRT = emptyGo.GetComponent<RectTransform>();
                emptyRT.sizeDelta = new Vector2(400, 60);
                return;
            }

            // Create object items
            foreach (var obj in _currentObjects)
            {
                CreateObjectItem(obj);
            }
        }

        private void CreateObjectItem(CatalogObjectData obj)
        {
            GameObject itemGo;

            if (_objectItemPrefab != null)
            {
                itemGo = Instantiate(_objectItemPrefab, _objectListContent);
            }
            else
            {
                // Create simple item if no prefab
                itemGo = new GameObject($"Object_{obj.id}");
                itemGo.transform.SetParent(_objectListContent, false);

                var itemRT = itemGo.AddComponent<RectTransform>();
                itemRT.sizeDelta = new Vector2(0, 60);

                var bg = itemGo.AddComponent<Image>();
                bg.color = new Color(0.2f, 0.25f, 0.3f, 0.8f);

                // Name text
                var nameGo = new GameObject("Name");
                nameGo.transform.SetParent(itemGo.transform, false);
                var nameText = nameGo.AddComponent<TextMeshProUGUI>();
                nameText.text = obj.display_name;
                nameText.fontSize = 16;
                nameText.alignment = TextAlignmentOptions.Left;
                var nameRT = nameGo.GetComponent<RectTransform>();
                nameRT.anchorMin = new Vector2(0, 0);
                nameRT.anchorMax = new Vector2(0.7f, 1);
                nameRT.offsetMin = new Vector2(10, 5);
                nameRT.offsetMax = new Vector2(-5, -5);

                // Category text
                var catGo = new GameObject("Category");
                catGo.transform.SetParent(itemGo.transform, false);
                var catText = catGo.AddComponent<TextMeshProUGUI>();
                catText.text = obj.category;
                catText.fontSize = 12;
                catText.color = new Color(0.7f, 0.7f, 0.7f);
                catText.alignment = TextAlignmentOptions.Left;
                var catRT = catGo.GetComponent<RectTransform>();
                catRT.anchorMin = new Vector2(0, 0);
                catRT.anchorMax = new Vector2(0.7f, 0.4f);
                catRT.offsetMin = new Vector2(10, 5);
                catRT.offsetMax = new Vector2(-5, -5);

                // Delete button
                var deleteBtn = CreateButton(itemGo.transform, "DeleteBtn", "Delete",
                    new Vector2(0.75f, 0.1f), new Vector2(0.95f, 0.9f),
                    new Color(0.8f, 0.3f, 0.3f, 0.9f));
                deleteBtn.onClick.AddListener(() => OnDeleteObject(obj));
            }
        }

        private Button CreateButton(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);

            var btnRT = btnGo.AddComponent<RectTransform>();
            btnRT.anchorMin = anchorMin;
            btnRT.anchorMax = anchorMax;
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = color;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 14;
            txt.color = Color.white;
            var txtRT = txtGo.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            return btn;
        }

        private void OnUploadObject()
        {
            Debug.Log($"OnUploadObject called - _uploadUI: {_uploadUI != null}, _selectedCatalogId: {_selectedCatalogId}");

            if (_uploadUI == null)
            {
                Debug.LogWarning("Upload UI not assigned");
                UpdateStatus("Upload UI not assigned");
                return;
            }

            if (string.IsNullOrEmpty(_selectedCatalogId))
            {
                UpdateStatus("Please create a catalog first using the 'Create Catalog' button.");
                Debug.LogWarning("No catalog selected - user needs to create a catalog first");
                return;
            }

            _uploadUI.Show(_selectedCatalogId);
        }

        private void OnCreateCatalog()
        {
            // Create a default catalog with username or timestamp
            var backend = BackendClient.Instance;
            string catalogName = $"My Catalog {System.DateTime.Now:yyyy-MM-dd HH:mm}";

            Debug.Log($"Creating catalog: {catalogName}");
            UpdateStatus($"Creating catalog '{catalogName}'...");

            StartCoroutine(CreateCatalogCoroutine(catalogName));
        }

        private IEnumerator CreateCatalogCoroutine(string catalogName)
        {
            var backend = BackendClient.Instance;
            var result = backend.CreateCatalog(catalogName);
            yield return result;

            if (result.Success)
            {
                Debug.Log($"Catalog created successfully, ID: {result.CatalogId}");
                UpdateStatus($"Catalog '{catalogName}' created successfully!");
                LoadUserCatalogs(); // Refresh the catalog list
            }
            else
            {
                Debug.LogError($"Failed to create catalog: {result.Error}");
                UpdateStatus($"Failed to create catalog: {result.Error}");
            }
        }

        private void OnDeleteObject(CatalogObjectData obj)
        {
            StartCoroutine(DeleteObjectCoroutine(obj));
        }

        private IEnumerator DeleteObjectCoroutine(CatalogObjectData obj)
        {
            SetLoadingState(true);
            UpdateStatus($"Deleting {obj.display_name}...");

            var backend = BackendClient.Instance;
            var result = backend.DeleteCatalogObject(_selectedCatalogId, obj.id);
            yield return result;

            if (result.Success)
            {
                UpdateStatus($"Deleted {obj.display_name}");
                LoadCatalogObjects(_selectedCatalogId); // Refresh list
            }
            else
            {
                UpdateStatus($"Failed to delete: {result.Error}");
                SetLoadingState(false);
            }
        }

        private void UpdateStatus(string message)
        {
            if (_statusText) _statusText.text = message;
        }

        private void SetLoadingState(bool loading)
        {
            if (_loadingIndicator) _loadingIndicator.SetActive(loading);
        }

        private void ClearObjectList()
        {
            if (_objectListContent == null) return;
            foreach (Transform child in _objectListContent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    // Data structures for catalog management
    [Serializable]
    public class UserCatalog
    {
        public string id;
        public string name;
        public string visibility;
        public int object_count;
        public string created_at;
    }

    [Serializable]
    public class UserCatalogsResponse
    {
        public UserCatalog[] catalogs;
    }

    [Serializable]
    public class PaginatedUserCatalogsResponse
    {
        public UserCatalog[] results;
    }

    [Serializable]
    public class CatalogObjectData
    {
        public string id;
        public string display_name;
        public string category;
        public string[] tags;
        public string model_url;
        public string thumbnail_url;
        public string model_hash;
        public string description;
        public string created_at;
    }

    [Serializable]
    public class CatalogObjectsResponse
    {
        public CatalogObjectData[] objects;
    }

    [Serializable]
    public class PaginatedCatalogObjectsResponse
    {
        public CatalogObjectData[] results;
    }
}
