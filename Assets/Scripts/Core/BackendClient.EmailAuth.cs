using System;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class BackendClient
    {
        [Serializable] public class EmailAuthData
        {
            public string email = "", code = "", current_password = "", new_password = "";
        }
        [Serializable] private class EmailAuthResult { public string detail; }
        public void EmailAuth(string operation, EmailAuthData data, bool authenticated, Action<string> done, Action<string> failed)
        {
            var guard = CaptureCredentialGuard();
            int epoch = Sandplay.Data.LocalAccountStorage.Epoch;
            StartCoroutine(Post(BaseUrl + "/auth/" + operation + "/", JsonUtility.ToJson(data), authenticated ? AccessToken : null,
                json =>
                {
                    if (authenticated && (!guard() || epoch != Sandplay.Data.LocalAccountStorage.Epoch)) return;
                    done?.Invoke(JsonUtility.FromJson<EmailAuthResult>(json)?.detail ?? "Done.");
                }, error => { if (!authenticated || guard()) failed?.Invoke(error); }));
        }
    }
}
