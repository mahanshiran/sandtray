using System;
using System.IO;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Data
{
    public partial class SessionManager
    {
        public bool CapacityEnforced => BackendClient.Instance.IsLoggedIn && BackendClient.Instance.UserId > 0 &&
            GameManager.Instance != null && GameManager.Instance.Config != null &&
            GameManager.Instance.Config.LocalTableCapacityEnforcement;

        public void SaveNewSession(string name, Action saved, Action<string> failed)
        {
            void Persist()
            {
                SessionData data = null;
                try
                {
                    data = CaptureValidatedSession(name);
                data.LocalCapacityId = Guid.NewGuid().ToString();
                data.BoardHistoryId = Guid.NewGuid().ToString("N");
                string path = Path.Combine(SavePath, SanitizeFileName(name) + ".json");
                // The file itself is the durable queue intent. No quota IO precedes it.
                LocalRecordFile.Write(path, JsonUtility.ToJson(data, true), createOnly: true);
                _boardReady = true;
                _lastSavedContent = SceneSignature(data);
                RefreshBoardPersistenceStatus(data);
                _nextBoardQuotaScan = 0;
                }
                catch (Exception ex) { failed?.Invoke(ex.Message); return; }
                EventBus.Publish(new SessionSavedEvent { SessionName = name });
                saved?.Invoke();
                if (!string.IsNullOrEmpty(data.OrganizationId) && !string.IsNullOrEmpty(data.OrganizationClientId))
                    BackendClient.Instance.RecordOrganizationBoardCreated(data.OrganizationId, data.OrganizationClientId, data.BoardHistoryId, data.SessionName);
            }

            if (!CapacityEnforced) { Persist(); return; }
            // Refresh the server snapshot before creating a new local table. This
            // prevents the UI from creating known-over-limit boards while the
            // journal reservation is being reconciled in the background.
            BackendClient.Instance.FetchAccessSnapshot(snapshot =>
            {
                var capability = AccessPolicy.Find(snapshot?.capabilities, "tables.capacity");
                if (capability == null || !capability.entitled || (!capability.unlimited && capability.remaining <= 0))
                { failed?.Invoke("Your table storage limit has been reached."); return; }
                Persist();
            }, error => failed?.Invoke(string.IsNullOrEmpty(error) ? "Could not verify table storage capacity." : error), force: true);
        }
    }
}
