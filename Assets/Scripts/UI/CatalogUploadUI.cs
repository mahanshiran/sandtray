using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.UI
{
    /// <summary>
    /// UI panel for uploading custom catalog objects.
    /// Allows user to pick a GLB model file and thumbnail image,
    /// fill in object metadata, and upload to the backend.
    /// </summary>
    public class CatalogUploadUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_InputField _displayNameInput;
        [SerializeField] private TMP_Dropdown _categoryDropdown;
        [SerializeField] private TMP_InputField _tagsInput;
        [SerializeField] private TMP_InputField _descriptionInput;
        [SerializeField] private TMP_Text _glbFilePathText;
        [SerializeField] private TMP_Text _thumbnailFilePathText;
        [SerializeField] private Button _pickGlbButton;
        [SerializeField] private Button _pickThumbnailButton;
        [SerializeField] private Button _uploadButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private GameObject _loadingIndicator;

        private string _glbFilePath;
        private string _thumbnailFilePath;
        private string _currentCatalogId;

        private void Start()
        {
            if (_panel) _panel.SetActive(false);

            if (_pickGlbButton) _pickGlbButton.onClick.AddListener(OnPickGlbFile);
            if (_pickThumbnailButton) _pickThumbnailButton.onClick.AddListener(OnPickThumbnailFile);
            if (_uploadButton) _uploadButton.onClick.AddListener(OnUploadClicked);
            if (_cancelButton) _cancelButton.onClick.AddListener(Hide);

            // Populate category dropdown
            if (_categoryDropdown)
            {
                _categoryDropdown.ClearOptions();
                var options = new System.Collections.Generic.List<string>();
                foreach (ObjectCategory cat in Enum.GetValues(typeof(ObjectCategory)))
                {
                    options.Add(cat.ToString());
                }
                _categoryDropdown.AddOptions(options);
            }

            SetLoadingState(false);
        }

        /// <summary>
        /// Show the upload panel for the specified catalog ID.
        /// </summary>
        public void Show(string catalogId)
        {
            _currentCatalogId = catalogId;
            if (_panel) _panel.SetActive(true);
            ResetForm();
        }

        public void Hide()
        {
            if (_panel) _panel.SetActive(false);
        }

        private void ResetForm()
        {
            if (_displayNameInput) _displayNameInput.text = "";
            if (_tagsInput) _tagsInput.text = "";
            if (_descriptionInput) _descriptionInput.text = "";
            if (_glbFilePathText) _glbFilePathText.text = "No file selected";
            if (_thumbnailFilePathText) _thumbnailFilePathText.text = "No file selected";
            if (_statusText) _statusText.text = "";

            _glbFilePath = null;
            _thumbnailFilePath = null;

            SetLoadingState(false);
        }

        private void OnPickGlbFile()
        {
#if UNITY_EDITOR
            // EditorUtility is editor-only; it is not compiled into player builds.
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select GLB Model", "", "glb");
            if (!string.IsNullOrEmpty(path))
            {
                _glbFilePath = path;
                if (_glbFilePathText) _glbFilePathText.text = Path.GetFileName(path);
                UpdateStatus("");
            }
#else
            UpdateStatus("File picker not available in this build yet.");
#endif
        }

        private void OnPickThumbnailFile()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Thumbnail Image", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                _thumbnailFilePath = path;
                if (_thumbnailFilePathText) _thumbnailFilePathText.text = Path.GetFileName(path);
                UpdateStatus("");
            }
#else
            UpdateStatus("File picker not available in this build yet.");
