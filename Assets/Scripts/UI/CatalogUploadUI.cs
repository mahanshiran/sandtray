using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Objects;
using SimpleFileBrowser;

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
        private bool _initialized;

        private void Start()
        {
            InitializeListeners();
        }

        private void InitializeListeners()
        {
            if (_initialized) return;

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
            _initialized = true;
        }

        /// <summary>
        /// Show the upload panel for the specified catalog ID.
        /// </summary>
        public void Show(string catalogId)
        {
            _currentCatalogId = catalogId;
            InitializeListeners();
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
            PickRuntimeFile(true);
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
            PickRuntimeFile(false);
#endif
        }

        private void PickRuntimeFile(bool model)
        {
#if UNITY_IOS || UNITY_ANDROID
            if (NativeFilePicker.IsFilePickerBusy()) return;
            string[] types = model
                ? new[] { NativeFilePicker.ConvertExtensionToFileType("glb") }
                : new[]
                {
                    NativeFilePicker.ConvertExtensionToFileType("png"),
                    NativeFilePicker.ConvertExtensionToFileType("jpg"),
                    NativeFilePicker.ConvertExtensionToFileType("jpeg"),
                    NativeFilePicker.ConvertExtensionToFileType("webp"),
                };
            NativeFilePicker.PickFile(path => ApplyPickedPath(path, model), types);
#else
            StartCoroutine(PickDesktopFile(model));
#endif
        }

#if !UNITY_IOS && !UNITY_ANDROID
        private IEnumerator PickDesktopFile(bool model)
        {
            if (model)
            {
                FileBrowser.SetFilters(false, new FileBrowser.Filter("GLB model", ".glb"));
                FileBrowser.SetDefaultFilter(".glb");
            }
            else
            {
                FileBrowser.SetFilters(false,
                    new FileBrowser.Filter("Images", ".png", ".jpg", ".jpeg", ".webp"));
                FileBrowser.SetDefaultFilter(".png");
            }

            yield return FileBrowser.WaitForLoadDialog(
                FileBrowser.PickMode.Files, false, null, null,
                model ? "Select GLB Model" : "Select Thumbnail", "Select");
            if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
                ApplyPickedPath(FileBrowser.Result[0], model);
        }
