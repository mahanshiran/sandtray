using System;
using UnityEngine;
using Sandplay.Data;
namespace Sandplay.Core
{
    [Serializable] public class CloudRecordInfo { public string id,kind,title,updated_at,checksum; public int revision; public bool deleted; }
    [Serializable] public class CloudRecordPage { public CloudRecordInfo[] items; public string next; }
    [Serializable] public class CloudVersionInfo { public int revision,schema_version; public string checksum,created_at; public long size_bytes; }
    [Serializable] public class CloudRecordDetail : CloudRecordInfo { public CloudVersionInfo[] versions; }
    [Serializable] public class CloudTableVersion { public CloudRecordInfo record; public int revision,schema_version; public string payload_json,checksum; }
    [Serializable] public class CloudPutTable
    {
        public string action="put",operation_id,record_id,kind="table",title,organization_id,organization_client_id;
        public int expected_revision,schema_version=1;
        public SessionData payload;
    }
    public partial class BackendClient
    {
        internal static SessionData DecodeCloudTable(CloudTableVersion version)
        {
            if(version==null || version.schema_version!=1 || string.IsNullOrEmpty(version.payload_json))
                throw new InvalidOperationException("Invalid cloud schema.");
            using var hash=System.Security.Cryptography.SHA256.Create();
            string checksum=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(version.payload_json))).Replace("-","").ToLowerInvariant();
            if(!string.Equals(checksum,version.checksum,StringComparison.Ordinal))
                throw new InvalidOperationException("Cloud backup checksum does not match.");
            return JsonUtility.FromJson<SessionData>(version.payload_json);
        }

        public void CloudRequest<T>(string path,string body,Action<T> success,Action<string> failure) where T:class
        {
            if(!IsLoggedIn || UserId<=0) { failure?.Invoke("Sign in first."); return; }
            if(!Uri.TryCreate(BaseUrl,UriKind.Absolute,out var endpoint) || endpoint.Scheme!="https")
            {failure?.Invoke("Cloud backups require HTTPS.");return;}
            int user=UserId; string token=AccessToken;
            bool Current()=>this!=null && Instance==this && user==UserId && token==AccessToken;
            void Done(string json)
            {
                if(!Current()) return;
                T result;
                try { result=JsonUtility.FromJson<T>(json); }
                catch(Exception) { failure?.Invoke("Invalid cloud response."); return; }
                if(result==null) { failure?.Invoke("Invalid cloud response."); return; }
                success?.Invoke(result);
            }
            void Error(string message) { if(Current()) failure?.Invoke(message); }
            string url=BaseUrl+"/cloud/v1/"+path;
            StartCoroutine(body==null ? Get(url,token,Done,Error) : Post(url,body,token,Done,Error));
        }
    }
}