#endif
        }

        private void OnUploadClicked()
        {
            // Validate inputs
            if (string.IsNullOrEmpty(_displayNameInput?.text))
            {
                UpdateStatus("Please enter a display name");
                return;
            }

            if (string.IsNullOrEmpty(_glbFilePath) || !File.Exists(_glbFilePath))
            {
                UpdateStatus("Please select a valid GLB file");
                return;
            }

            if (string.IsNullOrEmpty(_thumbnailFilePath) || !File.Exists(_thumbnailFilePath))
            {
                UpdateStatus("Please select a valid thumbnail image");
                return;
            }

            if (string.IsNullOrEmpty(_currentCatalogId))
            {
                UpdateStatus("No catalog ID set");
                return;
            }

            StartCoroutine(UploadCatalogObject());
        }

        private IEnumerator UploadCatalogObject()
        {
            SetLoadingState(true);
            UpdateStatus("Uploading files...");

            var backend = BackendClient.Instance;
            if (backend == null || !backend.IsLoggedIn)
            {
                UpdateStatus("Not logged in");
                SetLoadingState(false);
                yield break;
            }

            // Step 1: Upload GLB file
            byte[] glbData = File.ReadAllBytes(_glbFilePath);
            string glbFileName = Path.GetFileName(_glbFilePath);

            var glbUploadResult = backend.UploadFile(glbData, glbFileName, "model/gltf-binary");
            yield return glbUploadResult;

            if (!glbUploadResult.Success)
            {
                UpdateStatus($"GLB upload failed: {glbUploadResult.Error}");
                SetLoadingState(false);
                yield break;
            }

            string modelUrl = glbUploadResult.FileUrl;
            UpdateStatus("GLB uploaded. Uploading thumbnail...");

            // Step 2: Upload thumbnail image
            byte[] thumbnailData = File.ReadAllBytes(_thumbnailFilePath);
            string thumbnailFileName = Path.GetFileName(_thumbnailFilePath);
            string thumbnailMimeType = thumbnailFileName.EndsWith(".png") ? "image/png" : "image/jpeg";

            var thumbnailUploadResult = backend.UploadFile(thumbnailData, thumbnailFileName, thumbnailMimeType);
            yield return thumbnailUploadResult;

            if (!thumbnailUploadResult.Success)
            {
                UpdateStatus($"Thumbnail upload failed: {thumbnailUploadResult.Error}");
                SetLoadingState(false);
                yield break;
            }

            string thumbnailUrl = thumbnailUploadResult.FileUrl;
            UpdateStatus("Files uploaded. Creating catalog object...");

            // Step 3: Create catalog object
            var objectData = new CatalogObjectCreateRequest
            {
                display_name = _displayNameInput.text,
                category = _categoryDropdown.options[_categoryDropdown.value].text.ToLower(),
                tags = ParseTags(_tagsInput?.text),
                description = _descriptionInput?.text ?? "",
                model_url = modelUrl,
                thumbnail_url = thumbnailUrl
            };

            var createResult = backend.CreateCatalogObject(_currentCatalogId, objectData);
            yield return createResult;

            if (createResult.Success)
            {
                UpdateStatus("✓ Object uploaded successfully!");
                yield return new WaitForSeconds(1.5f);
                Hide();

                // Optionally refresh catalog
                EventBus.Publish(new CatalogRefreshRequestedEvent());
            }
            else
            {
                UpdateStatus($"Failed to create object: {createResult.Error}");
            }

            SetLoadingState(false);
        }

        private string[] ParseTags(string tagsInput)
        {
            if (string.IsNullOrEmpty(tagsInput)) return new string[0];
            return tagsInput.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();
        }

        private void UpdateStatus(string message)
        {
            if (_statusText) _statusText.text = message;
        }

        private void SetLoadingState(bool loading)
        {
            if (_loadingIndicator) _loadingIndicator.SetActive(loading);
            if (_uploadButton) _uploadButton.interactable = !loading;
            if (_pickGlbButton) _pickGlbButton.interactable = !loading;
            if (_pickThumbnailButton) _pickThumbnailButton.interactable = !loading;
        }
    }

    // Event for catalog refresh
    public struct CatalogRefreshRequestedEvent { }

    // Request payload for creating catalog objects
    [Serializable]
    public class CatalogObjectCreateRequest
    {
        public string display_name;
        public string category;
        public string[] tags;
        public string description;
        public string model_url;
        public string thumbnail_url;
    }
}
