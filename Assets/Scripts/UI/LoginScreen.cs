using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;

namespace Sandplay.UI
{
    /// <summary>
    /// Full-screen login / register overlay.
    /// Calls BackendClient and notifies the caller when auth state changes.
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        public System.Action OnAuthChanged;   // fired after login, register, or sign-out

        private TMP_InputField _emailField;
        private TMP_InputField _passwordField;
        private TMP_InputField _nameField;        // register only
        private TextMeshProUGUI _statusText;
        private Button _submitBtn;
        private TextMeshProUGUI _submitLabel;
        private Button _toggleModeBtn;
        private TextMeshProUGUI _toggleLabel;
        private GameObject _nameRow;
        private GameObject _typeRow;
        private bool _isRegisterMode;
        private string _selectedType = "normal";

        private const int RoundedSpriteSize = 64;
        private const int RoundedSpriteRadius = 18;
        private const int RoundedSpriteBorder = 3;
        private static Sprite _roundedFillSprite;
        private static Sprite _roundedBorderSprite;

        // ── Build UI ──────────────────────────────────────────────────────────

        public void Initialize()
        {
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.10f, 0.97f);

            // If already logged in, show the "logged in" state instead
            if (BackendClient.Instance != null && BackendClient.Instance.IsLoggedIn)
            {
                BuildLoggedInView();
                return;
            }

