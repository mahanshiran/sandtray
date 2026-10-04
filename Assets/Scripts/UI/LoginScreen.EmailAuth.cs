using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
namespace Sandplay.UI
{
    public partial class LoginScreen
    {
        Button _forgotPassword;
        GameObject _emailFlow;
        private void OnRegistrationCreated(string email)
        {
            if (this == null) return;
            _emailBusy = false; SetSocialBusy(false);
            _emailField.text = email;
            ShowEmailFlow(false);
        }

        private void CompleteEmailVerification(string email, System.Action close)
        {
            string verifiedEmail = email?.Trim() ?? "";
            string password = string.Equals(verifiedEmail, _pendingRegistrationEmail,
                System.StringComparison.OrdinalIgnoreCase) ? _pendingRegistrationPassword : null;

            _emailField.text = verifiedEmail;
            _passwordField.text = "";
            close();
            _isRegisterMode = false;
            ApplyAuthModeLayout();

            if (string.IsNullOrEmpty(password))
            {
                SetStatus(FriendsClient.Text("Email verified. Sign in with your password.",
                    "邮箱已验证，请使用密码登录。"), false);
                return;
            }

            _emailBusy = true;
            _submitBtn.interactable = false;
            _toggleModeBtn.interactable = false;
            _googleBtn.interactable = false;
            _appleBtn.interactable = false;
            if (_forgotPassword != null) _forgotPassword.interactable = false;
            SetStatus(FriendsClient.Text("Email verified. Signing you in…", "邮箱已验证，正在登录…"), false);
            BackendClient.Instance.Login(verifiedEmail, password,
                name =>
                {
                    ClearPendingRegistrationCredentials();
                    OnLoginSuccess(name);
                },
                error =>
                {
                    ClearPendingRegistrationCredentials();
                    OnLoginError(error);
                });
        }
        private void ShowEmailFlow(bool reset)
        {
            if (_emailFlow != null) return;
            var font = GetFont();
            var overlay = MakePanel("EmailSecurity", transform, Vector2.zero, Vector2.one, new Color(0,0,0,.72f));
            _emailFlow = overlay;
            var card = MakePanel("Card", overlay.transform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Surface);
            var rt = card.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(520, reset ? 620 : 470);
            var size = ((RectTransform)transform).rect.size;
            rt.localScale = Vector3.one * Mathf.Min(1, (size.x-24)/520, (size.y-24)/rt.sizeDelta.y);
            AddRoundedBorder(card, Border);
            string T(string en,string zh) => FriendsClient.Text(en,zh);
            MakeText("Title",card.transform,font,reset ? T("Reset password","重置密码"):T("Verify email","验证邮箱"),24,FontStyles.Bold,Ink,new Vector2(.07f,.87f),new Vector2(.85f,.97f));
            MakeText("Help",card.transform,font,T("Enter your email and request a 6-digit code. Codes expire in 10 minutes.","输入邮箱并获取 6 位验证码，验证码 10 分钟内有效。"),14,FontStyles.Normal,Muted,new Vector2(.07f,.75f),new Vector2(.93f,.86f));
            TMP_InputField Input(string title,float y,bool password=false)
            {
                MakeText(title,card.transform,font,title,13,FontStyles.Normal,Muted,new Vector2(.07f,y+.075f),new Vector2(.93f,y+.12f));
                return MakeInputField(title,card.transform,font,"",password,new Vector2(.07f,y),new Vector2(.93f,y+.075f));
            }
            var email=Input(T("Email","邮箱"),.63f);email.contentType=TMP_InputField.ContentType.EmailAddress;email.characterLimit=254;email.text=_emailField.text;
            var code=Input(T("Verification code","验证码"),.48f);code.characterLimit=6;code.contentType=TMP_InputField.ContentType.IntegerNumber;
            TMP_InputField passwordField=null, confirmField=null;
            if(reset){passwordField=Input(T("New password","新密码"),.34f,true);confirmField=Input(T("Confirm password","确认密码"),.20f,true);passwordField.characterLimit=confirmField.characterLimit=128;}
            var status=MakeText("Status",card.transform,font,"",13,FontStyles.Normal,Muted,new Vector2(.07f,reset ? .10f:.25f),new Vector2(.93f,reset ? .19f:.36f));
            bool busy=false;float resendAt=0;
            bool Current()=>this!=null && overlay!=null && _emailFlow==overlay;
            void Close(){if(!Current())return;_emailFlow=null;overlay.SetActive(false);if(Application.isPlaying)Destroy(overlay);else DestroyImmediate(overlay);}
            overlay.AddComponent<KeyboardFocusScope>().Configure(true,Close);
            var secondaryFill = IsLightTheme ? new Color(.83f,.91f,.90f,1f) : new Color(.15f,.30f,.32f,1f);
            var secondaryText = IsLightTheme ? new Color(.02f,.24f,.25f,1f) : Ink;
            var close=MakeButton("Close",card.transform,"×",font,secondaryFill,new Vector2(.87f,.89f),new Vector2(.96f,.97f));
            close.GetComponentInChildren<TextMeshProUGUI>().color=secondaryText;
            close.onClick.AddListener(Close);
            var send=MakeButton("SendCode",card.transform,T("Send / resend code","发送／重发验证码"),font,secondaryFill,new Vector2(.07f,reset ? .015f:.11f),new Vector2(.47f,reset ? .09f:.22f));
            send.GetComponentInChildren<TextMeshProUGUI>().color=secondaryText;
            AddRoundedBorder(send.gameObject,IsLightTheme ? new Color(.38f,.61f,.60f,1f) : Border);
            var finish=MakeButton("Confirm",card.transform,reset ? T("Reset password","重置密码"):T("Verify","验证"),font,Primary,new Vector2(.53f,reset ? .015f:.11f),new Vector2(.93f,reset ? .09f:.22f));
            void Pending(bool value){busy=value;send.interactable=finish.interactable=!value;email.interactable=code.interactable=!value;if(reset)passwordField.interactable=confirmField.interactable=!value;}
            send.onClick.AddListener(()=>
            {
                if(busy)return;
                if(Time.unscaledTime<resendAt){status.text=T("Please wait 60 seconds between requests.","请间隔 60 秒后重试。");return;}
                Pending(true);status.text=T("Requesting code…","正在获取验证码…");
                BackendClient.Instance.EmailAuth(reset ? "password-reset/request":"email-verification/request",new BackendClient.EmailAuthData{email=email.text.Trim()},false,
                    message=>{if(!Current())return;Pending(false);resendAt=Time.unscaledTime+60;status.text=T("If eligible, a code was sent. Check your inbox and spam folder.","如果账号符合条件，验证码已发送，请检查收件箱和垃圾邮件。");},
                    error=>{if(Current()){Pending(false);status.text=error;}});
            });
            finish.onClick.AddListener(()=>
            {
                if(busy)return;
                if(reset && (passwordField.text.Length==0 || passwordField.text!=confirmField.text)){status.text=T("Passwords must match.","两次输入的密码必须一致。");return;}
                Pending(true);status.text=T("Checking…","正在验证…");
                BackendClient.Instance.EmailAuth(reset ? "password-reset/confirm":"email-verification/confirm",new BackendClient.EmailAuthData{email=email.text.Trim(),code=code.text.Trim(),new_password=passwordField?.text??""},false,
                    message=>
                    {
                        if(!Current())return;
                        if(reset)
                        {
                            ClearPendingRegistrationCredentials();
                            _emailField.text=email.text.Trim();_passwordField.text="";Close();_isRegisterMode=false;ApplyAuthModeLayout();
                            SetStatus(T("Done. Sign in with your password.","已完成，请使用密码登录。"),false);
                        }
                        else CompleteEmailVerification(email.text,Close);
                    },
                    error=>{if(Current()){Pending(false);status.text=error;}});
            });
        }
    }
}
