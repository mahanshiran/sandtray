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
    public partial class LoginScreen : MonoBehaviour
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
        private bool _isRegisterMode;
        private Button _googleBtn, _appleBtn, _openBrowserBtn;
        private RectTransform _authCard, _accountTypeCard, _nameRowRt, _emailRowRt, _passRowRt;
        private TextMeshProUGUI _headingText, _footerPrompt;
        private GameObject _nameLabel, _emailLabel, _passwordLabel, _divider;
        private Button _passwordToggle;
        private bool _passwordVisible;
        private bool _googleEnabled, _appleEnabled, _socialBusy;
        private bool _emailBusy;
        private string _pendingRegistrationEmail, _pendingRegistrationPassword;

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
            bg.color = new Color(0.015f, 0.025f, 0.035f, IsLightTheme ? 0.54f : 0.72f);

            // If already logged in, show the "logged in" state instead
            if (BackendClient.Instance != null && BackendClient.Instance.IsLoggedIn)
            {
                if (!BackendClient.Instance.AccountTypeSelected)
                {
                    ShowAccountTypeOnboarding(BackendClient.Instance.UserName);
                    return;
                }
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
                AuthText("login.title"), 24, FontStyles.Bold, Color.white,
                new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.95f));

            var info = BackendClient.Instance;
            MakeText("Info", card.transform, font,
                AuthText("login.logged_in_as", info.UserName + "\n" + info.UserEmail),
                15, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f),
                new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.72f));

            var signOutBtn = MakeButton("SignOutBtn", card.transform,
                AuthText("login.sign_out"), font,
                new Color(0.7f, 0.2f, 0.2f, 1f),
                new Vector2(0.2f, 0.10f), new Vector2(0.8f, 0.32f));
            signOutBtn.onClick.AddListener(() =>
            {
                BackendClient.Instance?.SignOut();
                OnAuthChanged?.Invoke();
                Destroy(gameObject);
            });

            var closeBtn = MakeButton("CloseBtn", card.transform,
                AuthText("dialog.cancel"), font,
                new Color(0.25f, 0.25f, 0.3f, 1f),
                new Vector2(0.75f, 0.75f), new Vector2(0.98f, 0.95f));
            closeBtn.onClick.AddListener(() => Destroy(gameObject));
        }

        // ── Auth form (login + register) ──────────────────────────────────────

        private void BuildAuthForm()
        {
            var font = GetFont();

            var card = MakePanel("Card", transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Surface);
            _authCard = card.GetComponent<RectTransform>();
            _authCard.sizeDelta = new Vector2(556f, 660f);
            AddRoundedBorder(card, Border);
            var shadow = card.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, IsLightTheme ? .24f : .5f);
            shadow.effectDistance = new Vector2(0f, -8f);

            var brand = MakeText("Brand", card.transform, font, "Sandtray", 22, FontStyles.Bold, Ink,
                new Vector2(.07f, .90f), new Vector2(.55f, .97f));
            brand.alignment = TextAlignmentOptions.Left;

            var closeBtn = MakeButton("CloseBtn", card.transform, "×", font, Color.clear,
                new Vector2(.89f, .90f), new Vector2(.96f, .97f));
            closeBtn.GetComponentInChildren<TextMeshProUGUI>().color = Ink;
            closeBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 31;
            closeBtn.onClick.AddListener(() => Destroy(gameObject));

            _headingText = MakeText("Heading", card.transform, font, AuthText("login.welcome_back"),
                34, FontStyles.Bold, Ink, new Vector2(.07f, .81f), new Vector2(.93f, .89f));
            _headingText.alignment = TextAlignmentOptions.Left;
            _nameLabel = MakeFieldLabel("NameLabel", card.transform, font, AuthText("login.name"));
            _nameRow = MakePanel("NameRow", card.transform, Vector2.zero, Vector2.zero, Color.clear);
            _nameRowRt = _nameRow.GetComponent<RectTransform>();
            _nameField = MakeInputField("NameField", _nameRow.transform, font,
                AuthText("login.name_placeholder"), false, Vector2.zero, Vector2.one);

            _emailLabel = MakeFieldLabel("EmailLabel", card.transform, font, AuthText("login.email_address"));
            var emailRow = MakePanel("EmailRow", card.transform, Vector2.zero, Vector2.zero, Color.clear);
            _emailRowRt = emailRow.GetComponent<RectTransform>();
            _emailField = MakeInputField("EmailField", emailRow.transform, font,
                AuthText("login.email_placeholder"), false, Vector2.zero, Vector2.one);
            _emailField.contentType = TMP_InputField.ContentType.EmailAddress;

            _passwordLabel = MakeFieldLabel("PasswordLabel", card.transform, font, AuthText("login.password"));
            var passRow = MakePanel("PassRow", card.transform, Vector2.zero, Vector2.zero, Color.clear);
            _passRowRt = passRow.GetComponent<RectTransform>();
            _passwordField = MakeInputField("PassField", passRow.transform, font,
                AuthText("login.password_placeholder"), true, Vector2.zero, Vector2.one);
            var passTextRt = _passwordField.textComponent.rectTransform;
            passTextRt.anchorMax = new Vector2(.86f, 1f);
            _passwordField.placeholder.GetComponent<RectTransform>().anchorMax = new Vector2(.86f, 1f);
            _passwordToggle = MakeButton("PasswordToggle", passRow.transform, "◉", font, Color.clear,
                new Vector2(.87f, .10f), new Vector2(.98f, .90f));
            _passwordToggle.GetComponentInChildren<TextMeshProUGUI>().color = Ink;
            _passwordToggle.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
            _passwordToggle.onClick.AddListener(TogglePasswordVisibility);

            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(card.transform, false);
            _statusText = statusGo.AddComponent<TextMeshProUGUI>();
            _statusText.font = font;
            _statusText.fontSize = 11;
            _statusText.alignment = TextAlignmentOptions.Left;
            _statusText.color = Muted;

            _submitBtn = MakeButton("SubmitBtn", card.transform,
                AuthText("login.sign_in"), font, Primary, Vector2.zero, Vector2.zero);
            _submitLabel = _submitBtn.GetComponentInChildren<TextMeshProUGUI>();
            _submitLabel.fontStyle = FontStyles.Bold;
            _submitBtn.onClick.AddListener(OnSubmit);

            _divider = new GameObject("Divider", typeof(RectTransform));
            _divider.transform.SetParent(card.transform, false);
            AddDividerLine(_divider.transform, .0f, .43f);
            AddDividerLine(_divider.transform, .57f, 1f);
            var orText = MakeText("Or", _divider.transform, font, AuthText("login.or"), 14,
                FontStyles.Normal, Muted, new Vector2(.43f, 0f), new Vector2(.57f, 1f));

            _googleBtn = MakeSocialButton("GoogleSignIn", card.transform, "Google",
                AuthText("social.continue_google"), Field, Ink, Border);
            _appleBtn = MakeSocialButton("AppleSignIn", card.transform, "Apple",
                AuthText("social.continue_apple"), Color.black, Color.white, Color.black);

            _footerPrompt = MakeText("FooterPrompt", card.transform, font, AuthText("login.have_account"),
                14, FontStyles.Normal, Ink, Vector2.zero, Vector2.zero);
            _footerPrompt.alignment = TextAlignmentOptions.Right;
            _toggleModeBtn = MakeButton("ToggleBtn", card.transform, AuthText("login.create_account"),
                font, Color.clear, Vector2.zero, Vector2.zero);
            _toggleLabel = _toggleModeBtn.GetComponentInChildren<TextMeshProUGUI>();
            _toggleLabel.color = Primary;
            _toggleLabel.fontStyle = FontStyles.Bold;
            _toggleModeBtn.onClick.AddListener(ToggleMode);
            _openBrowserBtn = MakeButton("OpenSocialBrowser", card.transform, AuthText("social.open"), font,
                Primary, new Vector2(.07f,.10f), new Vector2(.93f,.205f));
            _openBrowserBtn.gameObject.SetActive(false);

            _forgotPassword = MakeButton("ForgotPassword", card.transform, AuthText("login.forgot_password"), font,
                Color.clear, new Vector2(.60f,.557f), new Vector2(.93f,.600f));
            var forgotLabel = _forgotPassword.GetComponentInChildren<TMP_Text>();
            forgotLabel.fontSize = 12;
            forgotLabel.color = Primary;
            forgotLabel.alignment = TextAlignmentOptions.Right;
            _forgotPassword.onClick.AddListener(() => { if (!_emailBusy && !_socialBusy) ShowEmailFlow(true); });
            ApplyAuthModeLayout();
            _googleBtn.interactable = _appleBtn.interactable = false;
            _googleBtn.onClick.AddListener(() => StartSocial("google"));
            _appleBtn.onClick.AddListener(() => StartSocial("apple"));
            BackendClient.Instance.FetchSocialProviders(providers =>
            {
                if (this == null) return;
                _googleEnabled = providers.google; _appleEnabled = providers.apple;
                if (!_socialBusy && !_emailBusy) SetSocialBusy(false);
                if (!_googleEnabled && !_appleEnabled) SetStatus(AuthText("social.unavailable"), false);
            });
        }

        private Button MakeSocialButton(string name, Transform parent, string provider, string label,
            Color background, Color textColor, Color borderColor)
        {
            var button = MakeButton(name, parent, label, GetFont(), background, Vector2.zero, Vector2.zero);
            button.GetComponentInChildren<TextMeshProUGUI>().color = textColor;
            button.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            var labelRect = button.GetComponentInChildren<TextMeshProUGUI>().rectTransform;
            labelRect.offsetMin = new Vector2(58f, 0f);
            labelRect.offsetMax = new Vector2(-16f, 0f);
            AddRoundedBorder(button.gameObject, borderColor);
            var icon = new GameObject("ProviderIcon");
            icon.transform.SetParent(button.transform, false);
            var raw = icon.AddComponent<RawImage>();
            raw.texture = Resources.Load<Texture2D>("SocialLogin/" + provider);
            raw.color = Color.white;
            var rt = icon.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.07f, .5f);
            rt.sizeDelta = new Vector2(27f, 27f);
            return button;
        }

        private void SetSocialBusy(bool busy)
        {
            _socialBusy = busy;
            _googleBtn.gameObject.SetActive(!busy); _appleBtn.gameObject.SetActive(!busy);
            _googleBtn.interactable = _googleEnabled && !busy; _appleBtn.interactable = _appleEnabled && !busy;
            _submitBtn.interactable = !busy; _toggleModeBtn.interactable = !busy;
            if (_forgotPassword != null) _forgotPassword.interactable = !busy;
            if (!busy) _openBrowserBtn.gameObject.SetActive(false);
        }

        private void StartSocial(string provider)
        {
            if (_emailBusy || _socialBusy) return;
            SetSocialBusy(true);
            SetStatus(AuthText("social.preparing"), false);
            BackendClient.Instance.BeginSocialLogin(provider, url =>
            {
                if (this == null) return;
                SetStatus(AuthText("social.waiting"), false);
                _openBrowserBtn.gameObject.SetActive(true);
                _openBrowserBtn.onClick.RemoveAllListeners();
                // Keep a user-initiated fallback for platforms that block automatic opening.
                _openBrowserBtn.onClick.AddListener(() => Application.OpenURL(url));
                Application.OpenURL(url);
            }, name => { if (this != null) { _socialBusy = false; OnLoginSuccess(name); } },
               error => { if (this != null) { SetSocialBusy(false); OnLoginError(error); } });
        }

        private void OnDestroy()
        {
            if (_socialBusy) BackendClient.Instance.CancelSocialLogin();
            ClearPendingRegistrationCredentials();
        }

        // ── Interaction ───────────────────────────────────────────────────────

        private void ToggleMode()
        {
            if (_socialBusy) return;
            _isRegisterMode = !_isRegisterMode;
            _statusText.text = "";
            ApplyAuthModeLayout();
        }

        private void ApplyAuthModeLayout()
        {
            bool register = _isRegisterMode;
            _authCard.sizeDelta = new Vector2(556f, register ? 720f : 700f);
            _nameRow.SetActive(register);
            _nameLabel.SetActive(register);

            _headingText.text = AuthText(register ? "login.create_heading" : "login.welcome_back");
            _headingText.gameObject.SetActive(register);
            _authCard.Find("Brand").gameObject.SetActive(!register);
            if (register)
            {
                _headingText.text = AuthText("login.create_account");
                SetRect(_headingText.rectTransform, new Vector2(.07f,.90f), new Vector2(.86f,.97f));
                _headingText.fontSize = 28;
            }
            if (_forgotPassword != null) _forgotPassword.gameObject.SetActive(!register);
            _submitLabel.text = AuthText(register ? "login.register" : "login.sign_in");
            _footerPrompt.text = AuthText("login.have_account");
            _footerPrompt.gameObject.SetActive(register);
            _toggleLabel.text = AuthText(register ? "login.sign_in" : "login.create_account");

            if (register)
            {
                SetRect(_nameLabel.GetComponent<RectTransform>(), new Vector2(.07f,.81f), new Vector2(.93f,.845f));
                SetRect(_nameRowRt, new Vector2(.07f,.73f), new Vector2(.93f,.80f));
                SetRect(_emailLabel.GetComponent<RectTransform>(), new Vector2(.07f,.66f), new Vector2(.93f,.695f));
                SetRect(_emailRowRt, new Vector2(.07f,.58f), new Vector2(.93f,.65f));
                SetRect(_passwordLabel.GetComponent<RectTransform>(), new Vector2(.07f,.51f), new Vector2(.93f,.545f));
                SetRect(_passRowRt, new Vector2(.07f,.43f), new Vector2(.93f,.50f));
                SetRect(_statusText.rectTransform, new Vector2(.07f,.39f), new Vector2(.93f,.42f));
                SetRect(_submitBtn.GetComponent<RectTransform>(), new Vector2(.07f,.30f), new Vector2(.93f,.37f));
                SetRect(_divider.GetComponent<RectTransform>(), new Vector2(.07f,.255f), new Vector2(.93f,.28f));
                SetRect(_googleBtn.GetComponent<RectTransform>(), new Vector2(.07f,.17f), new Vector2(.93f,.235f));
                SetRect(_appleBtn.GetComponent<RectTransform>(), new Vector2(.07f,.09f), new Vector2(.93f,.155f));
                SetRect(_footerPrompt.rectTransform, new Vector2(.17f,.03f), new Vector2(.58f,.07f));
                SetRect(_toggleModeBtn.GetComponent<RectTransform>(), new Vector2(.58f,.03f), new Vector2(.83f,.07f));
            }
            else
            {
                SetRect(_emailLabel.GetComponent<RectTransform>(), new Vector2(.07f,.735f), new Vector2(.93f,.77f));
                SetRect(_emailRowRt, new Vector2(.07f,.645f), new Vector2(.93f,.72f));
                SetRect(_passwordLabel.GetComponent<RectTransform>(), new Vector2(.07f,.56f), new Vector2(.93f,.595f));
                SetRect(_forgotPassword.GetComponent<RectTransform>(), new Vector2(.60f,.56f), new Vector2(.93f,.595f));
                SetRect(_passRowRt, new Vector2(.07f,.47f), new Vector2(.93f,.545f));
                SetRect(_statusText.rectTransform, new Vector2(.07f,.425f), new Vector2(.93f,.455f));
                SetRect(_submitBtn.GetComponent<RectTransform>(), new Vector2(.07f,.325f), new Vector2(.93f,.40f));
                SetRect(_divider.GetComponent<RectTransform>(), new Vector2(.07f,.275f), new Vector2(.93f,.30f));
                SetRect(_googleBtn.GetComponent<RectTransform>(), new Vector2(.07f,.185f), new Vector2(.93f,.255f));
                SetRect(_appleBtn.GetComponent<RectTransform>(), new Vector2(.07f,.095f), new Vector2(.93f,.165f));
                SetRect(_toggleModeBtn.GetComponent<RectTransform>(), new Vector2(.34f,.025f), new Vector2(.66f,.065f));
            }
        }

        private void TogglePasswordVisibility()
        {
            _passwordVisible = !_passwordVisible;
            _passwordField.inputType = _passwordVisible
                ? TMP_InputField.InputType.Standard
                : TMP_InputField.InputType.Password;
            _passwordField.ForceLabelUpdate();
        }

        private void LateUpdate()
        {
            var available = ((RectTransform)transform).rect.size;
            if (available.x <= 0f || available.y <= 0f) return;
            ScaleCardToFit(_authCard, available);
            ScaleCardToFit(_accountTypeCard, available);
        }

        private static void ScaleCardToFit(RectTransform card, Vector2 available)
        {
            if (card == null || card.sizeDelta.x <= 0f || card.sizeDelta.y <= 0f) return;
            float scale = Mathf.Min(1f, (available.x - 32f) / card.sizeDelta.x,
                (available.y - 32f) / card.sizeDelta.y);
            card.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }

        private void OnSubmit()
        {
            if (_socialBusy || _emailBusy) return;
            string email = _emailField?.text?.Trim() ?? "";
            string password = _passwordField?.text ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetStatus(AuthText("login.error", "Please fill all fields."), true);
                return;
            }

            _submitBtn.interactable = false;
            _emailBusy = true;
            _googleBtn.interactable = _appleBtn.interactable = _toggleModeBtn.interactable = false;

            if (_isRegisterMode)
            {
                string name = _nameField?.text?.Trim() ?? "";
                if (string.IsNullOrEmpty(name))
                {
                    SetStatus(AuthText("login.error", "Name is required."), true);
                    _submitBtn.interactable = true;
                    _emailBusy = false; SetSocialBusy(false);
                    return;
                }
                SetStatus(AuthText("login.registering"), false);
                _pendingRegistrationEmail = email;
                _pendingRegistrationPassword = password;
                BackendClient.Instance.Register(email, name, password, "normal",
                    OnRegistrationCreated, error =>
                    {
                        ClearPendingRegistrationCredentials();
                        OnLoginError(error);
                    });
            }
            else
            {
                SetStatus(AuthText("login.signing_in"), false);
                BackendClient.Instance.Login(email, password,
                    OnLoginSuccess, OnLoginError);
            }
        }

        private void OnLoginSuccess(string userName)
        {
            if (this == null) return;
            ClearPendingRegistrationCredentials();
            if (BackendClient.Instance != null && !BackendClient.Instance.AccountTypeSelected)
            {
                ShowAccountTypeOnboarding(userName);
                return;
            }
            CompleteLoginSuccess(userName);
        }

        private void CompleteLoginSuccess(string userName)
        {
            if (this == null) return;
            SetStatus(AuthText("login.success", userName), false);
            if (_statusText != null) _statusText.color = new Color(0.3f, 1f, 0.5f);
            OnAuthChanged?.Invoke();
            Invoke(nameof(SelfDestroy), 0.8f);
        }

        private void OnLoginError(string error)
        {
            if (this == null) return;
            _emailBusy = false;
            if (_googleBtn != null) SetSocialBusy(false);
            SetStatus(AuthText("login.error", error), true);
            if (error != null && error.Contains("Verify your email")) ShowEmailFlow(false);
            if (_submitBtn != null) _submitBtn.interactable = true;
        }

        private void SelfDestroy() => Destroy(gameObject);

        private void ClearPendingRegistrationCredentials()
        {
            _pendingRegistrationEmail = null;
            _pendingRegistrationPassword = null;
        }

        private void SetStatus(string msg, bool isError)
        {
            if (_statusText == null) return;
            _statusText.text = msg;
            _statusText.color = isError
                ? new Color(1f, 0.4f, 0.3f)
                : Muted;
        }

        // Keep newly introduced copy readable when an existing Play session still
        // holds the localization dictionaries from before a script reload.
        private static string AuthText(string key, params object[] args)
        {
            string text = Localization.Get(key);
            if (text == key)
            {
                switch (key)
                {
                    case "login.welcome_back": text = Localization.Text("Welcome back", "欢迎回来"); break;
                    case "login.create_heading": text = Localization.Text("Create your account", "创建账户"); break;
                    case "login.email_address": text = Localization.Text("Email address", "邮箱地址"); break;
                    case "login.email_placeholder": text = "you@example.com"; break;
                    case "login.password_placeholder": text = Localization.Text("Enter your password", "输入密码"); break;
                    case "login.name_placeholder": text = Localization.Text("Enter your full name", "输入您的姓名"); break;
                    case "login.or": text = Localization.Text("or", "或"); break;
                    case "login.have_account": text = Localization.Text("Already have an account?", "已有账户？"); break;
                    case "login.create_account": text = Localization.Text("Create account", "创建账户"); break;
                    case "login.forgot_password": text = Localization.Text("Forgot password?", "忘记密码？"); break;
                    case "login.type_organization": text = Localization.Text("Organization", "机构"); break;
                    case "social.continue_google": text = Localization.Text("Continue with Google", "使用 Google 继续"); break;
                    case "social.continue_apple": text = Localization.Text("Continue with Apple", "使用 Apple 继续"); break;
                }
            }
            return args.Length == 0 ? text : string.Format(Localization.Culture, text, args);
        }

        private bool IsLightTheme => PlayerPrefs.GetInt("sandplay_home_theme", 0) == 0;
        private Color Surface => IsLightTheme ? new Color(.995f, .99f, .975f, 1f) : new Color(.055f, .09f, .105f, 1f);
        private Color Field => IsLightTheme ? Color.white : new Color(.085f, .13f, .145f, 1f);
        private Color Ink => IsLightTheme ? new Color(.025f, .055f, .14f, 1f) : new Color(.95f, .97f, .98f, 1f);
        private Color Muted => IsLightTheme ? new Color(.34f, .42f, .55f, 1f) : new Color(.65f, .72f, .79f, 1f);
        private Color Border => IsLightTheme ? new Color(.69f, .73f, .78f, .72f) : new Color(.25f, .35f, .39f, 1f);
        private Color Primary => new Color(.015f, .43f, .40f, 1f);

        // ── UI helpers ────────────────────────────────────────────────────────

        private GameObject MakeFieldLabel(string name, Transform parent, TMP_FontAsset font, string label)
        {
            var text = MakeText(name, parent, font, label, 14, FontStyles.Normal, Ink, Vector2.zero, Vector2.zero);
            text.alignment = TextAlignmentOptions.BottomLeft;
            return text.gameObject;
        }

        private void AddDividerLine(Transform parent, float minX, float maxX)
        {
            var line = new GameObject("Line");
            line.transform.SetParent(parent, false);
            var image = line.AddComponent<Image>();
            image.color = Border;
            var rt = line.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(minX, .49f);
            rt.anchorMax = new Vector2(maxX, .51f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

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

        private TextMeshProUGUI MakeText(string name, Transform parent, TMP_FontAsset font,
            string text, int size, FontStyles style, Color color,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            if (SameTextColor(color, Muted)) size = Mathf.Max(6, size - 2);
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

        private static bool SameTextColor(Color left, Color right)
        {
            const float tolerance = .002f;
            return Mathf.Abs(left.r - right.r) < tolerance && Mathf.Abs(left.g - right.g) < tolerance &&
                   Mathf.Abs(left.b - right.b) < tolerance && Mathf.Abs(left.a - right.a) < tolerance;
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

        private TMP_InputField MakeInputField(string name, Transform parent, TMP_FontAsset font,
            string placeholder, bool isPassword, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            var fillColor = Field;
            img.color = fillColor;
            ApplyRoundedImage(img, fillColor);
            AddRoundedBorder(go, Border);
            var field = go.AddComponent<TMP_InputField>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Placeholder
            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var phTxt = phGo.AddComponent<TextMeshProUGUI>();
            phTxt.text = placeholder; phTxt.font = font; phTxt.fontSize = 12;
            phTxt.color = Muted;
            phTxt.alignment = TextAlignmentOptions.Left;
            var phRT = phGo.GetComponent<RectTransform>();
            phRT.anchorMin = new Vector2(0.06f, 0); phRT.anchorMax = new Vector2(0.96f, 1f);
            phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;

            // Text
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var inputTxt = txtGo.AddComponent<TextMeshProUGUI>();
            inputTxt.font = font; inputTxt.fontSize = 14;
            inputTxt.color = Ink;
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
