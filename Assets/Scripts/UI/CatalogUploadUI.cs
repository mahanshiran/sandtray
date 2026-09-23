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
        private Action<Image> _round;

        private static bool Light => PlayerPrefs.GetInt("sandplay_home_theme", 0) == 0;
        private static Color Surface => Light ? Color.white : new Color(.07f, .125f, .15f);
        private static Color Raised => Light ? new Color(.975f, .973f, .963f) : new Color(.045f, .09f, .11f);
        private static Color Ink => Light ? new Color(.025f, .075f, .145f) : new Color(.96f, .97f, .97f);
        private static Color Muted => Light ? new Color(.37f, .43f, .52f) : new Color(.65f, .73f, .78f);
        private static Color Accent => new Color(.025f, .43f, .40f);
        private static Color Border => Light ? new Color(.77f, .80f, .83f, .7f) : new Color(.25f, .36f, .40f);

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
        public void BuildRuntimeUI(GameObject panel, TMP_FontAsset font, Action<Image> round = null)
        {
            _panel = panel;
            _round = round;
            var background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, Light ? .42f : .68f);
            Sandplay.UI.DialogBackdrop.Apply(background);
            background.raycastTarget = true;

            var card = CreateRect(panel.transform, "UploadCard",
                new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.96f));
            var cardImage = StyleSurface(card, Surface);
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, Light ? .18f : .38f);
            shadow.effectDistance = new Vector2(0, -6);

            var title = CreateLabel(card, "Title", "Upload custom object", font, 26,
                new Vector2(0.055f, 0.88f), new Vector2(0.82f, 0.96f), TextAlignmentOptions.Left, Ink);
            title.fontStyle = FontStyles.Bold;
            var close = CreateButton(card, "Close", "×", font,
                new Vector2(0.90f, 0.89f), new Vector2(0.955f, 0.95f), Raised, Ink);
            close.onClick.AddListener(Hide);

            _displayNameInput = CreateInput(card, "DisplayName", "Object name", font,
                new Vector2(0.055f, 0.76f), new Vector2(0.945f, 0.835f), false);
            _categoryDropdown = CreateDropdown(card, font,
                new Vector2(0.055f, 0.655f), new Vector2(0.945f, 0.73f));
            _tagsInput = CreateInput(card, "Tags", "Tags, separated by commas", font,
                new Vector2(0.055f, 0.55f), new Vector2(0.945f, 0.625f), false);
            _descriptionInput = CreateInput(card, "Description", "Optional description", font,
                new Vector2(0.055f, 0.405f), new Vector2(0.945f, 0.52f), true);

            CreateFileRow(card, "ModelRow", font,
                new Vector2(0.055f, 0.295f), new Vector2(0.945f, 0.375f),
                "Choose GLB", out _pickGlbButton, out _glbFilePathText);
            CreateFileRow(card, "ThumbnailRow", font,
                new Vector2(0.055f, 0.195f), new Vector2(0.945f, 0.275f),
                "Choose thumbnail", out _pickThumbnailButton, out _thumbnailFilePathText);

            _statusText = CreateLabel(card, "Status", "", font, 13,
                new Vector2(0.055f, 0.12f), new Vector2(0.945f, 0.175f), TextAlignmentOptions.Left, Muted);
            _cancelButton = CreateButton(card, "Cancel", "Cancel", font,
                new Vector2(0.055f, 0.035f), new Vector2(0.47f, 0.105f), Raised, Ink);
            _uploadButton = CreateButton(card, "Upload", "Upload", font,
                new Vector2(0.53f, 0.035f), new Vector2(0.945f, 0.105f), Accent, Color.white);
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

        private TMP_Text CreateLabel(Transform parent, string name, string value,
            TMP_FontAsset font, float size, Vector2 min, Vector2 max, TextAlignmentOptions alignment,
            Color? color = null)
        {
            Color resolvedColor = color ?? Ink;
            if (ColorsMatch(resolvedColor, Muted)) size = Mathf.Max(6f, size - 2f);
            var rt = CreateRect(parent, name, min, max);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.font = font; text.fontSize = size;
            text.color = resolvedColor; text.alignment = alignment;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(10, size * .78f);
            text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static bool ColorsMatch(Color left, Color right)
        {
            const float tolerance = .002f;
            return Mathf.Abs(left.r - right.r) < tolerance && Mathf.Abs(left.g - right.g) < tolerance &&
                   Mathf.Abs(left.b - right.b) < tolerance && Mathf.Abs(left.a - right.a) < tolerance;
        }

        private TMP_InputField CreateInput(Transform parent, string name, string placeholder,
            TMP_FontAsset font, Vector2 min, Vector2 max, bool multiline)
        {
            var rt = CreateRect(parent, name, min, max);
            var image = StyleSurface(rt, Raised);
            var value = CreateLabel(rt, "Text", "", font, 15,
                new Vector2(0.025f, 0.08f), new Vector2(0.975f, 0.92f), TextAlignmentOptions.Left, Ink);
            var hint = CreateLabel(rt, "Placeholder", placeholder, font, 15,
                new Vector2(0.025f, 0.08f), new Vector2(0.975f, 0.92f), TextAlignmentOptions.Left, Muted);
            var input = rt.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.textViewport = rt;
            input.textComponent = value as TextMeshProUGUI;
            input.placeholder = hint as Graphic;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            return input;
        }

        private Button CreateButton(Transform parent, string name, string value,
            TMP_FontAsset font, Vector2 min, Vector2 max, Color background, Color foreground)
        {
            var rt = CreateRect(parent, name, min, max);
            var image = StyleSurface(rt, background);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(.82f, .82f, .82f);
            colors.disabledColor = new Color(.65f, .65f, .65f, .55f);
            button.colors = colors;
            CreateLabel(rt, "Label", value, font, 14, Vector2.zero, Vector2.one,
                TextAlignmentOptions.Center, foreground);
            return button;
        }

        private TMP_Dropdown CreateDropdown(Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max)
        {
            var rt = CreateRect(parent, "Category", min, max);
            var image = StyleSurface(rt, Raised);
            var dropdown = rt.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = image;
            var caption = CreateLabel(rt, "Label", "Category", font, 15,
                new Vector2(0.025f, 0), new Vector2(0.92f, 1), TextAlignmentOptions.Left, Ink);
            CreateLabel(rt, "Chevron", "⌄", font, 18,
                new Vector2(.92f, 0), new Vector2(.975f, 1), TextAlignmentOptions.Center, Muted);
            dropdown.captionText = caption as TextMeshProUGUI;

            var template = CreateRect(rt, "Template", new Vector2(0, 0), new Vector2(1, 0));
            template.pivot = new Vector2(0.5f, 1); template.sizeDelta = new Vector2(0, 240);
            StyleSurface(template, Surface);
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
            toggle.targetGraphic = StyleSurface(item, Surface);
            var itemText = CreateLabel(item, "Item Label", "Category", font, 14,
                new Vector2(0.04f, 0), new Vector2(0.96f, 1), TextAlignmentOptions.Left);
            dropdown.template = template;
            dropdown.itemText = itemText as TextMeshProUGUI;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private RectTransform CreateFileRow(Transform parent, string name, TMP_FontAsset font,
            Vector2 min, Vector2 max, string buttonTitle, out Button button, out TMP_Text path)
        {
            var row = CreateRect(parent, name, min, max);
            StyleSurface(row, Raised);
            button = CreateButton(row, "Choose", buttonTitle, font,
                new Vector2(.012f, .12f), new Vector2(.32f, .88f), Surface, Accent);
            path = CreateLabel(row, "Path", "No file selected", font, 13,
                new Vector2(.35f, .08f), new Vector2(.975f, .92f), TextAlignmentOptions.Left, Muted);
            return row;
        }

        private Image StyleSurface(RectTransform rt, Color color)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            _round?.Invoke(image);
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1, -1);
            return image;
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
