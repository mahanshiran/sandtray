using System;
using System.Linq;
using Sandplay.Data;
using TMPro;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void BuildPersonalClientSyncControls()
        {
            var service=PersonalClientSyncClient.Instance;
            var label=ClientText(_clientsPage.transform,"",11,.305f,.895f,.685f,.025f,HomeMuted);
            label.richText=false;
            var sync=ClientButton(_clientsPage.transform,F("Sync now","立即同步"),.70f,.91f,.13f,.065f,()=>
            {
                if(!LocalAccountStorage.IsIsolated){ShowLocalCapacitySetup();return;}
                if(service.Conflicts.Count>0)ShowPersonalClientSyncConflict();else service.Refresh();
            });
            void Render()
            {
                if(label==null)return;
                label.text=service.Status;
                sync.interactable=!service.Busy;
                sync.GetComponentInChildren<TMP_Text>().text=service.Conflicts.Count>0
                    ?F("Review edits","核对更改")+" ("+service.Conflicts.Count+")":F("Sync now","立即同步");
                if(!service.Busy && _clientsPage.activeInHierarchy && CanUsePersonalClientDirectory && !_independentOrganizationClientMode)
                {
                    try { RefreshClientList();RefreshClientDetail(); }
                    catch(Exception) { if(_clientStatus!=null)_clientStatus.text=Localization.Get("clients.storage_error"); }
                }
            }
            service.Changed+=Render;
            _clientsPage.AddComponent<FriendViewLifetime>().Released=()=>service.Changed-=Render;
            Render();
        }
        private string ClientSyncDescription(PersonalClientSyncItem item)
        {
            if(item==null||item.deleted)return F("Client deleted on this device/version.","此设备或版本已删除来访者。");
            var record=JsonUtility.FromJson<ClientRecord>(item.record_json);
            return string.Join("\n",new[]{record.Name,record.Reference,record.DateOfBirth,record.Email,record.Phone,
                record.Account?.DisplayName,record.Archived?F("Archived","已归档"):F("Active","使用中"),
                string.IsNullOrEmpty(item.photo_base64)?F("No photo","无照片"):F("Has photo","有照片"),record.Notes}
                .Where(value=>!string.IsNullOrWhiteSpace(value)));
        }
        private void ShowPersonalClientSyncConflict()
        {
            var service=PersonalClientSyncClient.Instance;
            var conflict=service.Conflicts.FirstOrDefault();if(conflict==null)return;
            int epoch=LocalAccountStorage.Epoch;
            var box=ClientDialog(F("Review client changes","核对来访者更改"),960,720);StyleFriendDialog(box);
            box.gameObject.AddComponent<FriendViewLifetime>().Released=service.PauseForClientEdit();
            var dialog=_clientDialog;
            ClientText(box,F("This client changed on more than one device. Choose which version to keep. A local recovery copy of both versions will be saved.",
                "此来访者在多个设备上有更改。请选择要保留的版本。两个版本都将保留本地恢复副本。"),14,.04f,.72f,.92f,.12f,HomeMuted);
            void Version(string title,PersonalClientSyncItem item,float x)
            {
                ClientText(box,title,17,x,.65f,.44f,.06f,HomeText);
                var list=ClientScroll(box,title,x,.16f,.44f,.48f);
                if(!string.IsNullOrEmpty(item?.photo_base64))
                {
                    var tex=new Texture2D(2,2);
                    try
                    {
                        if(tex.LoadImage(Convert.FromBase64String(item.photo_base64)))
                        {
                            var row=ClientRow(list,"Photo",100);
                            var image=ClientRect(row,"Image",.02f,.02f,.96f,.96f).gameObject.AddComponent<UnityEngine.UI.RawImage>();
                            image.texture=tex;image.raycastTarget=false;
                            var fit=image.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
                            fit.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=(float)tex.width/tex.height;
                            row.gameObject.AddComponent<FriendViewLifetime>().Released=()=>Destroy(tex);
                        }
                        else Destroy(tex);
                    }
                    catch { Destroy(tex); }
                }
                string description=ClientSyncDescription(item);
                var textRow=ClientRow(list,"Version",100);
                var text=ClientText(textRow,description,15,.02f,0,.96f,1,HomeText);text.richText=false;text.alignment=TextAlignmentOptions.TopLeft;
                Canvas.ForceUpdateCanvases();
                textRow.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=Mathf.Max(100,text.GetPreferredValues(description,Mathf.Max(150,((RectTransform)list).rect.width-24),0).y+20);
            }
            Version(F("This device","此设备"),conflict.local,.04f);Version(F("Cloud","云端"),conflict.remote,.52f);
            void Resolve(bool local)
            {
                if(epoch!=LocalAccountStorage.Epoch||dialog!=_clientDialog)return;
                service.Resolve(conflict,local);CloseClientDialog();
            }
            ClientButton(box,F("Keep this device's version","保留此设备版本"),.04f,.04f,.44f,.08f,()=>Resolve(true));
            ClientButton(box,F("Keep cloud version","保留云端版本"),.52f,.04f,.44f,.08f,()=>Resolve(false));
            GuardNotificationAccount(box,dialog);
        }
    }
}
