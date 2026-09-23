using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.UI;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        void CertificateLink(Transform content,TherapistCertificateData certificate)
        {
            var row=ClientRow(content,"Certificate",88);
            var label=ClientText(row,certificate.title+"\n"+certificate.issuer+ (string.IsNullOrEmpty(certificate.expires_on)?"":" · "+F("Expires ","有效期至 ")+certificate.expires_on),14,.025f,.08f,.68f,.84f,HomeText);
            label.richText=false;
            ClientButton(row,F("View", "查看"),.73f,.20f,.24f,.60f,()=>OpenCertificate(certificate.url));
        }
        void OpenCertificate(string path)
        {
            if(string.IsNullOrEmpty(path))return;
            if(path.StartsWith("/"))path=new Uri(new Uri(BackendClient.BaseUrl),path).AbsoluteUri;
            if(Uri.TryCreate(path,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.Host==new Uri(BackendClient.BaseUrl).Host)
                Application.OpenURL(path);
        }
        void BuildCertificateEditor(Transform content,Func<bool> current)
        {
            var header=ClientText(ClientRow(content,"Certificates",100),F("Certificates · Optional · 0/10\nPublic documents · Therapist-provided, not verified. Changes save immediately.","证书 · 可选 · 0/10\n公开文件 · 治疗师自行提供，未经核实。更改立即保存。"),14,.025f,.05f,.95f,.90f,HomeMuted);
            var list=ClientRow(content,"Certificate list",0);
            var layout=list.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childForceExpandHeight=false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            TMP_InputField Input(string name)
            {
                var row=ClientRow(content,name,80);ClientText(row,name,13,.025f,.66f,.95f,.3f,HomeMuted);
                return ClientInput(row,"",name,.025f,.04f,.95f,.58f,200);
            }
            var title=Input(F("Certificate name", "证书名称"));var issuer=Input(F("Issuing organization (optional)","颁发机构（可选）"));
            var issued=Input(F("Issue date (optional, YYYY-MM-DD)","颁发日期（可选，YYYY-MM-DD）"));
            var expires=Input(F("Expiry date (optional, YYYY-MM-DD)","到期日期（可选，YYYY-MM-DD）"));
            var status=ClientText(ClientRow(content,"Upload status",52),F("PDF, JPG or PNG · Up to 10 MB each. Upload only documents you want users to see.","PDF、JPG 或 PNG · 每份不超过 10 MB。仅上传您希望用户查看的文件。"),12,.025f,.05f,.95f,.90f,HomeMuted);
            var picker=content.gameObject.AddComponent<CertificatePicker>();bool busy=false;int count=0;
            void Error(string error){if(content==null)return;busy=false;status.text=error;}
            void RunMutation(Button button,string progress,string completed,
                Action<Action<TherapistCertificateList>,Action<string>> request,Action afterSuccess=null)
            {
                if(busy || !current())return;
                busy=true;if(button)button.interactable=false;status.text=progress;
                BlockingOperationFeedback.Ticket loading=null;
                loading=BeginUiOperation(progress,65f,()=>
                {
                    if(content==null || !current())return;
                    busy=false;if(button)button.interactable=true;
                    status.text=F("The request timed out. Please try again.","请求超时，请重试。");
                });
                request(_=>
                {
                    if(!CompleteUiOperation(loading) || content==null || !current())return;
                    busy=false;if(button)button.interactable=true;status.text=completed;
                    afterSuccess?.Invoke();Reload();
                },error=>
                {
                    if(!CompleteUiOperation(loading) || content==null || !current())return;
                    busy=false;if(button)button.interactable=true;status.text=error;
                });
            }
            void Reload()
            {
                BackendClient.Instance.Certificates("GET",null,null,null,null,null,null,result=>
                {
                    if(content==null || !current())return;
                    busy=false;foreach(Transform child in list){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                    count=result.certificates?.Length??0;
                    header.text=F("Certificates · Optional · ","证书 · 可选 · ")+count+"/10\n"+F("Public documents · Therapist-provided, not verified. Changes save immediately.","公开文件 · 治疗师自行提供，未经核实。更改立即保存。");
                    foreach(var certificate in result.certificates??new TherapistCertificateData[0])
                    {
                        CertificateLink(list,certificate);
                        var actions=ClientRow(list,"Certificate actions",54);
                        Button replace=null;
                        replace=ClientButton(actions,F("Replace file","替换文件"),.025f,.08f,.45f,.84f,()=>
                        {
                            if(busy || !current())return;
                            picker.Pick(bytes=>
                            {
                                if(!current())return;
                                RunMutation(replace,F("Replacing certificate…","正在替换证书…"),
                                    F("Certificate replaced.","证书已替换。"),(success,failure)=>
                                    BackendClient.Instance.Certificates("PATCH",certificate.id,bytes,null,null,null,null,success,failure));
                            },Error);
                        });
                        Button remove=null;
                        remove=ClientButton(actions,F("Remove","移除"),.52f,.08f,.45f,.84f,()=>
                        {
                            RunMutation(remove,F("Removing certificate…","正在移除证书…"),
                                F("Certificate removed.","证书已移除。"),(success,failure)=>
                                BackendClient.Instance.Certificates("DELETE",certificate.id,null,null,null,null,null,success,failure));
                        });
                    }
                },Error);
            }
            Button add=null;
            add=ClientButton(ClientRow(content,"Add certificate",58),F("Add public certificate","添加公开证书"),.025f,.06f,.95f,.88f,()=>
            {
                if(busy || !current())return;
                if(count>=10){Error(F("You can upload up to 10 certificates.","最多可上传 10 份证书。"));return;}
                if(string.IsNullOrWhiteSpace(title.text)){Error(F("Enter a certificate name.","请输入证书名称。"));return;}
                picker.Pick(bytes=>
                {
                    if(content==null || !current())return;
                    RunMutation(add,F("Uploading certificate…","正在上传证书…"),
                        F("Certificate published.","证书已发布。"),(success,failure)=>
                        BackendClient.Instance.Certificates("POST",null,bytes,title.text.Trim(),issuer.text.Trim(),issued.text.Trim(),expires.text.Trim(),success,failure),
                        ()=>title.text=issuer.text=issued.text=expires.text="");
                },Error);
            });
            Reload();
        }
    }
}
