using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sandplay.Data
{
    [Serializable] public sealed class PersonalClientSyncItem
    {
        public string id, record_json = "", photo_base64 = "";
        public int revision;
        public bool deleted;
    }
    [Serializable] public sealed class PersonalClientSyncPage { public PersonalClientSyncItem[] items; public string next; }
    [Serializable] public sealed class PersonalClientSyncResponse { public PersonalClientSyncItem item; public bool conflict; }
    [Serializable] public sealed class PersonalClientSyncState
    {
        public int schema = 1;
        public List<PersonalClientSyncItem> items = new List<PersonalClientSyncItem>();
    }

    public static class PersonalClientSync
    {
        public enum Decision { Equal, Upload, Download, Conflict }
        public static bool Same(PersonalClientSyncItem a, PersonalClientSyncItem b)
        {
            bool absentA = a == null || a.deleted, absentB = b == null || b.deleted;
            if (absentA || absentB) return absentA == absentB;
            return a.photo_base64 == b.photo_base64 && JToken.DeepEquals(JToken.Parse(a.record_json), JToken.Parse(b.record_json));
        }
        public static Decision Decide(PersonalClientSyncItem baseline, PersonalClientSyncItem local, PersonalClientSyncItem remote)
        {
            if (Same(local, remote)) return Decision.Equal;
            bool localChanged = !Same(local, baseline), remoteChanged = !Same(remote, baseline);
            // Tombstones are authoritative history even when this device has no baseline.
            if (baseline == null && local != null && remote?.deleted == true) return Decision.Conflict;
            if (localChanged && remoteChanged) return Decision.Conflict;
            return localChanged ? Decision.Upload : Decision.Download;
        }
        public static PersonalClientSyncItem Snapshot(ClientRecordStore store, ClientRecord record)
        {
            if (record == null) return null;
            byte[] photo = store.ReadPhoto(record.PhotoFile);
            if (!string.IsNullOrEmpty(record.PhotoFile) && photo == null)
                throw new IOException("A client photo is missing. Restore or remove the photo before syncing.");
            return new PersonalClientSyncItem { id = record.Id, record_json = JsonUtility.ToJson(record),
                photo_base64 = photo == null ? "" : Convert.ToBase64String(photo) };
        }
        public static PersonalClientSyncState ReadState(string path)
        {
            PersonalClientSyncState Read(string file)
            {
                if (!File.Exists(file)) return null;
                try
                {
                    var state = JsonUtility.FromJson<PersonalClientSyncState>(File.ReadAllText(file));
                    if (state?.schema != 1 || state.items == null || state.items.Any(i => i == null || string.IsNullOrEmpty(i.id)) ||
                        state.items.Select(i => i.id).Distinct().Count() != state.items.Count) return null;
                    return state;
                }
                catch (ArgumentException) { return null; }
            }
            var result = Read(path) ?? Read(path + ".bak");
            if (result != null) return result;
            if (File.Exists(path) || File.Exists(path + ".bak")) throw new IOException("Client sync history needs recovery; local records are preserved.");
            return new PersonalClientSyncState();
        }
    }
}
