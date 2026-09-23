using System;
using System.IO;
using System.Collections;
using UnityEngine;
namespace Sandplay.UI
{
    public sealed class CertificatePicker : MonoBehaviour
    {
        bool busy; int epoch; Action<byte[]> complete; Action<string> failure;
        public void Pick(Action<byte[]> success,Action<string> error)
        {
            if(busy)return;busy=true;epoch=Sandplay.Data.LocalAccountStorage.Epoch;complete=success;failure=error;
#if UNITY_EDITOR
            Load(UnityEditor.EditorUtility.OpenFilePanel("Certificate", "", "pdf,png,jpg,jpeg"));
#elif UNITY_ANDROID || UNITY_IOS
            if(NativeFilePicker.IsFilePickerBusy()){busy=false;return;}
            NativeFilePicker.PickFile(Load,new[]{NativeFilePicker.ConvertExtensionToFileType("pdf"),NativeFilePicker.ConvertExtensionToFileType("png"),NativeFilePicker.ConvertExtensionToFileType("jpg")});
#elif UNITY_WEBGL
            busy=false;failure?.Invoke("Certificate uploads are available in the mobile and desktop apps.");
#else
            StartCoroutine(Desktop());
#endif
        }
#if !UNITY_EDITOR && !UNITY_ANDROID && !UNITY_IOS && !UNITY_WEBGL
        IEnumerator Desktop()
        {
            if(SimpleFileBrowser.FileBrowser.IsOpen){busy=false;yield break;}
            SimpleFileBrowser.FileBrowser.SetFilters(false,new SimpleFileBrowser.FileBrowser.Filter("Certificates",".pdf",".jpg",".jpeg",".png"));
            yield return SimpleFileBrowser.FileBrowser.WaitForLoadDialog(SimpleFileBrowser.FileBrowser.PickMode.Files,false,null,null,"Certificate","Select");
            Load(SimpleFileBrowser.FileBrowser.Success?SimpleFileBrowser.FileBrowser.Result[0]:null);
        }
#endif
        void Load(string path)
        {
            if(this==null || epoch!=Sandplay.Data.LocalAccountStorage.Epoch)return;
            busy=false;if(string.IsNullOrEmpty(path))return;
            try
            {
                if(new FileInfo(path).Length>10*1024*1024)throw new IOException();
                complete?.Invoke(File.ReadAllBytes(path));
            }
            catch(Exception){failure?.Invoke("Choose a PDF, JPG or PNG up to 10 MB.");}
        }
    }
}
