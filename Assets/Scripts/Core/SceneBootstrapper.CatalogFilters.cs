using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Objects;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private RectTransform _catalogCategoryContent;
        private NetworkCatalogItem[] _catalogFilterItems = Array.Empty<NetworkCatalogItem>();
        private string _selectedCatalogCategory;
        private string _catalogSearchQuery = "";
        private TMP_InputField _catalogSearchInput;
        public static bool CatalogMatchesSearch(NetworkCatalogItem item, string query)
        {
            if (item == null) return false;
            query = (query ?? "").Trim();
            if (query.Length == 0) return true;
            bool Matches(string value) => !string.IsNullOrEmpty(value) &&
                value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            if (Matches(item.display_name) || Matches(item.category) || Matches(CatalogCategoryLabel(CatalogCategory(item.category)))) return true;
            if (item.tags != null)
                foreach (var tag in item.tags) if (Matches(tag)) return true;
            return false;
        }
        public static string CatalogCategory(string category) => string.IsNullOrWhiteSpace(category)
            ? "general" : category.Trim().ToLowerInvariant();

        public static string CatalogCategoryLabel(string category)
        {
            if (category == null) return Localization.Text("All", "全部");
            var name = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(category.Replace('_', ' '));
            var key = "cat." + name;
            var translated = Localization.Get(key);
            return translated == key ? Localization.Text(name, name) : translated;
        }

        private void CreateCatalogCategoryStrip()
        {
            var search = ClientInput(_catalogPanel.transform, _catalogSearchQuery,
                Localization.Text("Search by name or tag", "按名称或标签搜索"), 0, 0, 1, 1, 100);
            search.name = "CatalogSearch";
            _catalogSearchInput = search;
            var searchRect = (RectTransform)search.transform;
            // Share the header row with the close button. The left inset leaves
            // room for that button while the right edge still uses the drawer
            // width, giving the catalog more vertical space for results.
            searchRect.anchorMin = new Vector2(0, 1); searchRect.anchorMax = new Vector2(1, 1);
            searchRect.pivot = new Vector2(.5f, 1);
            searchRect.offsetMin = new Vector2(40, -36);
            searchRect.offsetMax = new Vector2(-6, -4);
            search.lineType = TMP_InputField.LineType.SingleLine;
            search.textComponent.richText = false;
            search.onValueChanged.AddListener(value =>
            {
                _catalogSearchQuery = value;
                if (_catalogContentGo != null && _catalogPanel.activeInHierarchy) RefreshCatalogFilter();
            });
            var strip = new GameObject("CatalogCategories", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            strip.transform.SetParent(_catalogPanel.transform, false);
            var rect = (RectTransform)strip.transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -42);
            rect.sizeDelta = new Vector2(0, 34);
            strip.GetComponent<Image>().color = new Color(.1f, .1f, .1f, .95f);
            var content = new GameObject("CategoryContent", typeof(RectTransform));
            content.transform.SetParent(strip.transform, false);
            _catalogCategoryContent = (RectTransform)content.transform;
            _catalogCategoryContent.anchorMin = Vector2.zero;
            _catalogCategoryContent.anchorMax = new Vector2(0, 1);
            _catalogCategoryContent.pivot = new Vector2(0, .5f);
            var scroll = strip.GetComponent<ScrollRect>();
            scroll.viewport = rect; scroll.content = _catalogCategoryContent;
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            BuildCatalogCategoryChips();
        }

        private void BuildCatalogCategoryChips()
        {
            if (_catalogCategoryContent == null) return;
            var categories = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var item in _catalogFilterItems)
                if (item != null) categories.Add(CatalogCategory(item.category));
            if (_selectedCatalogCategory != null && !categories.Contains(_selectedCatalogCategory)) _selectedCatalogCategory = null;
            for (int i = _catalogCategoryContent.childCount - 1; i >= 0; i--)
            {
                var child = _catalogCategoryContent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            var ordered = new List<string> { null };
            foreach (var preferred in new[] { "people", "animals" })
                if (categories.Remove(preferred)) ordered.Add(preferred);
            ordered.AddRange(categories);
            float x = 4;
            foreach (var category in ordered)
            {
                string label = CatalogCategoryLabel(category);
                var go = new GameObject("Category_" + (category ?? "all"), typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_catalogCategoryContent, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, .5f);
                var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(go.transform, false);
                var text = textGo.GetComponent<TextMeshProUGUI>();
                text.font = GetUIFont(); text.fontSize = 12; text.text = label;
                text.enableWordWrapping = false; text.richText = false;
                text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                var tr = (RectTransform)textGo.transform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(10, 0); tr.offsetMax = new Vector2(-10, 0);
                float width = Mathf.Max(52, text.GetPreferredValues(label).x + 24);
                rect.anchoredPosition = new Vector2(x, 0); rect.sizeDelta = new Vector2(width, -4);
                go.GetComponent<Image>().color = category == _selectedCatalogCategory
                    ? new Color(.22f,.42f,.52f) : new Color(.18f,.19f,.22f);
                ApplyRoundedCorners(go.GetComponent<Image>());
                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _selectedCatalogCategory = category;
                    BuildCatalogCategoryChips();
                    RefreshCatalogFilter();
                });
                x += width + 6;
            }
            _catalogCategoryContent.sizeDelta = new Vector2(x, 0);
        }

        private void RefreshCatalogLanguage()
        {
            if (_catalogSearchInput != null && _catalogSearchInput.placeholder is TMP_Text placeholder)
                placeholder.text = Localization.Text("Search by name or tag", "按名称或标签搜索");
            BuildCatalogCategoryChips();
            if (_catalogPanel != null && _catalogPanel.activeInHierarchy && _catalogContentGo != null)
                RefreshCatalogFilter();
        }
    }
}