            BuildAuthForm();
        }

        // ── Logged-in view ────────────────────────────────────────────────────

        private void BuildLoggedInView()
        {
            var font = GetFont();

            var card = MakePanel("Card", transform,
                new Vector2(0.2f, 0.35f), new Vector2(0.8f, 0.65f),
                new Color(0.12f, 0.12f, 0.18f, 1f));
            AddRoundedBorder(card, new Color(0.78f, 0.68f, 0.46f, 0.55f));

            var titleTxt = MakeText("Title", card.transform, font,
                Localization.Get("login.title"), 24, FontStyles.Bold, Color.white,
                new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.95f));

            var info = BackendClient.Instance;
            MakeText("Info", card.transform, font,
                Localization.Get("login.logged_in_as", info.UserName + "\n" + info.UserEmail),
                15, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f),
                new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.72f));

            var signOutBtn = MakeButton("SignOutBtn", card.transform,
                Localization.Get("login.sign_out"), font,
                new Color(0.7f, 0.2f, 0.2f, 1f),
                new Vector2(0.2f, 0.10f), new Vector2(0.8f, 0.32f));
            signOutBtn.onClick.AddListener(() =>
            {
                BackendClient.Instance?.SignOut();
                OnAuthChanged?.Invoke();
                Destroy(gameObject);
            });

            var closeBtn = MakeButton("CloseBtn", card.transform,
                Localization.Get("dialog.cancel"), font,
                new Color(0.25f, 0.25f, 0.3f, 1f),
                new Vector2(0.75f, 0.75f), new Vector2(0.98f, 0.95f));
            closeBtn.onClick.AddListener(() => Destroy(gameObject));
        }

        // ── Auth form (login + register) ──────────────────────────────────────

        private void BuildAuthForm()
        {
            var font = GetFont();

            var card = MakePanel("Card", transform,
                new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.88f),
                new Color(0.12f, 0.12f, 0.18f, 1f));
            AddRoundedBorder(card, new Color(0.78f, 0.68f, 0.46f, 0.55f));

            // Title
            MakeText("Title", card.transform, font,
                Localization.Get("login.title"), 26, FontStyles.Bold,
                new Color(0.9f, 0.82f, 0.6f),
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));

            // Close button (top-right)
            var closeBtn = MakeButton("CloseBtn", card.transform,
                "✕", font, new Color(0.3f, 0.3f, 0.35f, 1f),
                new Vector2(0.85f, 0.88f), new Vector2(0.98f, 0.98f));
            closeBtn.onClick.AddListener(() => Destroy(gameObject));

            // ── Name row (register mode only) ──
            _nameRow = MakePanel("NameRow", card.transform,
                new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.84f),
                Color.clear);
            _nameField = MakeInputField("NameField", _nameRow.transform, font,
                Localization.Get("login.name"), false,
                new Vector2(0f, 0.08f), new Vector2(1f, 0.92f));
            _nameRow.SetActive(false);

            // ── Email ──
            var emailRow = MakePanel("EmailRow", card.transform,
                new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.73f), Color.clear);
            _emailField = MakeInputField("EmailField", emailRow.transform, font,
                Localization.Get("login.email"), false,
                new Vector2(0f, 0.08f), new Vector2(1f, 0.92f));
            _emailField.contentType = TMP_InputField.ContentType.EmailAddress;

            // ── Password ──
            var passRow = MakePanel("PassRow", card.transform,
                new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.61f), Color.clear);
            _passwordField = MakeInputField("PassField", passRow.transform, font,
                Localization.Get("login.password"), true,
                new Vector2(0f, 0.08f), new Vector2(1f, 0.92f));

            // ── User type row (register mode only) ──
            _typeRow = MakePanel("TypeRow", card.transform,
                new Vector2(0.05f, 0.37f), new Vector2(0.95f, 0.49f), Color.clear);
            BuildTypeToggle(_typeRow.transform, font);
            _typeRow.SetActive(false);

            // ── Status text ──
            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(card.transform, false);
            _statusText = statusGo.AddComponent<TextMeshProUGUI>();
            _statusText.font = font;
            _statusText.fontSize = 13;
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.color = new Color(1f, 0.5f, 0.3f);
            var statusRT = statusGo.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0.05f, 0.28f);
            statusRT.anchorMax = new Vector2(0.95f, 0.37f);
            statusRT.offsetMin = Vector2.zero;
            statusRT.offsetMax = Vector2.zero;

            // ── Submit button ──
            _submitBtn = MakeButton("SubmitBtn", card.transform,
                Localization.Get("login.sign_in"), font,
                new Color(0.2f, 0.5f, 0.8f, 1f),
                new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.27f));
            _submitLabel = _submitBtn.GetComponentInChildren<TextMeshProUGUI>();
            _submitBtn.onClick.AddListener(OnSubmit);

            // ── Mode toggle ──
            _toggleModeBtn = MakeButton("ToggleBtn", card.transform,
                Localization.Get("login.register"), font,
                new Color(0.18f, 0.18f, 0.22f, 1f),
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.15f));
            _toggleLabel = _toggleModeBtn.GetComponentInChildren<TextMeshProUGUI>();
            _toggleModeBtn.onClick.AddListener(ToggleMode);
        }

        private void BuildTypeToggle(Transform parent, TMP_FontAsset font)
        {
            var normalBtn = MakeButton("Normal", parent,
                Localization.Get("login.type_normal"), font,
                new Color(0.2f, 0.5f, 0.3f, 1f),
                new Vector2(0f, 0.08f), new Vector2(0.48f, 0.92f));
            normalBtn.onClick.AddListener(() =>
            {
                _selectedType = "normal";
                normalBtn.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.3f, 1f);
            });

            var psychBtn = MakeButton("Psychologist", parent,
                Localization.Get("login.type_psychologist"), font,
                new Color(0.25f, 0.25f, 0.3f, 1f),
                new Vector2(0.52f, 0.08f), new Vector2(1f, 0.92f));
            psychBtn.onClick.AddListener(() =>
            {
                _selectedType = "psychologist";
                psychBtn.GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.6f, 1f);
                normalBtn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 1f);
            });
        }

        // ── Interaction ───────────────────────────────────────────────────────

        private void ToggleMode()
        {
            _isRegisterMode = !_isRegisterMode;
            _nameRow.SetActive(_isRegisterMode);
            _typeRow.SetActive(_isRegisterMode);
            _statusText.text = "";

            if (_isRegisterMode)
            {
                _submitLabel.text = Localization.Get("login.register");
                _toggleLabel.text = Localization.Get("login.sign_in");
                // Shift email/password rows down to make room for name row
                ShiftRow(_emailField.transform.parent.GetComponent<RectTransform>(),
                    new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.73f));
            }
            else
            {
                _submitLabel.text = Localization.Get("login.sign_in");
                _toggleLabel.text = Localization.Get("login.register");
                ShiftRow(_emailField.transform.parent.GetComponent<RectTransform>(),
                    new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.73f));
            }
        }

        private static void ShiftRow(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void OnSubmit()
        {
            string email = _emailField?.text?.Trim() ?? "";
            string password = _passwordField?.text ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetStatus(Localization.Get("login.error", "Please fill all fields."), true);
                return;
            }

            _submitBtn.interactable = false;

            if (_isRegisterMode)
            {
                string name = _nameField?.text?.Trim() ?? "";
                if (string.IsNullOrEmpty(name))
                {
                    SetStatus(Localization.Get("login.error", "Name is required."), true);
                    _submitBtn.interactable = true;
                    return;
                }
                SetStatus(Localization.Get("login.registering"), false);
                BackendClient.Instance.Register(email, name, password, _selectedType,
                    OnLoginSuccess, OnLoginError);
            }
            else
            {
                SetStatus(Localization.Get("login.signing_in"), false);
                BackendClient.Instance.Login(email, password,
                    OnLoginSuccess, OnLoginError);
            }
        }

        private void OnLoginSuccess(string userName)
        {
            SetStatus(Localization.Get("login.success", userName), false);
            _statusText.color = new Color(0.3f, 1f, 0.5f);
            OnAuthChanged?.Invoke();
            Invoke(nameof(SelfDestroy), 0.8f);
        }

        private void OnLoginError(string error)
        {
            SetStatus(Localization.Get("login.error", error), true);
            if (_submitBtn != null) _submitBtn.interactable = true;
        }

        private void SelfDestroy() => Destroy(gameObject);

        private void SetStatus(string msg, bool isError)
        {
            if (_statusText == null) return;
            _statusText.text = msg;
            _statusText.color = isError
                ? new Color(1f, 0.4f, 0.3f)
                : new Color(0.7f, 0.7f, 0.7f);
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private static TMP_FontAsset GetFont()
        {
            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        private static GameObject MakePanel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            ApplyRoundedImage(img, color);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, TMP_FontAsset font,
            string text, int size, FontStyles style, Color color,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text; txt.font = font; txt.fontSize = size;
            txt.fontStyle = style; txt.color = color;
            txt.alignment = TextAlignmentOptions.Center;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return txt;
        }

        private static Button MakeButton(string name, Transform parent, string label, TMP_FontAsset font,
            Color bgColor, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            ApplyRoundedImage(img, bgColor);
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.25f;
            colors.pressedColor = bgColor * 0.75f;
            btn.colors = colors;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var txt = lblGo.AddComponent<TextMeshProUGUI>();
            txt.text = label; txt.font = font; txt.fontSize = 15;
            txt.alignment = TextAlignmentOptions.Center; txt.color = Color.white;
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            return btn;
        }

        private static TMP_InputField MakeInputField(string name, Transform parent, TMP_FontAsset font,
            string placeholder, bool isPassword, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            var fillColor = new Color(0.16f, 0.16f, 0.20f, 1f);
            img.color = fillColor;
            ApplyRoundedImage(img, fillColor);
            AddRoundedBorder(go, new Color(0.78f, 0.68f, 0.46f, 0.42f));
            var field = go.AddComponent<TMP_InputField>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Placeholder
            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var phTxt = phGo.AddComponent<TextMeshProUGUI>();
            phTxt.text = placeholder; phTxt.font = font; phTxt.fontSize = 14;
            phTxt.color = new Color(0.45f, 0.45f, 0.45f);
            phTxt.alignment = TextAlignmentOptions.Left;
            var phRT = phGo.GetComponent<RectTransform>();
            phRT.anchorMin = new Vector2(0.06f, 0); phRT.anchorMax = new Vector2(0.96f, 1f);
            phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;

            // Text
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var inputTxt = txtGo.AddComponent<TextMeshProUGUI>();
            inputTxt.font = font; inputTxt.fontSize = 14;
            inputTxt.color = Color.white;
            inputTxt.alignment = TextAlignmentOptions.Left;
            var txtRT = txtGo.GetComponent<RectTransform>();
            txtRT.anchorMin = new Vector2(0.06f, 0); txtRT.anchorMax = new Vector2(0.96f, 1f);
            txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;

            field.targetGraphic = img;
            field.placeholder = phTxt;
            field.textComponent = inputTxt;
            if (isPassword)
                field.inputType = TMP_InputField.InputType.Password;

            return field;
        }

        private static void ApplyRoundedImage(Image image, Color color)
        {
            image.sprite = GetRoundedFillSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static void AddRoundedBorder(GameObject target, Color borderColor)
        {
            var borderGo = new GameObject("RoundedBorder");
            borderGo.transform.SetParent(target.transform, false);
            borderGo.transform.SetAsLastSibling();

            var border = borderGo.AddComponent<Image>();
            border.sprite = GetRoundedBorderSprite();
            border.type = Image.Type.Sliced;
            border.color = borderColor;
            border.raycastTarget = false;

            var rt = borderGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite GetRoundedFillSprite()
        {
            if (_roundedFillSprite == null)
                _roundedFillSprite = CreateRoundedSprite(false);
            return _roundedFillSprite;
        }

        private static Sprite GetRoundedBorderSprite()
        {
            if (_roundedBorderSprite == null)
                _roundedBorderSprite = CreateRoundedSprite(true);
            return _roundedBorderSprite;
        }

        private static Sprite CreateRoundedSprite(bool borderOnly)
        {
            var texture = new Texture2D(RoundedSpriteSize, RoundedSpriteSize, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            float radius = RoundedSpriteRadius;
            float innerRadius = Mathf.Max(0f, radius - RoundedSpriteBorder);

            for (int y = 0; y < RoundedSpriteSize; y++)
            {
                for (int x = 0; x < RoundedSpriteSize; x++)
                {
                    float outerDistance = RoundedRectDistance(x + 0.5f, y + 0.5f, radius);
                    float outerAlpha = Mathf.Clamp01(0.5f - outerDistance);
                    float alpha = outerAlpha;

                    if (borderOnly)
                    {
                        float innerDistance = RoundedRectDistance(
                            x + 0.5f, y + 0.5f,
                            innerRadius,
                            RoundedSpriteBorder,
                            RoundedSpriteBorder,
                            RoundedSpriteSize - RoundedSpriteBorder,
                            RoundedSpriteSize - RoundedSpriteBorder);
                        float innerAlpha = Mathf.Clamp01(0.5f - innerDistance);
                        alpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);

            var rect = new Rect(0, 0, RoundedSpriteSize, RoundedSpriteSize);
            var pivot = new Vector2(0.5f, 0.5f);
            var border = new Vector4(RoundedSpriteRadius, RoundedSpriteRadius, RoundedSpriteRadius, RoundedSpriteRadius);
            var sprite = Sprite.Create(texture, rect, pivot, 100f, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static float RoundedRectDistance(float x, float y, float radius)
        {
            return RoundedRectDistance(x, y, radius, 0f, 0f, RoundedSpriteSize, RoundedSpriteSize);
        }

        private static float RoundedRectDistance(float x, float y, float radius,
            float minX, float minY, float maxX, float maxY)
        {
            float centerX = Mathf.Clamp(x, minX + radius, maxX - radius);
            float centerY = Mathf.Clamp(y, minY + radius, maxY - radius);
            float dx = x - centerX;
            float dy = y - centerY;
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }
    }
}
