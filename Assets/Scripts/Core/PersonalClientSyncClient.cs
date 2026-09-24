using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Core
{
    // Durable local records are the offline queue; the acknowledged server copy is the merge baseline.
    public sealed class PersonalClientSyncClient : MonoBehaviour
    {
        static PersonalClientSyncClient instance;
        public static PersonalClientSyncClient Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GameObject("PersonalClientSync").AddComponent<PersonalClientSyncClient>();
                    if (Application.isPlaying) DontDestroyOnLoad(instance.gameObject);
                }
                return instance;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void StartSync() { var client = Instance; }
        public sealed class Conflict { public PersonalClientSyncItem local, remote; public string id; }
        public readonly List<Conflict> Conflicts = new List<Conflict>();
        public event Action Changed;
        public string Status { get; private set; } = "";
        public bool Busy { get; private set; }
        int user, epoch = -1, generation;
        string token = "", statePath;
        float next;
        bool checkedLegacy;
        int editHolds, editScope;
        public Action PauseForClientEdit()
        {
            int scope = editScope; editHolds++;
            return () => { if (scope == editScope) { editHolds = Math.Max(0, editHolds - 1); next = 0; } };
        }
        void OnEnable() { ClientRecordStore.LocalChanged += LocalChanged; }
        void OnDisable() { ClientRecordStore.LocalChanged -= LocalChanged; }
        void LocalChanged() { next = 0; Status = Text("Client changes waiting to sync.", "来访者更改等待同步。"); Notify(); }
        ClientRecordStore store;
        PersonalClientSyncState state;
        readonly Dictionary<string, PersonalClientSyncItem> remote = new Dictionary<string, PersonalClientSyncItem>();
        bool Ready => BackendClient.Instance.IsLoggedIn && BackendClient.Instance.UserType == "psychologist" &&
            BackendClient.Instance.UserId > 0 && !LocalAccountStorage.RequiresRestart && LocalAccountStorage.IsIsolated &&
            SessionManager.Instance != null && SessionManager.Instance.WorkspaceIsCurrent;
        string Text(string en, string zh) => FriendsClient.Text(en, zh);
        void Update()
        {
            var backend = BackendClient.Instance;
            if (user != backend.UserId || token != backend.AccessToken || epoch != LocalAccountStorage.Epoch)
            {
                generation++; editScope++; editHolds=0; Busy = false; user = backend.UserId; token = backend.AccessToken; epoch = LocalAccountStorage.Epoch;
                state = null; store = null; checkedLegacy = false; remote.Clear(); Conflicts.Clear(); Status = ""; next = 0; Changed?.Invoke();
            }
            if (!Ready)
            {
                if (Busy) { generation++; Busy=false; store=null; state=null; next=0; Conflicts.Clear(); }
                if (backend.IsLoggedIn && backend.UserType == "psychologist" && !LocalAccountStorage.RequiresRestart && !LocalAccountStorage.IsIsolated)
                {
                    if (!checkedLegacy && backend.UserId > 0 && SessionManager.Instance != null &&
                        SessionManager.Instance.WorkspaceIsCurrent && !SessionManager.Instance.HasOpenBoard &&
                        (NetworkBootstrapper.Instance == null || !NetworkBootstrapper.Instance.IsOnline))
                    {
                        checkedLegacy = true;
                        try
                        {
                            var migration = new LocalOwnershipMigration(Application.persistentDataPath, BackendClient.BaseUrl, backend.UserId,
                                () => backend.IsLoggedIn && backend.UserId == user);
                            if (migration.Preview().Files.Length == 0)
                            { LocalAccountStorage.ActivateEmptyWorkspace(); return; }
                        }
                        catch (Exception) { /* Existing/recovery data must be reviewed, never automatically claimed. */ }
                    }
                    string message = Text("Choose Sync now to confirm ownership of existing records before cloud sync.", "请选择立即同步，确认现有记录归属后启用云同步。");
                    if (Status != message) { Status = message; Notify(); }
                }
                return;
            }
            if (!Busy && Time.unscaledTime >= next) Refresh();
        }
        bool Current(int request) => this != null && request == generation && Ready && user == BackendClient.Instance.UserId &&
            token == BackendClient.Instance.AccessToken && epoch == LocalAccountStorage.Epoch;
        void Notify() => Changed?.Invoke();
        public void Refresh()
        {
            if (!Ready || Busy || editHolds > 0) return;
            if (user != BackendClient.Instance.UserId || token != BackendClient.Instance.AccessToken || epoch != LocalAccountStorage.Epoch) { next = 0; return; }
            if (Application.internetReachability == NetworkReachability.NotReachable)
            { Status = Text("Offline · changes will sync when connected.", "离线 · 联网后将同步更改。"); next = Time.unscaledTime + 15; Notify(); return; }
            int request = ++generation;
            try
            {
                if (store == null)
                {
                    var guard = LocalAccountStorage.CaptureGuard();
                    string directory = Path.Combine(LocalAccountStorage.Root, "Clients");
                    statePath = Path.Combine(directory, "sync-v1.json");
                    state = PersonalClientSync.ReadState(statePath);
                    store = new ClientRecordStore(directory, guard);
                }
                Busy = true; Status = Text("Syncing clients…", "正在同步来访者…"); Notify(); remote.Clear(); Conflicts.Clear();
                FetchPage(request, "");
            }
            catch (Exception ex) { Fail(request, ex.Message); }
        }
        void Fail(int request, string error)
        {
            if (!Current(request)) return;
            Busy = false; next = Time.unscaledTime + 45;
            Status = Text("Client sync paused: ", "来访者同步暂停：") + error; Notify();
        }
        void FetchPage(int request, string cursor)
        {
            BackendClient.Instance.CloudRequest<PersonalClientSyncPage>("personal-clients/" + (cursor == "" ? "" : "?after=" + Uri.EscapeDataString(cursor)), null, page =>
            {
                if (!Current(request)) return;
                try
                {
                    foreach (var item in page.items ?? Array.Empty<PersonalClientSyncItem>()) remote.Add(item.id, item);
                    if (!string.IsNullOrEmpty(page.next))
                    {
                        if (string.CompareOrdinal(page.next, cursor) <= 0) throw new InvalidDataException("Invalid client sync cursor.");
                        FetchPage(request, page.next); return;
                    }
                    var ids = store.GetAll().Select(c => c.Id).Union(remote.Keys).Union(state.items.Select(i => i.id)).Distinct().ToArray();
                    Process(request, new Queue<string>(ids));
                }
                catch (Exception ex) { Fail(request, ex.Message); }
            }, error => Fail(request, error));
        }
        void Acknowledge(PersonalClientSyncItem item)
        {
            var previous = state.items.Find(i => i.id == item.id);
            if (previous != null && previous.revision == item.revision && PersonalClientSync.Same(previous, item)) return;
            var updated = new PersonalClientSyncState { items = state.items.Where(i => i.id != item.id).Concat(new[] { item }).ToList() };
            LocalRecordFile.Write(statePath, JsonUtility.ToJson(updated, true));
            state = updated;
        }
        void Process(int request, Queue<string> ids)
        {
            if (!Current(request)) return;
            if (editHolds > 0) { Busy=false; next=Time.unscaledTime+2; Status=Text("Client edit in progress · sync waiting.", "正在编辑来访者 · 同步等待中。"); Notify(); return; }
            try
            {
                while (ids.Count > 0)
                {
                    string id = ids.Dequeue();
                    var local = PersonalClientSync.Snapshot(store, store.GetAll().Find(c => c.Id == id));
                    remote.TryGetValue(id, out var cloud);
                    var baseline = state.items.Find(i => i.id == id);
                    if (cloud == null && baseline != null) throw new InvalidDataException("Cloud client history is missing; local records are preserved.");
                    switch (PersonalClientSync.Decide(baseline, local, cloud))
                    {
                        case PersonalClientSync.Decision.Equal:
                            if (cloud != null) Acknowledge(cloud);
                            break;
                        case PersonalClientSync.Decision.Download:
                            store.ApplySynced(cloud, local); Acknowledge(cloud); break;
                        case PersonalClientSync.Decision.Conflict:
                            Conflicts.Add(new Conflict { id = id, local = local, remote = cloud }); break;
                        case PersonalClientSync.Decision.Upload:
                            Push(request, id, local, cloud, () => Process(request, ids)); return;
                    }
                }
                var currentRecords = store.GetAll();
                var currentIds = currentRecords.Select(c => c.Id).Union(state.items.Select(i => i.id));
                bool pending = currentIds.Any(id => !PersonalClientSync.Same(
                    PersonalClientSync.Snapshot(store, currentRecords.Find(c => c.Id == id)), state.items.Find(i => i.id == id)));
                Busy = false; next = Time.unscaledTime + (pending && Conflicts.Count == 0 ? 1 : 30);
                Status = Conflicts.Count > 0 ? Text("Client edits need review: ", "来访者更改需要核对：") + Conflicts.Count :
                    pending ? Text("Client changes waiting to sync.", "来访者更改等待同步。") :
                    Text("Clients synced · ", "来访者已同步 · ") + DateTime.Now.ToString("t");
                Notify();
            }
            catch (Exception ex) { Fail(request, ex.Message); }
        }
        void Push(int request, string id, PersonalClientSyncItem local, PersonalClientSyncItem cloud, Action done)
        {
            var payload = new PersonalClientSyncItem { id = id, revision = cloud?.revision ?? 0, deleted = local == null || local.deleted,
                record_json = local?.record_json ?? "", photo_base64 = local?.photo_base64 ?? "" };
            BackendClient.Instance.CloudRequest<PersonalClientSyncResponse>("personal-clients/", JsonUtility.ToJson(payload), result =>
            {
                if (!Current(request)) return;
                try
                {
                    if (result.item == null || result.item.id != id) throw new InvalidDataException("Invalid client sync response.");
                    if (result.conflict)
                        Conflicts.Add(new Conflict { id = id, local = PersonalClientSync.Snapshot(store, store.GetAll().Find(c => c.Id == id)), remote = result.item });
                    else Acknowledge(result.item);
                    done();
                }
                catch (Exception ex) { Fail(request, ex.Message); }
            }, error => Fail(request, error));
        }
        public void Resolve(Conflict conflict, bool useLocal)
        {
            if (!Ready || Busy || !Conflicts.Contains(conflict)) return;
            int request = ++generation;
            try
            {
                var local = PersonalClientSync.Snapshot(store, store.GetAll().Find(c => c.Id == conflict.id));
                if (!PersonalClientSync.Same(local, conflict.local)) { next = 0; Refresh(); return; }
                // Keep an on-device recovery copy of both versions before an explicit resolution.
                string recovery = Path.Combine(Path.GetDirectoryName(statePath), "SyncConflicts", Guid.NewGuid().ToString("N") + ".json");
                LocalRecordFile.Write(recovery, Newtonsoft.Json.JsonConvert.SerializeObject(new { conflict.local, conflict.remote }));
                if (useLocal)
                {
                    Busy = true; Conflicts.Remove(conflict);
                    Push(request, conflict.id, local, conflict.remote, () => { Busy = false; next = 0; Notify(); });
                }
                else
                {
                    store.ApplySynced(conflict.remote, local); Acknowledge(conflict.remote); Conflicts.Remove(conflict); next = 0; Notify();
                }
            }
            catch (Exception ex) { Fail(request, ex.Message); }
        }
        void OnApplicationFocus(bool focused) { if (focused) next = 0; }
    }
}