#endif

        private void ApplyPickedPath(string path, bool model)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (model)
            {
                _glbFilePath = path;
                if (_glbFilePathText) _glbFilePathText.text = Path.GetFileName(path);
            }
            else
            {
                _thumbnailFilePath = path;
                if (_thumbnailFilePathText) _thumbnailFilePathText.text = Path.GetFileName(path);
            }
            UpdateStatus("");
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
                thumbnail_url = thumbnailUrl,
                model_hash = glbUploadResult.Sha256,
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

        /// <summary>Build and wire the upload form used by the code-generated main menu.</summary>
        public void BuildRuntimeUI(GameObject panel, TMP_FontAsset font)
        {
            _panel = panel;
            var background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.72f);
            background.raycastTarget = true;

            var card = CreateRect(panel.transform, "UploadCard",
                new Vector2(0.22f, 0.10f), new Vector2(0.78f, 0.90f));
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = new Color(0.11f, 0.15f, 0.21f, 0.99f);

            CreateLabel(card, "Title", "Upload Custom Object", font, 25,
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.97f), TextAlignmentOptions.Center);

            _displayNameInput = CreateInput(card, "DisplayName", "Object name", font,
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.84f), false);
            _categoryDropdown = CreateDropdown(card, font,
                new Vector2(0.08f, 0.65f), new Vector2(0.92f, 0.73f));
            _tagsInput = CreateInput(card, "Tags", "Tags, separated by commas", font,
                new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.62f), false);
            _descriptionInput = CreateInput(card, "Description", "Optional description", font,
                new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.51f), true);

            _pickGlbButton = CreateButton(card, "PickModel", "Choose GLB", font,
                new Vector2(0.08f, 0.29f), new Vector2(0.32f, 0.37f));
            _glbFilePathText = CreateLabel(card, "ModelPath", "No file selected", font, 13,
                new Vector2(0.35f, 0.29f), new Vector2(0.92f, 0.37f), TextAlignmentOptions.Left);
            _pickThumbnailButton = CreateButton(card, "PickThumbnail", "Choose Thumbnail", font,
                new Vector2(0.08f, 0.19f), new Vector2(0.32f, 0.27f));
            _thumbnailFilePathText = CreateLabel(card, "ThumbnailPath", "No file selected", font, 13,
                new Vector2(0.35f, 0.19f), new Vector2(0.92f, 0.27f), TextAlignmentOptions.Left);

            _statusText = CreateLabel(card, "Status", "", font, 13,
                new Vector2(0.08f, 0.11f), new Vector2(0.92f, 0.17f), TextAlignmentOptions.Center);
            _statusText.color = new Color(1f, 0.82f, 0.38f);
            _cancelButton = CreateButton(card, "Cancel", "Cancel", font,
                new Vector2(0.40f, 0.03f), new Vector2(0.59f, 0.10f));
            _uploadButton = CreateButton(card, "Upload", "Upload", font,
                new Vector2(0.62f, 0.03f), new Vector2(0.92f, 0.10f));
            _uploadButton.GetComponent<Image>().color = new Color(0.24f, 0.62f, 0.45f, 1f);
            InitializeListeners();
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string value,
            TMP_FontAsset font, float size, Vector2 min, Vector2 max, TextAlignmentOptions alignment)
        {
            var rt = CreateRect(parent, name, min, max);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.font = font; text.fontSize = size;
            text.color = Color.white; text.alignment = alignment;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            return text;
        }

        private static TMP_InputField CreateInput(Transform parent, string name, string placeholder,
            TMP_FontAsset font, Vector2 min, Vector2 max, bool multiline)
        {
            var rt = CreateRect(parent, name, min, max);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.23f, 0.30f, 1f);
            var value = CreateLabel(rt, "Text", "", font, 15,
                new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f), TextAlignmentOptions.Left);
            var hint = CreateLabel(rt, "Placeholder", placeholder, font, 15,
                new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f), TextAlignmentOptions.Left);
            hint.color = new Color(1f, 1f, 1f, 0.42f);
            var input = rt.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.textViewport = rt;
            input.textComponent = value as TextMeshProUGUI;
            input.placeholder = hint as Graphic;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            return input;
        }

        private static Button CreateButton(Transform parent, string name, string value,
            TMP_FontAsset font, Vector2 min, Vector2 max)
        {
            var rt = CreateRect(parent, name, min, max);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.25f, 0.37f, 0.49f, 1f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateLabel(rt, "Label", value, font, 14, Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
            return button;
        }

        private static TMP_Dropdown CreateDropdown(Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max)
        {
            var rt = CreateRect(parent, "Category", min, max);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.23f, 0.30f, 1f);
            var dropdown = rt.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = image;
            var caption = CreateLabel(rt, "Label", "Category", font, 15,
                new Vector2(0.03f, 0), new Vector2(0.92f, 1), TextAlignmentOptions.Left);
            dropdown.captionText = caption as TextMeshProUGUI;

            var template = CreateRect(rt, "Template", new Vector2(0, 0), new Vector2(1, 0));
            template.pivot = new Vector2(0.5f, 1); template.sizeDelta = new Vector2(0, 240);
            template.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.17f, 0.23f, 1f);
            var viewport = CreateRect(template, "Viewport", Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect(viewport, "Content", new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 30);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            var item = CreateRect(content, "Item", new Vector2(0, 0.5f), new Vector2(1, 0.5f));
            item.sizeDelta = new Vector2(0, 30);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = item.gameObject.AddComponent<Image>();
            var itemText = CreateLabel(item, "Item Label", "Category", font, 14,
                new Vector2(0.04f, 0), new Vector2(0.96f, 1), TextAlignmentOptions.Left);
            dropdown.template = template;
            dropdown.itemText = itemText as TextMeshProUGUI;
            template.gameObject.SetActive(false);
            return dropdown;
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
        public string model_hash;
    }
}
