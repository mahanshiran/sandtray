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
    /// <summary>Personal object library, including catalog sharing and thumbnail previews.</summary>
    public class CatalogManagementUI : MonoBehaviour
    {
        private TMP_FontAsset _font;
        private Action<Image> _round;
        private TMP_Dropdown _catalogDropdown;
        private TMP_Dropdown _visibilityDropdown;
        private TMP_InputField _search;
        private Transform _objectListContent;
        private TMP_Text _statusText;
        private TMP_Text _countText;
        private Button _uploadObjectButton;
        private Button _refreshButton;
        private Button _createCatalogButton;
        private CatalogUploadUI _uploadUI;
        private ScrollRect _listScroll;
        private GameObject _dialog;
        private readonly List<UserCatalog> _userCatalogs = new List<UserCatalog>();
        private List<CatalogObjectData> _currentObjects = new List<CatalogObjectData>();
        private readonly Dictionary<string, NetworkCatalogItem> _previews = new Dictionary<string, NetworkCatalogItem>();
        private string _selectedCatalogId;
        private int _catalogRequest;
        private int _objectsRequest;
        private Coroutine _previewRoutine;
        private bool _initialized;
        private bool _loading;
        private bool _updatingVisibility;

        private static readonly Color Surface = new Color(0.085f, 0.105f, 0.15f);
        private static readonly Color Raised = new Color(0.13f, 0.16f, 0.22f);
        private static readonly Color Ink = new Color(0.94f, 0.95f, 0.98f);
        private static readonly Color Muted = new Color(0.59f, 0.65f, 0.74f);
        private static readonly Color Accent = new Color(0.78f, 0.63f, 0.40f);
        private static readonly string[] VisibilityValues = { "private", "clinic", "public", "marketplace" };
        private static string L(string en, string zh) => Localization.Current == Language.Chinese ? zh : en;

        public void BuildRuntimeUI(TMP_FontAsset font, Action<Image> round)
        {
            _font = font;
            _round = round;
            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            gameObject.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.065f, 0.78f);

            var card = Rect(transform, "Library", new Vector2(0.065f, 0.065f), new Vector2(0.935f, 0.935f));
            Background(card, Surface);
            _round?.Invoke(card.GetComponent<Image>());
            var title = Label(card, "Title", L("Object library", "物件库"), 28, Ink);
            Top(title.rectTransform, 28, 18, -100, 48);
            var subtitle = Label(card, "Subtitle", L("Your collections, ready for every session.", "整理你的物件，让每次沙盘创作更从容。"), 14, Muted);
            Top(subtitle.rectTransform, 28, 62, -100, 26);
            var close = Button(card, "Close", "×", Raised, Ink, Hide);
            Top(close.GetComponent<RectTransform>(), -68, 24, -28, 40, 1, 1);
            close.GetComponentInChildren<TMP_Text>().fontSize = 26;

            var controls = Rect(card, "Controls", new Vector2(0, 1), Vector2.one);
            Top(controls, 28, 100, -28, 72);
            var catLabel = Label(controls, "CatalogLabel", L("COLLECTION", "物件集"), 10, Muted);
            Top(catLabel.rectTransform, 0, 0, -8, 18, 0, 0.50f);
            _catalogDropdown = Dropdown(controls, "CatalogDropdown", 308);
            Top(_catalogDropdown.GetComponent<RectTransform>(), 0, 23, -8, 46, 0, 0.50f);
            var shareLabel = Label(controls, "ShareLabel", L("SHARING", "共享范围"), 10, Muted);
            Top(shareLabel.rectTransform, 8, 0, -8, 18, 0.50f, 0.70f);
            _visibilityDropdown = Dropdown(controls, "VisibilityDropdown", 220);
            Top(_visibilityDropdown.GetComponent<RectTransform>(), 8, 23, -8, 46, 0.50f, 0.70f);
            _createCatalogButton = Button(controls, "NewCollection", L("+ New collection", "+ 新建物件集"), Raised, Ink, OnCreateCatalog);
            Top(_createCatalogButton.GetComponent<RectTransform>(), 8, 23, 0, 46, 0.70f, 1);

            var tools = Rect(card, "LibraryActions", new Vector2(0, 1), Vector2.one);
            Top(tools, 28, 184, -28, 44);
            _search = Input(tools, "Search", L("Search objects by name or category", "搜索名称或分类"));
            Top(_search.GetComponent<RectTransform>(), 0, 0, -12, 44, 0, 0.55f);
            _refreshButton = Button(tools, "Refresh", L("Refresh", "刷新"), Raised, Muted, LoadUserCatalogs);
            Top(_refreshButton.GetComponent<RectTransform>(), 0, 0, -10, 44, 0.55f, 0.72f);
            _uploadObjectButton = Button(tools, "Upload", L("+ Upload object", "+ 上传物件"), Accent, Surface, OnUploadObject);
            Top(_uploadObjectButton.GetComponent<RectTransform>(), 0, 0, 0, 44, 0.72f, 1);

            var viewport = Rect(card, "ObjectViewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(28, 58);
            viewport.offsetMax = new Vector2(-28, -244);
            Background(viewport, Surface);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "ObjectRows", new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10; layout.padding = new RectOffset(0, 8, 0, 4);
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _objectListContent = content;
            _listScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _listScroll.viewport = viewport; _listScroll.content = content;
            _listScroll.horizontal = false; _listScroll.movementType = ScrollRect.MovementType.Clamped;
            _listScroll.scrollSensitivity = 28;
            AddScrollbar(viewport, _listScroll);

            _countText = Label(card, "ObjectCount", "", 12, Muted);
            Bottom(_countText.rectTransform, 28, 16, -12, 26, 0, 0.3f);
            _statusText = Label(card, "Status", "", 12, Muted);
            Bottom(_statusText.rectTransform, 0, 16, -28, 26, 0.3f, 1);
            _statusText.alignment = TextAlignmentOptions.Right;

            var uploadPanel = Rect(transform, "CatalogUploadPanel", Vector2.zero, Vector2.one);
            uploadPanel.gameObject.SetActive(false);
            _uploadUI = uploadPanel.gameObject.AddComponent<CatalogUploadUI>();
            _uploadUI.BuildRuntimeUI(uploadPanel.gameObject, font);
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;
            _catalogDropdown.onValueChanged.AddListener(OnCatalogSelected);
            _visibilityDropdown.ClearOptions();
            _visibilityDropdown.AddOptions(new List<string> {
                L("Private", "私密"), L("Clinic", "诊所"), L("Public", "公开"), L("Marketplace", "市场")
            });
            _visibilityDropdown.onValueChanged.AddListener(OnVisibilityChanged);
            _search.onValueChanged.AddListener(_ => PopulateObjectList());
            EventBus.Subscribe<CatalogRefreshRequestedEvent>(OnCatalogRefreshRequested);
            _initialized = true;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            LoadUserCatalogs();
        }

        public void Hide() => Destroy(gameObject);

        private void OnDestroy()
        {
            if (_initialized) EventBus.Unsubscribe<CatalogRefreshRequestedEvent>(OnCatalogRefreshRequested);
            foreach (var item in _previews.Values)
            {
                if (item.ThumbnailSprite == null) continue;
                Destroy(item.ThumbnailSprite.texture);
                Destroy(item.ThumbnailSprite);
            }
        }

        private void OnCatalogRefreshRequested(CatalogRefreshRequestedEvent _) => LoadUserCatalogs();
        private void LoadUserCatalogs() => StartCoroutine(LoadUserCatalogsCoroutine());

        private IEnumerator LoadUserCatalogsCoroutine()
        {
            int request = ++_catalogRequest;
            SetLoadingState(true);
            UpdateStatus(L("Loading collections…", "正在加载物件集…"));
            var backend = BackendClient.Instance;
            if (backend == null || !backend.IsLoggedIn)
            {
                UpdateStatus(L("Sign in to view your collections.", "登录后查看你的物件集。"), true);
                SetLoadingState(false);
                yield break;
            }
            var result = backend.FetchUserCatalogs();
            yield return result;
            if (request != _catalogRequest) yield break;
            if (!result.Success)
            {
                UpdateStatus(result.Error, true);
                SetLoadingState(false);
                yield break;
            }
            _userCatalogs.Clear();
            _userCatalogs.AddRange(result.Catalogs);
            var options = new List<string>();
            foreach (var catalog in _userCatalogs)
                options.Add(catalog.name);
            _catalogDropdown.ClearOptions();
            _catalogDropdown.AddOptions(options.Count > 0 ? options : new List<string> { L("No collections yet", "还没有物件集") });
            int selected = _userCatalogs.FindIndex(c => c.id == _selectedCatalogId);
            selected = Mathf.Max(0, selected);
            _catalogDropdown.SetValueWithoutNotify(selected);
            SetLoadingState(false);
            if (_userCatalogs.Count > 0) OnCatalogSelected(selected);
            else
            {
                _selectedCatalogId = null;
                _currentObjects.Clear();
                PopulateObjectList();
                UpdateStatus(L("Create a collection to get started.", "创建物件集，开始添加物件。"));
            }
        }

        private void OnCatalogSelected(int index)
        {
            if (index < 0 || index >= _userCatalogs.Count) return;
            _selectedCatalogId = _userCatalogs[index].id;
            ShowSelectedVisibility(_userCatalogs[index].visibility);
            _search.SetTextWithoutNotify("");
            _currentObjects.Clear();
            PopulateObjectList();
            StartCoroutine(LoadCatalogObjectsCoroutine(_selectedCatalogId));
        }

        private void ShowSelectedVisibility(string visibility) =>
            _visibilityDropdown.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(VisibilityValues, visibility)));

        private void OnVisibilityChanged(int index)
        {
            if (_updatingVisibility || string.IsNullOrEmpty(_selectedCatalogId)) return;
            if (index >= 0 && index < VisibilityValues.Length)
                StartCoroutine(UpdateVisibilityCoroutine(_selectedCatalogId, VisibilityValues[index]));
        }

        private IEnumerator UpdateVisibilityCoroutine(string catalogId, string visibility)
        {
            _updatingVisibility = true;
            _visibilityDropdown.interactable = false;
            var result = BackendClient.Instance.UpdateCatalogVisibility(catalogId, visibility);
            yield return result;
            _updatingVisibility = false;
            var selected = _userCatalogs.Find(c => c.id == catalogId);
            if (result.Success && selected != null) selected.visibility = visibility;
            if (_selectedCatalogId == catalogId)
            {
                if (!result.Success && selected != null) ShowSelectedVisibility(selected.visibility);
                UpdateStatus(result.Success ? L("Sharing updated.", "共享范围已更新。") : result.Error, !result.Success);
            }
            SetLoadingState(_loading);
        }

        private IEnumerator LoadCatalogObjectsCoroutine(string catalogId)
        {
            int request = ++_objectsRequest;
            SetLoadingState(true);
            UpdateStatus(L("Loading objects…", "正在加载物件…"));
            var result = BackendClient.Instance.FetchCatalogObjects(catalogId);
            yield return result;
            if (request != _objectsRequest || _selectedCatalogId != catalogId) yield break;
            _currentObjects = result.Success ? result.Objects : new List<CatalogObjectData>();
            PopulateObjectList();
            UpdateStatus(result.Success ? L("Up to date", "已更新") : result.Error, !result.Success);
            SetLoadingState(false);
        }

        private void PopulateObjectList()
        {
            if (_objectListContent == null) return;
            if (_previewRoutine != null) StopCoroutine(_previewRoutine);
            foreach (Transform child in _objectListContent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            string query = _search != null ? _search.text.Trim() : "";
            var rows = new List<KeyValuePair<CatalogObjectData, Image>>();
            foreach (var obj in _currentObjects)
            {
                if (query.Length > 0 &&
                    (obj.display_name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (obj.category ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                rows.Add(new KeyValuePair<CatalogObjectData, Image>(obj, CreateObjectItem(obj)));
            }
            _countText.text = L($"{rows.Count} objects", $"{rows.Count} 个物件");
            if (rows.Count == 0)
            {
                var empty = Rect(_objectListContent, "EmptyState", Vector2.zero, Vector2.one);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 170;
                var headline = Label(empty, "Headline",
                    query.Length > 0 ? L("No matching objects", "没有匹配的物件") : L("Make room for your imagination", "为想象留出空间"), 21, Ink);
                Top(headline.rectTransform, 24, 40, -24, 36);
                headline.alignment = TextAlignmentOptions.Center;
                var hint = Label(empty, "Hint", query.Length > 0
                    ? L("Try a different name or category.", "试试其他名称或分类。")
                    : L("Upload a model and a preview image to build your collection.", "上传模型和预览图片，建立你的物件集。"), 14, Muted);
                Top(hint.rectTransform, 24, 84, -24, 44);
                hint.alignment = TextAlignmentOptions.Center;
            }
            _listScroll.verticalNormalizedPosition = 1;
            _previewRoutine = StartCoroutine(LoadPreviews(rows));
        }

        private Image CreateObjectItem(CatalogObjectData obj)
        {
            var row = Rect(_objectListContent, "Object_" + obj.id, Vector2.zero, Vector2.one);
            Background(row, Raised);
            _round?.Invoke(row.GetComponent<Image>());
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
            var preview = Rect(row, "PreviewFrame", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            preview.sizeDelta = new Vector2(76, 76); preview.anchoredPosition = new Vector2(50, 0);
            Background(preview, new Color(0.18f, 0.21f, 0.28f));
            _round?.Invoke(preview.GetComponent<Image>());
            var placeholder = Label(preview, "Placeholder", L("Preview", "预览"), 11, Muted);
            placeholder.alignment = TextAlignmentOptions.Center;
            var thumb = Rect(preview, "Thumbnail", Vector2.zero, Vector2.one);
            thumb.offsetMin = new Vector2(5, 5); thumb.offsetMax = new Vector2(-5, -5);
            var image = thumb.gameObject.AddComponent<Image>();
            image.preserveAspect = true; image.raycastTarget = false; image.enabled = false;

            var name = Label(row, "Name", obj.display_name, 18, Ink);
            Top(name.rectTransform, 108, 10, -116, 28);
            var category = Label(row, "Category", (obj.category ?? L("Custom", "自定义")).ToUpperInvariant(), 10, Accent);
            Top(category.rectTransform, 108, 41, -116, 18);
            string detail = !string.IsNullOrWhiteSpace(obj.description) ? obj.description
                : obj.tags != null && obj.tags.Length > 0 ? string.Join(" · ", obj.tags)
                : L("Ready to use in your sandtray", "可在沙盘中使用");
            var description = Label(row, "Description", detail, 12, Muted);
            Top(description.rectTransform, 108, 65, -116, 22);
            var delete = Button(row, "Delete", L("Delete", "删除"),
                new Color(0.23f, 0.15f, 0.18f), new Color(0.91f, 0.57f, 0.59f),
                () => StartCoroutine(DeleteObjectCoroutine(_selectedCatalogId, obj)));
            Top(delete.GetComponent<RectTransform>(), -94, 32, -14, 36, 1, 1);
            return image;
        }

        private IEnumerator LoadPreviews(List<KeyValuePair<CatalogObjectData, Image>> rows)
        {
            foreach (var row in rows)
            {
                var obj = row.Key;
                var image = row.Value;
                if (image == null || string.IsNullOrEmpty(obj.thumbnail_url)) continue;
                string key = obj.id + "|" + obj.thumbnail_url;
                if (!_previews.TryGetValue(key, out var item))
                {
                    item = new NetworkCatalogItem { id = obj.id, thumbnail_url = obj.thumbnail_url, display_name = obj.display_name };
                    _previews.Add(key, item);
                }
                yield return NetworkCatalogLoader.PreloadThumbnail(item);
                if (image == null) continue;
                if (item.ThumbnailSprite != null)
                {
                    image.sprite = item.ThumbnailSprite; image.enabled = true;
                    var placeholder = image.transform.parent.Find("Placeholder");
                    if (placeholder != null) placeholder.gameObject.SetActive(false);
                }
                else
                {
                    var placeholder = image.transform.parent.Find("Placeholder")?.GetComponent<TMP_Text>();
                    if (placeholder != null) placeholder.text = L("No preview", "无预览");
                }
            }
        }

        private void OnUploadObject()
        {
            if (string.IsNullOrEmpty(_selectedCatalogId)) { OnCreateCatalog(); return; }
            _uploadUI.Show(_selectedCatalogId);
        }

        private void OnCreateCatalog()
        {
            if (_dialog != null) return;
            var overlay = Rect(transform, "NewCollectionDialog", Vector2.zero, Vector2.one);
            _dialog = overlay.gameObject;
            Background(overlay, new Color(0.02f, 0.03f, 0.05f, 0.8f));
            var card = Rect(overlay, "Card", new Vector2(0.25f, 0.35f), new Vector2(0.75f, 0.65f));
            Background(card, Raised); _round?.Invoke(card.GetComponent<Image>());
            var title = Label(card, "Title", L("New collection", "新建物件集"), 23, Ink);
            Top(title.rectTransform, 24, 18, -24, 36);
            var input = Input(card, "Name", L("Give your collection a name", "为物件集命名"));
            Top(input.GetComponent<RectTransform>(), 24, 68, -24, 44);
            input.characterLimit = 200;
            var cancel = Button(card, "Cancel", L("Cancel", "取消"), Surface, Muted, () => Destroy(_dialog));
            Bottom(cancel.GetComponent<RectTransform>(), 24, 18, -8, 40, 0, 0.5f);
            var create = Button(card, "Create", L("Create", "创建"), Accent, Surface, () =>
            {
                string name = input.text.Trim();
                if (name.Length == 0) { input.ActivateInputField(); return; }
                Destroy(_dialog);
                StartCoroutine(CreateCatalogCoroutine(name));
            });
            Bottom(create.GetComponent<RectTransform>(), 8, 18, -24, 40, 0.5f, 1);
            input.ActivateInputField();
        }

        private IEnumerator CreateCatalogCoroutine(string name)
        {
            SetLoadingState(true);
            UpdateStatus(L("Creating collection…", "正在创建物件集…"));
            var result = BackendClient.Instance.CreateCatalog(name);
            yield return result;
            if (result.Success)
            {
                _selectedCatalogId = result.CatalogId;
                LoadUserCatalogs();
            }
            else { SetLoadingState(false); UpdateStatus(result.Error, true); }
        }

        private IEnumerator DeleteObjectCoroutine(string catalogId, CatalogObjectData obj)
        {
            SetLoadingState(true);
            var result = BackendClient.Instance.DeleteCatalogObject(catalogId, obj.id);
            yield return result;
            if (result.Success) LoadUserCatalogs();
            else { SetLoadingState(false); UpdateStatus(result.Error, true); }
        }

        private void UpdateStatus(string text, bool error = false)
        {
            _statusText.text = text;
            _statusText.color = error ? new Color(0.96f, 0.59f, 0.57f) : Muted;
        }

        private void SetLoadingState(bool loading)
        {
            _loading = loading;
            _refreshButton.interactable = !loading;
            _createCatalogButton.interactable = !loading;
            _uploadObjectButton.interactable = !loading && _userCatalogs.Count > 0;
            _catalogDropdown.interactable = !loading && _userCatalogs.Count > 0;
            _visibilityDropdown.interactable = !loading && !_updatingVisibility && _userCatalogs.Count > 0;
        }

        private RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer; go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void Top(RectTransform rt, float left, float top, float right, float height, float minX = 0, float maxX = 1)
        {
            rt.anchorMin = new Vector2(minX, 1); rt.anchorMax = new Vector2(maxX, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height); rt.offsetMax = new Vector2(right, -top);
        }

        private static void Bottom(RectTransform rt, float left, float bottom, float right, float height, float minX, float maxX)
        {
            rt.anchorMin = new Vector2(minX, 0); rt.anchorMax = new Vector2(maxX, 0);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(right, bottom + height);
        }

        private Image Background(RectTransform rt, Color color)
        {
            var image = rt.gameObject.AddComponent<Image>(); image.color = color; return image;
        }

        private TMP_Text Label(Transform parent, string name, string value, float size, Color color)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font; text.text = value; text.fontSize = size; text.color = color;
            text.enableAutoSizing = true; text.fontSizeMin = size * 0.85f; text.fontSizeMax = size;
            text.alignment = TextAlignmentOptions.Left; text.raycastTarget = false;
            text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private Button Button(Transform parent, string name, string title, Color background, Color foreground, Action onClick)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            var image = Background(rt, background); _round?.Invoke(image);
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f); button.colors = colors;
            button.onClick.AddListener(() => onClick());
            var label = Label(rt, "Label", title, 14, foreground);
            label.rectTransform.offsetMin = new Vector2(8, 0); label.rectTransform.offsetMax = new Vector2(-8, 0);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private TMP_InputField Input(Transform parent, string name, string placeholder)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            var image = Background(rt, Raised); _round?.Invoke(image);
            var viewport = Rect(rt, "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(14, 6); viewport.offsetMax = new Vector2(-14, -6);
            viewport.gameObject.AddComponent<RectMask2D>();
            var value = Label(viewport, "Text", "", 14, Ink);
            var hint = Label(viewport, "Placeholder", placeholder, 14, Muted);
            var input = rt.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image; input.textViewport = viewport;
            input.textComponent = (TextMeshProUGUI)value; input.placeholder = hint;
            return input;
        }

        private TMP_Dropdown Dropdown(Transform parent, string name, float popupHeight)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            var image = Background(rt, Raised); _round?.Invoke(image);
            var dropdown = rt.gameObject.AddComponent<TMP_Dropdown>(); dropdown.targetGraphic = image;
            var caption = Label(rt, "Label", "", 15, Ink);
            caption.rectTransform.offsetMin = new Vector2(14, 0); caption.rectTransform.offsetMax = new Vector2(-38, 0);
            // Draw the chevron with UI geometry so it works with every language font.
            var arrow = Rect(rt, "Arrow", new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            arrow.sizeDelta = new Vector2(14, 10); arrow.anchoredPosition = new Vector2(-23, 0);
            for (int i = 0; i < 2; i++)
            {
                var stroke = Rect(arrow, "Stroke", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                stroke.sizeDelta = new Vector2(8, 1.5f);
                stroke.anchoredPosition = new Vector2(i == 0 ? -2.6f : 2.6f, 0);
                stroke.localRotation = Quaternion.Euler(0, 0, i == 0 ? -45 : 45);
                Background(stroke, Muted).raycastTarget = false;
            }
            var template = Rect(rt, "Template", Vector2.zero, Vector2.right);
            template.pivot = new Vector2(0.5f, 1);
            template.anchoredPosition = new Vector2(0, -6);
            template.sizeDelta = new Vector2(0, popupHeight);
            Background(template, Raised);
            var viewport = Rect(template, "Viewport", Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(0.5f, 1);
            // TMP measures item offsets against this rect. Zero height clips the first/last rows.
            content.sizeDelta = new Vector2(0, 44);
            var item = Rect(content, "Item", new Vector2(0, 0.5f), new Vector2(1, 0.5f));
            item.sizeDelta = new Vector2(0, 44);
            var itemBg = Background(item, Raised);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = itemBg;
            var colors = toggle.colors;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colors.selectedColor = new Color(1.25f, 1.25f, 1.25f);
            toggle.colors = colors;
            var mark = Rect(item, "SelectionMark", Vector2.zero, new Vector2(0, 1));
            mark.sizeDelta = new Vector2(3, 0);
            toggle.graphic = Background(mark, Accent);
            var itemText = Label(item, "ItemLabel", "", 14, Ink);
            itemText.rectTransform.offsetMin = new Vector2(14, 0); itemText.rectTransform.offsetMax = new Vector2(-18, 0);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            AddScrollbar(template, scroll);
            dropdown.template = template; dropdown.captionText = (TextMeshProUGUI)caption;
            dropdown.itemText = (TextMeshProUGUI)itemText;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private void AddScrollbar(RectTransform parent, ScrollRect scroll)
        {
            var track = Rect(parent, "Scrollbar", new Vector2(1, 0), Vector2.one);
            track.offsetMin = new Vector2(-5, 4); track.offsetMax = new Vector2(-1, -4);
            Background(track, new Color(0.1f, 0.12f, 0.17f));
            var handle = Rect(track, "Handle", Vector2.zero, Vector2.one);
            var graphic = Background(handle, new Color(0.37f, 0.42f, 0.51f));
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop; bar.handleRect = handle; bar.targetGraphic = graphic;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
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
