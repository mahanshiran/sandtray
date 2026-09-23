using UnityEngine;
using TMPro;
using Sandplay.Data;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void PromptManagedPasswordChange()
        {
            // Temporary passwords remain marked for account-security guidance, but they do not
            // interrupt or block organization work during the current managed-account rollout.
        }

        private void ShowAccountSecurity(bool changeEmail)
        {
            var backend=BackendClient.Instance;
            if(!backend.IsLoggedIn)return;
            var box=ClientDialog(F("Account security","账号安全"),560,650);
            var dialog=_clientDialog;int user=backend.UserId;int epoch=LocalAccountStorage.Epoch;
            bool Current()=>this!=null && dialog!=null && _clientDialog==dialog && backend.IsLoggedIn && user==backend.UserId && epoch==LocalAccountStorage.Epoch;
            ClientButton(box,F("Change password","修改密码"),.06f,.77f,.42f,.065f,()=>ShowAccountSecurity(false),!changeEmail);
            if(!backend.IsOrganizationTherapist)
                ClientButton(box,F("Change email","更改邮箱"),.52f,.77f,.42f,.065f,()=>ShowAccountSecurity(true),changeEmail);
            ClientText(box,backend.IsOrganizationTherapist
                ? F((backend.MustChangePassword ? "You can change the temporary password here. " : "")+"This login is managed by "+backend.ManagedOrganizationName+"; its login ID cannot be changed here.",
                    (backend.MustChangePassword ? "您可以在此更改临时密码。" : "")+"此登录账户由 "+backend.ManagedOrganizationName+" 管理，无法在此更改登录 ID。")
                : F("Google/Apple accounts manage their password and email with their sign-in provider.","Google／Apple 账号请通过相应平台管理密码和邮箱。"),13,.06f,.65f,.88f,.10f,HomeMuted);
            TMP_InputField Input(string title,float y,bool secret)
            {
                ClientText(box,title,13,.06f,y+.068f,.88f,.04f,HomeMuted);
                var field=ClientInput(box,"","",.06f,y,.88f,.065f,secret?128:254);
                if(secret){field.inputType=TMP_InputField.InputType.Password;field.ForceLabelUpdate();}
                return field;
            }
            var current=Input(F("Current password","当前密码"),.54f,true);
            var next=Input(changeEmail ? F("New email","新邮箱"):F("New password","新密码"),.40f,!changeEmail);
            var confirm=Input(changeEmail ? F("Verification code","验证码"):F("Confirm new password","确认新密码"),.26f,!changeEmail);
            if(changeEmail){next.contentType=TMP_InputField.ContentType.EmailAddress;confirm.characterLimit=6;confirm.contentType=TMP_InputField.ContentType.IntegerNumber;}
            var status=ClientText(box,"",13,.06f,.13f,.88f,.11f,HomeMuted);
            bool busy=false;float resendAt=0;
            BackendClient.EmailAuthData Data()=>new BackendClient.EmailAuthData{current_password=current.text,new_password=changeEmail ? "":next.text,email=changeEmail ? next.text.Trim():backend.UserEmail,code=confirm.text.Trim()};
            if(changeEmail)ClientButton(box,F("Send code","发送验证码"),.06f,.035f,.26f,.065f,()=>
            {
                if(busy)return;
                if(Time.unscaledTime<resendAt){status.text=F("Wait 60 seconds before resending.","请等待 60 秒后重发。");return;}
                busy=true;
                backend.EmailAuth("email-change/request",Data(),true,message=>{if(Current()){busy=false;resendAt=Time.unscaledTime+60;status.text=F("Check the new email inbox for a code.","请检查新邮箱中的验证码。");}},error=>{if(Current()){busy=false;status.text=error;}});
            });
            ClientButton(box,"dialog.cancel",changeEmail ? .35f:.06f,.035f,changeEmail ? .25f:.42f,.065f,CloseClientDialog);
            ClientButton(box,F("Confirm change","确认更改"),.64f,.035f,.30f,.065f,()=>
            {
                if(busy)return;
                if(!changeEmail && (next.text.Length==0 || next.text!=confirm.text)){status.text=F("Passwords must match.","两次输入的密码必须一致。");return;}
                if(!LocalAccountStorage.SaveBeforeIdentityChange()){status.text=F("Save your current table before changing account security.","请先保存当前沙盘。");return;}
                busy=true;
                backend.EmailAuth(changeEmail ? "email-change/confirm":"password/change",Data(),true,message=>
                {
                    if(!Current())return;
                    CloseClientDialog();backend.SignOut();UpdateAccountButton();OpenLoginScreen();
                },error=>{if(Current()){busy=false;status.text=error;}});
            },true);
        }
    }
}
