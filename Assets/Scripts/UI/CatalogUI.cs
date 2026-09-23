using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.UI
{
    public class CatalogUI : MonoBehaviour
    {
        [SerializeField] private ObjectCatalog _catalog;
        [SerializeField] private Transform _categoryTabContainer;
        [SerializeField] private Transform _itemGridContainer;
        [SerializeField] private GameObject _categoryTabPrefab;
        [SerializeField] private GameObject _catalogItemPrefab;

        private ObjectCategory _activeCategory;

        private void Start()
        {
            if (_catalog == null) return;
            BuildCategoryTabs();
            ShowCategory(ObjectCategory.People);
        }

        private void BuildCategoryTabs()
        {
            if (_categoryTabContainer == null || _categoryTabPrefab == null) return;

            foreach (var cat in _catalog.GetAvailableCategories())
            {
                var go = Instantiate(_categoryTabPrefab, _categoryTabContainer);
                var text = go.GetComponentInChildren<TMP_Text>();
                if (text) text.text = Localization.Get("cat." + cat.ToString());

                var btn = go.GetComponent<Button>();
                if (btn)
                {
                    var category = cat;
                    btn.onClick.AddListener(() => ShowCategory(category));
                }
            }
        }

        public void ShowCategory(ObjectCategory category)
        {
            _activeCategory = category;

            // Clear existing items
            if (_itemGridContainer == null) return;
            foreach (Transform child in _itemGridContainer)
                Destroy(child.gameObject);

            // Populate
            foreach (var obj in _catalog.GetByCategory(category))
            {
                if (_catalogItemPrefab == null) break;

                var go = Instantiate(_catalogItemPrefab, _itemGridContainer);
                var text = go.GetComponentInChildren<TMP_Text>();
                if (text) text.text = obj.DisplayName;

                var img = go.transform.Find("Thumbnail")?.GetComponent<Image>();
                if (img && obj.Thumbnail)
                    img.sprite = obj.Thumbnail;

                var btn = go.GetComponent<Button>();
                if (btn)
                {
                    var data = obj;
                    btn.onClick.AddListener(() => StartCoroutine(SelectBuiltInAfterAccess(data)));
                }
            }
        }

        private System.Collections.IEnumerator SelectBuiltInAfterAccess(SandplayObject data)
        {
            if (data == null) yield break;
            bool done = false;
            bool allowed = false;
            if (BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn)
            {
                allowed = true;
                done = true;
            }
            else
            {
                BackendClient.Instance.FetchAccessSnapshot(snapshot =>
                {
                    allowed = AccessPolicy.Evaluate(snapshot, "objects.builtin.read", checkUsage: false) == AccessDecision.Allowed;
                    done = true;
                }, _ => done = true, force: true);
            }
            while (!done) yield return null;
            if (allowed)
                EventBus.Publish(new CatalogObjectSelectedEvent { ObjectData = data });
            else
                Debug.LogWarning("[Catalog] Built-in objects are not included in the current plan.");
        }
    }
}
