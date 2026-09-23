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
        private TMP_Text _title;
        private bool _catalogsPage = true;
        private GridLayoutGroup _grid;
        private ObjectLibraryGrid _responsiveGrid;
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

        private static bool Light => PlayerPrefs.GetInt("sandplay_home_theme",0)==0;
        private static Color Surface => Light ? Color.white : new Color(.07f,.125f,.15f);
        private static Color Raised => Light ? new Color(.975f,.973f,.963f) : new Color(.045f,.09f,.11f);
        private static Color Ink => Light ? new Color(.025f,.075f,.145f) : new Color(.96f,.97f,.97f);
        private static Color Muted => Light ? new Color(.37f,.43f,.52f) : new Color(.65f,.73f,.78f);
        private static Color Accent => new Color(.025f,.43f,.40f);
        private static Color Border => Light ? new Color(.77f,.80f,.83f,.7f) : new Color(.25f,.36f,.40f);
        private static readonly string[] VisibilityValues = { "private", "clinic", "public", "marketplace" };
        private static string L(string en, string zh) => Localization.Text(en, zh);

        public void BuildRuntimeUI(TMP_FontAsset font, Action<Image> round)
        {
            _font = font;
            _round = round;
            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            gameObject.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.065f, Light ? .48f : .72f);

            var card = Rect(transform, "Library", Vector2.zero, Vector2.one);
            Background(card, Surface);
            var back = Button(card, "Back", "", Raised, Ink, () =>
            {
                if (_catalogsPage) Hide(); else ShowCatalogs();
            });
            Top(back.GetComponent<RectTransform>(), 24, 18, 68, 44, 0, 0);
            RecordSearchGlyph.StyleBackButton(back, Ink);
            _title = Label(card, "Title", L("Object library", "物件库"), 26, Ink);
            Top(_title.rectTransform, 260, 18, -260, 44);
            _title.fontStyle = FontStyles.Bold;
            _title.alignment = TextAlignmentOptions.Center;
            _createCatalogButton = Button(card, "NewCatalog", L("+ New catalog", "+ 新建目录"), Accent, Color.white, OnCreateCatalog);
            Top(_createCatalogButton.GetComponent<RectTransform>(), -230, 18, -28, 44, 1, 1);
            _uploadObjectButton = Button(card, "Upload", L("+ Upload object", "+ 上传物件"), Accent, Color.white, OnUploadObject);
            Top(_uploadObjectButton.GetComponent<RectTransform>(), -230, 18, -28, 44, 1, 1);
            var tools = Rect(card, "SearchTools", Vector2.zero, Vector2.one);
            Top(tools, 28, 78, -28, 44);
            _search = Input(tools, "Search", L("Search catalogs", "搜索目录"));
            Top(_search.GetComponent<RectTransform>(), 0, 0, -120, 44);
            _refreshButton = Button(tools, "Refresh", L("Refresh", "刷新"), Raised, Muted, LoadUserCatalogs);
            Top(_refreshButton.GetComponent<RectTransform>(), -108, 0, 0, 44, 1, 1);
            // Retain the existing visibility adapter for explicit sharing dialogs.
            _catalogDropdown = Dropdown(card, "CatalogAdapter", 220);
            _catalogDropdown.gameObject.SetActive(false);
            _visibilityDropdown = Dropdown(card, "VisibilityAdapter", 220);
            _visibilityDropdown.gameObject.SetActive(false);

            var viewport = Rect(card, "ObjectViewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(28, 58);
            viewport.offsetMax = new Vector2(-28, -138);
            Background(viewport, Raised);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "ObjectRows", new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout=content.gameObject.AddComponent<GridLayoutGroup>();
            layout.spacing=new Vector2(14,14);layout.padding=new RectOffset(14,22,14,14);
            layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=4;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            _grid = layout;
            _responsiveGrid = content.gameObject.AddComponent<ObjectLibraryGrid>();
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
            _uploadUI.BuildRuntimeUI(uploadPanel.gameObject, font, round);
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
            _search.onValueChanged.AddListener(_ => { if (_catalogsPage) PopulateCatalogs(); else PopulateObjectList(); });
            EventBus.Subscribe<CatalogRefreshRequestedEvent>(OnCatalogRefreshRequested);
            _initialized = true;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowCatalogs();
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
            UpdateStatus(L("Loading catalogs…", "正在加载目录…"));
            var backend = BackendClient.Instance;
            if (backend == null || !backend.IsLoggedIn)
            {
                UpdateStatus(L("Sign in to view your catalogs.", "登录后查看你的目录。"), true);
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
            _catalogDropdown.AddOptions(options.Count > 0 ? options : new List<string> { L("No catalogs yet", "还没有目录") });
            int selected = _userCatalogs.FindIndex(c => c.id == _selectedCatalogId);
            selected = Mathf.Max(0, selected);
            _catalogDropdown.SetValueWithoutNotify(selected);
            SetLoadingState(false);
            if (_catalogsPage) { PopulateCatalogs(); UpdateStatus(""); }
            else if (_userCatalogs.Count > 0 && _userCatalogs.Exists(c => c.id == _selectedCatalogId)) OnCatalogSelected(selected);
            else
            {
                _selectedCatalogId = null;
                _currentObjects.Clear();
                ShowCatalogs();
                UpdateStatus(L("Create a catalog to get started.", "创建目录，开始添加物件。"));
            }
        }

        private void OnCatalogSelected(int index)
        {
            if (index < 0 || index >= _userCatalogs.Count) return;
            var selectedCatalog = _userCatalogs[index];
            _catalogsPage = false;
            _responsiveGrid.enabled = true;
            _createCatalogButton.gameObject.SetActive(false);
            _uploadObjectButton.gameObject.SetActive(selectedCatalog.can_edit_objects);
            _title.text = selectedCatalog.is_default ? L("Platform Default", "平台默认") : selectedCatalog.name;
            ((TMP_Text)_search.placeholder).text = L("Search objects by name or category", "搜索名称或分类");
            _selectedCatalogId = selectedCatalog.id;
            ShowSelectedVisibility(selectedCatalog.visibility);
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
            if (_objectListContent == null || _catalogsPage) return;
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
            _countText.text = string.Format(L("{0} objects", "{0} 个物件"), rows.Count);
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
                    : L("Upload a model and a preview image to build your catalog.", "上传模型和预览图片，建立你的目录。"), 14, Muted);
                Top(hint.rectTransform, 24, 84, -24, 44);
                hint.alignment = TextAlignmentOptions.Center;
            }
            _listScroll.verticalNormalizedPosition = 1;
            _previewRoutine = StartCoroutine(LoadPreviews(rows));
        }

        private Image CreateObjectItem(CatalogObjectData obj)
        {
            var row = Rect(_objectListContent, "Object_" + obj.id, Vector2.zero, Vector2.one);
            Background(row, Surface);
            _round?.Invoke(row.GetComponent<Image>());
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
            var preview = Rect(row, "PreviewFrame", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            preview.anchorMin=new Vector2(.05f,.46f);preview.anchorMax=new Vector2(.95f,.96f);preview.offsetMin=preview.offsetMax=Vector2.zero;
            Background(preview, Raised);
            _round?.Invoke(preview.GetComponent<Image>());
            var placeholder = Label(preview, "Placeholder", L("Preview", "预览"), 11, Muted);
            placeholder.alignment = TextAlignmentOptions.Center;
            var thumb = Rect(preview, "Thumbnail", Vector2.zero, Vector2.one);
            thumb.offsetMin = new Vector2(5, 5); thumb.offsetMax = new Vector2(-5, -5);
            var image = thumb.gameObject.AddComponent<Image>();
            image.preserveAspect = true; image.raycastTarget = false; image.enabled = false;

            var name = Label(row, "Name", obj.display_name, 18, Ink);
            Bottom(name.rectTransform,12,112,-12,28,0,1);name.fontStyle=FontStyles.Bold;
            var category = Label(row, "Category", (obj.category ?? L("Custom", "自定义")).ToUpperInvariant(), 10, Accent);
            Bottom(category.rectTransform,12,89,-12,20,0,1);
            string detail = !string.IsNullOrWhiteSpace(obj.description) ? obj.description
                : obj.tags != null && obj.tags.Length > 0 ? string.Join(" · ", obj.tags)
                : L("Ready to use in your sandtray", "可在沙盘中使用");
            var description = Label(row, "Description", detail, 12, Muted);
            Bottom(description.rectTransform,12,55,-12,24,0,1);
            var availability = Button(
                row, "Availability",
                obj.enabled ? L("Disable", "停用") : L("Enable", "启用"),
                obj.enabled ? Raised : Accent,
                obj.enabled ? Ink : Color.white,
                () => SetObjectEnabled(obj, !obj.enabled));
            Bottom(availability.GetComponent<RectTransform>(), 12, 9, 12, 34, 0, .48f);
            var catalog = _userCatalogs.Find(c => c.id == _selectedCatalogId);
            if (catalog != null && catalog.can_edit_objects)
            {
                var more = Button(row, "More", "", Raised, Ink, () => ShowObjectMenu(obj));
                Bottom(more.GetComponent<RectTransform>(), -48, 9, -12, 34, 1, 1);
                var dots = Rect(more.transform, "Dots", new Vector2(.2f,.2f), new Vector2(.8f,.8f)).gameObject.AddComponent<RecordSearchGlyph>();
                dots.Icon = RecordSearchGlyph.Kind.More; dots.color = Ink; dots.raycastTarget = false;
            }
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

        private void LateUpdate()
        {
            if (!_catalogsPage || _grid == null) return;
            float width = Mathf.Max(100, ((RectTransform)_objectListContent.parent).rect.width - 36);
            if (!Mathf.Approximately(_grid.cellSize.x, width)) _grid.cellSize = new Vector2(width, 88);
        }

        private void ShowCatalogs()
        {
            _catalogsPage = true;
            ++_objectsRequest;
            _selectedCatalogId = null;
            _search.SetTextWithoutNotify("");
            ((TMP_Text)_search.placeholder).text = L("Search catalogs", "搜索目录");
            _title.text = L("Object library", "物件库");
            _createCatalogButton.gameObject.SetActive(true);
            _uploadObjectButton.gameObject.SetActive(false);
            SetLoadingState(false);
            PopulateCatalogs();
        }

        private void PopulateCatalogs()
        {
            if (_previewRoutine != null) StopCoroutine(_previewRoutine);
            foreach (Transform child in _objectListContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _responsiveGrid.enabled = false;
            _grid.constraintCount = 1;
            _grid.cellSize = new Vector2(Mathf.Max(100, ((RectTransform)_objectListContent.parent).rect.width - 36), 96);
            int count = 0;
            foreach (var catalog in _userCatalogs)
            {
                if ((catalog.name ?? "").IndexOf(_search.text.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                count++;
                var row = Button(_objectListContent, "Catalog_" + catalog.id, "", Surface, Ink,
                    () => OnCatalogSelected(_userCatalogs.IndexOf(catalog)));
                var glyph = Rect(row.transform, "CatalogIcon", new Vector2(.02f,.22f), new Vector2(.09f,.78f)).gameObject.AddComponent<RecordSearchGlyph>();
                glyph.Icon = RecordSearchGlyph.Kind.Grid; glyph.color = Accent; glyph.raycastTarget = false;
                string displayName = catalog.is_default ? L("Platform Default", "平台默认") : catalog.name;
                var name = Label(row.transform, "Name", displayName, 18, Ink);
                Top(name.rectTransform, 0, 14, -12, 30, .12f, .50f);
                string state = catalog.enabled ? L("Enabled", "已启用") : L("Disabled", "已停用");
                var info = Label(row.transform, "Details", catalog.object_count + L(" objects · ", " 个物件 · ") + state, 12, Muted);
                Top(info.rectTransform, 0, 51, -12, 24, .12f, .50f);

                var actions = new List<ActionButton>();
                actions.Add(new ActionButton {
                    Name = "Enabled", Title = catalog.enabled ? L("Disable", "停用") : L("Enable", "启用"),
                    Color = catalog.enabled ? Raised : Accent,
                    TextColor = catalog.enabled ? Ink : Color.white,
                    Action = () => SetCatalogEnabled(catalog, !catalog.enabled)
                });
                if (catalog.can_rename) actions.Add(new ActionButton {
                    Name = "Rename", Title = L("Rename", "重命名"), Color = Raised, TextColor = Ink,
                    Action = () => EditName(catalog.name, value => Mutate("catalogs/" + catalog.id + "/", "PATCH", "name", value))
                });
                if (catalog.can_share) actions.Add(new ActionButton {
                    Name = "Share", Title = L("Share", "共享"), Color = Raised, TextColor = Ink,
                    Action = () => ShowCatalogSharing(catalog)
                });
                if (catalog.can_delete) actions.Add(new ActionButton {
                    Name = "Delete", Title = L("Delete", "删除"), Color = Raised, TextColor = new Color(.80f,.15f,.16f),
                    Action = () => ConfirmDelete(catalog.name, () => Mutate("catalogs/" + catalog.id + "/", "DELETE"))
                });
                float start = actions.Count == 1 ? .78f : .50f;
                float width = (1f - start) / actions.Count;
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    var action = actions[actionIndex];
                    var actionButton = Button(row.transform, action.Name, action.Title, action.Color, action.TextColor, action.Action);
                    Top(actionButton.GetComponent<RectTransform>(), 4, 27, actionIndex == actions.Count - 1 ? -12 : -4, 42,
                        start + width * actionIndex, start + width * (actionIndex + 1));
                }

            }
            _countText.text = count + L(" catalogs", " 个目录");
            if (count == 0)
                Label(_objectListContent, "Empty", L("No catalogs found. Create a catalog to add objects.", "没有目录。新建目录以添加物件。"), 16, Muted);
        }

        private Transform Menu(string title)
        {
            if (_dialog != null) Destroy(_dialog);
            var overlay = Rect(transform, "CatalogDialog", Vector2.zero, Vector2.one);
            _dialog = overlay.gameObject;
            Background(overlay, new Color(0,0,0,.65f));
            var card = Rect(overlay, "Card", new Vector2(.12f,.06f), new Vector2(.88f,.94f));
            _round?.Invoke(Background(card, Surface));
            var heading = Label(card, "Title", title, 22, Ink);
            Top(heading.rectTransform, 24, 16, -76, 42);
            var close = Button(card, "Close", "×", Raised, Ink, () => Destroy(_dialog));
            Top(close.GetComponent<RectTransform>(), -64, 16, -20, 40, 1, 1);
            return card;
        }

        private void Option(Transform card, string title, int index, Action action)
        {
            var button = Button(card, "Action", title, Raised, Ink, () => { Destroy(_dialog); action(); });
            Top(button.GetComponent<RectTransform>(), 24, 76 + index * 52, -24, 44);
        }

        private void EditName(string current, Action<string> save)
        {
            var card = Menu(L("Rename", "重命名"));
            var input = Input(card, "Name", L("Name", "名称"));
            Top(input.GetComponent<RectTransform>(),24,76,-24,44);
            input.characterLimit = 200; input.text = current;
            Option(card, L("Save", "保存"), 1, () => { var value = input.text.Trim(); if (value.Length > 0) save(value); });
        }

        private void ConfirmDelete(string name, Action remove)
        {
            var card = Menu(L("Delete ", "删除 ") + name);
            var note = Label(card, "Warning", L("Remove from your library? Existing saved boards keep their object references.", "从物件库删除？已保存沙盘将保留物件引用。"), 15, Muted);
            note.enableWordWrapping = true;
            Top(note.rectTransform,24,76,-24,66);
            Option(card, L("Delete", "删除"), 2, remove);
        }

        private void ShowCatalogSharing(UserCatalog catalog)
        {
            var share = Menu(L("Sharing — choose who can browse this catalog", "共享 — 选择谁可以浏览此目录"));
            Option(share,L("Private — only me", "私密 — 仅自己"),0,()=>Mutate("catalogs/"+catalog.id+"/","PATCH","visibility","private"));
            Option(share,L("Clinic — linked clients", "诊所 — 关联来访者"),1,()=>Mutate("catalogs/"+catalog.id+"/","PATCH","visibility","clinic"));
            Option(share,L("Public — everyone", "公开 — 所有人"),2,()=>Mutate("catalogs/"+catalog.id+"/","PATCH","visibility","public"));
        }

        private void ShowObjectMenu(CatalogObjectData obj)
        {
            var catalog = _userCatalogs.Find(c => c.id == _selectedCatalogId);
            if (catalog == null || !catalog.can_edit_objects) return;
            string path = "catalogs/" + _selectedCatalogId + "/objects/" + obj.id + "/";
            var card = Menu(obj.display_name);
            Option(card,L("Rename", "重命名"),0,()=>EditName(obj.display_name,value=>Mutate(path,"PATCH","display_name",value)));
            Option(card,L("Move to catalog", "移动到目录"),1,()=>
            {
                var move = Menu(L("Move to catalog", "移动到目录"));
                var dropdown = Dropdown(move,"Destination",200);
                Top(dropdown.GetComponent<RectTransform>(),24,76,-24,44);
                var destinations = _userCatalogs.FindAll(c=>c.id!=_selectedCatalogId && c.can_edit_objects);
                dropdown.AddOptions(destinations.ConvertAll(c=>c.name));
                if (destinations.Count > 0) Option(move,L("Move", "移动"),1,()=>Mutate(path+"move/","POST","catalog_id",destinations[dropdown.value].id));
                else UpdateStatus(L("Create another catalog first.", "请先新建另一个目录。"));
            });
            Option(card,L("Delete", "删除"),2,()=>ConfirmDelete(obj.display_name,()=>Mutate(path,"DELETE")));
        }

        private void Mutate(string path, string method, string field = null, string value = null)
        {
            SetLoadingState(true);
            StartCoroutine(BackendClient.Instance.MutateCatalogCoroutine(path, method, field, value,
                () => EventBus.Publish(new CatalogRefreshRequestedEvent()),
                error => { SetLoadingState(false); UpdateStatus(error,true); }));
        }

        private void SetCatalogEnabled(UserCatalog catalog, bool enabled)
        {
            SetLoadingState(true);
            StartCoroutine(BackendClient.Instance.MutateCatalogCoroutine(
                "catalogs/" + catalog.id + "/enabled/", "PATCH", "enabled",
                enabled.ToString().ToLowerInvariant(),
                () =>
                {
                    // Disabling only removes the catalog from placement. Keep its
                    // downloaded assets because saved boards may still reference them.
                    NetworkCatalogCache.SetCatalogEnabled(catalog.id, enabled);
                    EventBus.Publish(new CatalogRefreshRequestedEvent());
                },
                error => { SetLoadingState(false); UpdateStatus(error, true); }));
        }

        private void SetObjectEnabled(CatalogObjectData obj, bool enabled)
        {
            SetLoadingState(true);
            string path = "catalogs/" + _selectedCatalogId + "/objects/" + obj.id + "/enabled/";
            StartCoroutine(BackendClient.Instance.MutateCatalogCoroutine(
                path, "PATCH", "enabled", enabled.ToString().ToLowerInvariant(),
                () =>
                {
                    NetworkCatalogCache.SetObjectEnabled(obj.id, enabled);
                    EventBus.Publish(new CatalogRefreshRequestedEvent());
                },
                error => { SetLoadingState(false); UpdateStatus(error, true); }));
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
            var title = Label(card, "Title", L("New catalog", "新建目录"), 23, Ink);
            Top(title.rectTransform, 24, 18, -24, 36);
            var input = Input(card, "Name", L("Give your catalog a name", "为目录命名"));
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
            UpdateStatus(L("Creating catalog…", "正在创建目录…"));
            var result = BackendClient.Instance.CreateCatalog(name);
            yield return result;
            if (result.Success)
            {
                _selectedCatalogId = result.CatalogId;
                _catalogsPage = false;
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
            var selected = _userCatalogs.Find(c => c.id == _selectedCatalogId);
            _uploadObjectButton.interactable = !loading && selected != null && selected.can_edit_objects;
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
            var image = rt.gameObject.AddComponent<Image>(); image.color = color;
            var outline=rt.gameObject.AddComponent<Outline>();outline.effectColor=Border;outline.effectDistance=new Vector2(1,-1);
            return image;
        }

        private TMP_Text Label(Transform parent, string name, string value, float size, Color color)
        {
            if (ColorsMatch(color, Muted)) size = Mathf.Max(6f, size - 2f);
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font; text.text = value; text.fontSize = size; text.color = color;
            text.enableAutoSizing = true; text.fontSizeMin = size * 0.85f; text.fontSizeMax = size;
            text.alignment = TextAlignmentOptions.Left; text.raycastTarget = false;
            text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static bool ColorsMatch(Color left, Color right)
        {
            const float tolerance = .002f;
            return Mathf.Abs(left.r - right.r) < tolerance && Mathf.Abs(left.g - right.g) < tolerance &&
                   Mathf.Abs(left.b - right.b) < tolerance && Mathf.Abs(left.a - right.a) < tolerance;
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
            var popup=Background(template, Surface);_round?.Invoke(popup);
            var shadow=template.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.14f);shadow.effectDistance=new Vector2(0,-4);
            var viewport = Rect(template, "Viewport", Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(0.5f, 1);
            // TMP measures item offsets against this rect. Zero height clips the first/last rows.
            content.sizeDelta = new Vector2(0, 44);
            var item = Rect(content, "Item", new Vector2(0, 0.5f), new Vector2(1, 0.5f));
            item.sizeDelta = new Vector2(0, 44);
            var itemBg = Background(item, Surface);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = itemBg;
            var colors = toggle.colors;
            colors.highlightedColor = Light?new Color(.86f,.95f,.93f):new Color(.7f,.9f,.9f);
            colors.selectedColor = colors.highlightedColor;
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
        public bool enabled = true;
        public bool is_default;
        public bool can_rename;
        public bool can_share;
        public bool can_delete;
        public bool can_edit_objects;
    }

    internal sealed class ActionButton
    {
        public string Name;
        public string Title;
        public Color Color;
        public Color TextColor;
        public Action Action;
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
        public bool enabled = true;
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
    public sealed class ObjectLibraryGrid : MonoBehaviour
    {
        void LateUpdate()
        {
            var grid=GetComponent<GridLayoutGroup>();float width=((RectTransform)transform.parent).rect.width-36;
            if(width<=0)return;
            int columns=Mathf.Max(1,Mathf.FloorToInt((width+14)/194));
            grid.constraintCount=columns;grid.cellSize=new Vector2(Mathf.Min(220,(width-14*(columns-1))/columns),310);
        }
    }

}
