using UnityEngine;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private const string PrivacyPolicyUrl = "https://sandtraypro.com/privacy/";
        private const string TermsOfUseUrl = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/";
        private const string SupportEmail = "contact@sandtraypro.com";
        private const string AppFilingNumber = "浙ICP备2024093930号-17A";
        private const string GovernmentFilingUrl = "https://beian.miit.gov.cn/";

        private void BuildSupportSettingsRows(Transform list)
        {
            SettingsAction(list, "support.delete_account", "Btn_AccountDeletion", "support.request",
                ShowAccountDeletion);
            SettingsAction(list, "support.title", "Btn_HelpSupport", "settings.view", () =>
            {
                CloseHomeSettingsSheet();
                ShowHelpSupport();
            });
            SettingsAction(list, "sub.privacy", "Btn_SettingsPrivacy", "settings.view",
                () => Application.OpenURL(PrivacyPolicyUrl));
            SettingsAction(list, "sub.terms_link", "Btn_SettingsTerms", "settings.view",
                () => Application.OpenURL(TermsOfUseUrl));
            SettingsAction(list, "support.regulatory_filing", "Btn_GovernmentFiling", AppFilingNumber,
                () => Application.OpenURL(GovernmentFilingUrl));
        }

        private void ShowHelpSupport()
        {
            var box = ClientDialog(Localization.Get("support.title"), 620, 560);
            var list = ClientScroll(box, "SupportList", .04f, .04f, .92f, .79f);
            var emailRow = ClientRow(list, "SupportEmail", 64);
            ClientText(emailRow, SupportEmail, 18, .025f, .1f, .95f, .8f, HomeText);
            SettingsAction(list, "support.email", "Btn_EmailSupport", "support.open_email",
                () => Application.OpenURL("mailto:" + SupportEmail));
            SettingsAction(list, "support.copy_email", "Btn_CopySupportEmail", "support.copy", () =>
            {
                GUIUtility.systemCopyBuffer = SupportEmail;
                // Keep feedback local to this button; it resets on reopening the dialog.
                var button = list.Find("support.copy_email/Btn_CopySupportEmail");
                if (button != null)
                    button.GetComponentInChildren<TMPro.TMP_Text>().text = Localization.Get("support.copied");
            });
            SettingsAction(list, "support.website", "Btn_SupportWebsite", "settings.view",
                () => Application.OpenURL("https://sandtraypro.com/"));
            var version = ClientRow(list, "AppVersion", 64);
            ClientText(version, Localization.Get("support.version") + " " + Application.version,
                16, .025f, .1f, .95f, .8f, HomeMuted);
        }

        private void ShowAccountDeletion()
        {
            var backend = BackendClient.Instance;
            if (!backend.IsLoggedIn) { OpenLoginScreen(); return; }
            CloseHomeSettingsSheet();
            var box = ClientDialog(F("Delete account", "删除账号"), 620, 600);
            var dialog = _clientDialog;
            int user = backend.UserId;
            int epoch = Sandplay.Data.LocalAccountStorage.Epoch;
            bool Current() => this != null && dialog != null && _clientDialog == dialog &&
                backend.IsLoggedIn && backend.UserId == user && Sandplay.Data.LocalAccountStorage.Epoch == epoch;

            ClientText(box, F(
                "This permanently deletes your Sandtray account and personal cloud data. Organization-owned records may require the organization owner to resolve them first.",
                "这将永久删除您的 Sandtray 账号和个人云端数据。组织拥有的记录可能需要先由组织所有者处理。"),
                15, .06f, .60f, .88f, .22f, HomeMuted);
            ClientText(box, F("Account email", "账号邮箱"), 13, .06f, .51f, .88f, .04f, HomeMuted);
            var email = ClientInput(box, backend.UserEmail, "", .06f, .44f, .88f, .065f, 254);
            email.interactable = false;
            ClientText(box, F("Verification code", "验证码"), 13, .06f, .36f, .88f, .04f, HomeMuted);
            var code = ClientInput(box, "", F("6-digit code", "6 位验证码"), .06f, .29f, .88f, .065f, 6);
            code.contentType = TMPro.TMP_InputField.ContentType.IntegerNumber;
            var status = ClientText(box, "", 13, .06f, .16f, .88f, .10f, HomeMuted);
            bool busy = false;
            float resendAt = 0;
            BackendClient.EmailAuthData Data() => new BackendClient.EmailAuthData {
                email = backend.UserEmail, code = code.text.Trim()
            };

            ClientButton(box, F("Send verification code", "发送验证码"), .06f, .06f, .40f, .075f, () =>
            {
                if (busy) return;
                if (Time.unscaledTime < resendAt) { status.text = F("Wait before requesting another code.", "请稍候再请求验证码。"); return; }
                busy = true;
                backend.EmailAuth("account-deletion/request", Data(), true, message =>
                {
                    if (!Current()) return;
                    busy = false; resendAt = Time.unscaledTime + 60;
                    status.text = F("Check your email for the verification code.", "请检查邮箱中的验证码。");
                }, error => { if (Current()) { busy = false; status.text = error; } });
            });
            ClientButton(box, "dialog.cancel", .49f, .06f, .22f, .075f, CloseClientDialog);
            ClientButton(box, F("Delete permanently", "永久删除"), .74f, .06f, .20f, .075f, () =>
            {
                if (busy) return;
                if (code.text.Trim().Length != 6) { status.text = F("Enter the six-digit code first.", "请先输入 6 位验证码。"); return; }
                busy = true;
                backend.EmailAuth("account-deletion/confirm", Data(), true, message =>
                {
                    if (!Current()) return;
                    // Delete only the isolated account directory; legacy shared
                    // storage is intentionally preserved for other accounts.
                    try { Sandplay.Data.LocalAccountStorage.DeleteCurrentAccountData(); }
                    catch (System.Exception ex) { Debug.LogWarning("[Account deletion] Local cleanup: " + ex.GetType().Name); }
                    CloseClientDialog();
                    backend.ForgetDeletedAccount();
                    UpdateAccountButton();
                    OpenLoginScreen();
                }, error => { if (Current()) { busy = false; status.text = error; } });
            });
        }
    }
}
