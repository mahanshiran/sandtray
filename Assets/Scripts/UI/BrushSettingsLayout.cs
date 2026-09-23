using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>Safe-area-relative brush controls; wide touch targets, no custom shader dependency.</summary>
    public sealed class BrushSettingsLayout : MonoBehaviour
    {
        private Slider[] sliders;
        private TextMeshProUGUI[] labels;
        private TextMeshProUGUI[] values;
        private float left;
        private Vector2 previousSize;
        private Sprite roundedSprite;

        public void Initialize(Slider radius, Slider strength, TextMeshProUGUI radiusLabel,
            TextMeshProUGUI strengthLabel, float leftInset)
        {
            sliders = new[] { radius, strength };
            labels = new[] { radiusLabel, strengthLabel };
            values = new TextMeshProUGUI[2];
            left = leftInset;
            roundedSprite = MakeRoundedSprite();
            var panelImage = GetComponent<Image>();
            panelImage.sprite = roundedSprite;
            panelImage.type = Image.Type.Sliced;
            for (int i = 0; i < 2; i++)
            {
                foreach (var graphic in sliders[i].GetComponentsInChildren<Image>(true))
                    graphic.material = null;
                // The whole 44-unit slider row accepts mouse/touch, not only its thin track.
                var hit = sliders[i].gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                hit.raycastTarget = true;
                var background = sliders[i].transform.Find("Background").GetComponent<Image>();
                background.color = new Color(.28f, .34f, .38f, 1);
                SetTrack(background.rectTransform);
                SetTrack((RectTransform)sliders[i].fillRect.parent);
                sliders[i].fillRect.GetComponent<Image>().color = new Color(.35f, .78f, .76f, 1);
                var handle = sliders[i].handleRect;
                // Slider drives the handle's vertical anchors to 0..1 on every value change.
                // Fix the parent height instead, so the handle never stretches into a bar.
                var handleArea = (RectTransform)handle.parent;
                handleArea.anchorMin = new Vector2(0, .5f);
                handleArea.anchorMax = new Vector2(1, .5f);
                handleArea.sizeDelta = new Vector2(-16, 16);
                handleArea.anchoredPosition = Vector2.zero;
                handle.anchorMin = new Vector2(0, 0);
                handle.anchorMax = new Vector2(0, 1);
                handle.sizeDelta = new Vector2(16, 0);
                handle.anchoredPosition = Vector2.zero;
                handle.GetComponent<Image>().sprite = roundedSprite;
                labels[i].enableAutoSizing = true;
                labels[i].fontSizeMin = 9;
                labels[i].fontSizeMax = 11;
                labels[i].enableWordWrapping = false;
                labels[i].raycastTarget = false;
                var go = new GameObject("Value", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                values[i] = go.AddComponent<TextMeshProUGUI>();
                values[i].font = labels[i].font;
                values[i].fontSize = 11;
                values[i].alignment = TextAlignmentOptions.TopRight;
                values[i].color = new Color(.55f, .88f, .86f);
                values[i].raycastTarget = false;
                int index = i;
                sliders[i].onValueChanged.AddListener(v => UpdateValue(index));
                UpdateValue(i);
            }
            Refresh();
        }

        private void UpdateValue(int i)
        {
            values[i].text = i == 0 ? sliders[i].value.ToString("0.0") : Mathf.RoundToInt(sliders[i].value * 100) + "%";
        }

        private void LateUpdate()
        {
            if (sliders != null && ((RectTransform)transform.parent).rect.size != previousSize) Refresh();
        }

        private void Refresh()
        {
            previousSize = ((RectTransform)transform.parent).rect.size;
            float width = Mathf.Min(340, Mathf.Max(1, previousSize.x - left - 8));
            bool stacked = width < 280;
            Place((RectTransform)transform, left, 48, width, stacked ? 116 : 60);
            float column = stacked ? width : width / 2;
            for (int i = 0; i < 2; i++)
            {
                float x = stacked ? 0 : i * column;
                float y = stacked ? i * 56 : 0;
                Place(labels[i].rectTransform, x + 12, y + 7, column - 60, 18);
                Place(values[i].rectTransform, x + column - 46, y + 7, 34, 18);
                Place((RectTransform)sliders[i].transform, x + 12, y + 16, column - 24, 44);
            }
        }

        private static void SetTrack(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, .5f);
            rt.anchorMax = new Vector2(1, .5f);
            rt.sizeDelta = new Vector2(-16, 3);
            rt.anchoredPosition = Vector2.zero;
        }

        private static Sprite MakeRoundedSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1, 1, 1,
                        Mathf.Clamp01(16 - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(16, 16))));
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0,
                SpriteMeshType.FullRect, Vector4.one * 15);
        }

        private void OnDestroy()
        {
            if (roundedSprite == null) return;
            Destroy(roundedSprite.texture);
            Destroy(roundedSprite);
        }

        private static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
