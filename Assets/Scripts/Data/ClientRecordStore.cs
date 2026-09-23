using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sandplay.Data
{
    [Serializable]
    public class ClientAccountLink
    {
        public int UserId;
        public string AccountCode;
        public string IdentityCode;
        public string DisplayName;
        public string Backend;
        public string LinkedAt;
    }

    [Serializable]
    public class ClientRecord
    {
        public string Id;
        public string Name;
        public string Reference;
        public ClientAccountLink Account;
        public string DateOfBirth;
        public string PhotoFile;
        public bool AccountPhotoSuppressed;
        public string Email;
        public string Phone;
        public string Notes;
        public bool Archived;
        public string CreatedAt;
        public string UpdatedAt;
    }

    /// <summary>Local client profiles; no login/account is required for a client.</summary>
    public sealed class ClientRecordStore
    {
        [Serializable]
        private class Records { public List<ClientRecord> Clients; }
        private readonly string _path;
        private readonly Action requireCurrent;
        private readonly AggregateCapacityStore fixedCapacity;
        private readonly Func<AggregateCapacityStore> capacityProvider;
        private AggregateCapacityStore ResolveCapacity() => capacityProvider != null ? capacityProvider() : fixedCapacity;
        private readonly Action requireCreation;
        private bool saving;
        public bool RecoveredFromBackup { get; private set; }
        public ClientRecordStore(string directory, Action guard = null, AggregateCapacityStore capacity = null, Action requireCreation = null, Func<AggregateCapacityStore> capacityProvider = null) { _path = Path.Combine(directory, "clients.json"); requireCurrent = guard; this.fixedCapacity=capacity; this.capacityProvider=capacityProvider; this.requireCreation=requireCreation; }

        public List<ClientRecord> GetAll()
        {
            requireCurrent?.Invoke();
            var records = ReadValidRecords(_path);
            if (records != null) return records.Clients;
            var backup = ReadValidRecords(_path + ".bak");
            if (backup != null)
            {
                // Preserve the damaged bytes and the good backup before repair.
                // A subsequent edit must not rotate corrupt data over the backup.
                if (File.Exists(_path)) File.Move(_path, _path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                LocalRecordFile.Write(_path, JsonUtility.ToJson(backup, true));
                RecoveredFromBackup = true;
                Debug.LogWarning("[Clients] Recovered client records from local backup.");
                return backup.Clients;
            }
            if (!File.Exists(_path) && !File.Exists(_path + ".bak")) return new List<ClientRecord>();
            throw new InvalidDataException("Client records could not be read.");
        }

        private static Records ReadValidRecords(string path)
        {
            if (!File.Exists(path)) return null;
            // Permission and disk failures must surface, not masquerade as empty data.
            string json = File.ReadAllText(path);
            Records records;
            try { records = JsonUtility.FromJson<Records>(json); }
            catch (ArgumentException) { return null; }
            if (records?.Clients == null) return null;
            var ids = new HashSet<string>();
            foreach (var record in records.Clients)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Id) ||
                    string.IsNullOrWhiteSpace(record.Name) || !ids.Add(record.Id)) return null;
                // JsonUtility materializes a serialized null inline class as an empty object.
                var account = record.Account;
                if (account != null && account.UserId == 0 && string.IsNullOrEmpty(account.Backend) &&
                    string.IsNullOrEmpty(account.AccountCode) && string.IsNullOrEmpty(account.IdentityCode) &&
                    string.IsNullOrEmpty(account.DisplayName) && string.IsNullOrEmpty(account.LinkedAt))
                    record.Account = null;
            }
            return records;
        }

        public static string[] RecordIds(string json)
        {
            var document=Newtonsoft.Json.Linq.JObject.Parse(json);
            if(!(document["Clients"] is Newtonsoft.Json.Linq.JArray))throw new InvalidDataException("Invalid client inventory.");
            var records=JsonUtility.FromJson<Records>(json);
            if(records?.Clients==null || records.Clients.Any(c=>c==null || string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Name)))throw new InvalidDataException("Invalid client inventory.");
            var ids=records.Clients.Select(c=>c.Id).ToArray();
            if(ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)throw new InvalidDataException("Duplicate client identity.");
            return ids;
        }
        public byte[] ReadPhoto(string filename)
        {
            requireCurrent?.Invoke();
            if (string.IsNullOrEmpty(filename)) return null;
            string path = PhotoPath(filename);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        private string PhotoPath(string filename)
        {
            if (filename.Length != 36 || !filename.EndsWith(".png", StringComparison.Ordinal) ||
                !Guid.TryParseExact(filename.Substring(0, 32), "N", out _))
                throw new ArgumentException("clients.photo_error");
            return Path.Combine(Path.GetDirectoryName(_path), "Photos", filename);
        }

        public void DeleteAsync(string id, Action beforeDelete, Action deleted, Action<string> failed)
        {
            if (saving) { failed?.Invoke("A client change is already in progress."); return; }
            saving = true;
            void Error(string message) { saving = false; failed?.Invoke(message); }
            try
            {
                requireCurrent?.Invoke();
                var capacity = ResolveCapacity();
                var records = GetAll();
                if (!records.Any(c => c.Id == id)) throw new InvalidOperationException("Client no longer exists.");
                var before = records.Select(c => c.Id).ToArray();
                string expected = File.ReadAllText(_path);
                records.RemoveAll(c => c.Id == id);
                void Persist()
                {
                    requireCurrent?.Invoke();
                    if (File.ReadAllText(_path) != expected) throw new InvalidOperationException("Client records changed. Please retry.");
                    beforeDelete?.Invoke();
                    LocalRecordFile.Write(_path, JsonUtility.ToJson(new Records { Clients = records }, true));
                }
                void Done() { saving = false; deleted?.Invoke(); }
                if (capacity != null) capacity.Write("Clients/clients.json", "clients.capacity", before, records.Select(c => c.Id), Persist, Done, Error);
                else { Persist(); Done(); }
            }
            catch (Exception e) { Error(e.Message); }
        }

        public ClientRecord Save(ClientRecord record, byte[] photoPng = null, bool replacePhoto = false)
        {
            return SaveCore(record,photoPng,replacePhoto,false,null,null,false);
        }

        public void SaveAsync(ClientRecord record, byte[] photoPng, bool replacePhoto, bool isNew,
            Action<ClientRecord> saved, Action<string> failed)
        {
            if(saving){failed?.Invoke("A client save is already pending.");return;}
            saving=true;
            try { SaveCore(record,photoPng,replacePhoto,true,c=>{saving=false;saved?.Invoke(c);},e=>{saving=false;failed?.Invoke(e);},isNew); }
            catch(Exception ex){saving=false;failed?.Invoke(ex.Message);}
        }

        private ClientRecord SaveCore(ClientRecord record, byte[] photoPng, bool replacePhoto, bool async,
            Action<ClientRecord> saved, Action<string> failed, bool isNew)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            requireCurrent?.Invoke();
            var capacity = ResolveCapacity();
            if (isNew || string.IsNullOrEmpty(record.Id) || !GetAll().Any(c => c.Id == record.Id)) requireCreation?.Invoke();
            record.Name = (record.Name ?? "").Trim();
            record.Reference = (record.Reference ?? "").Trim();
            record.DateOfBirth = (record.DateOfBirth ?? "").Trim();
            if (record.Name.Length == 0 || record.Name.Length > 100)
                throw new ArgumentException("clients.invalid_name");
            if (record.DateOfBirth.Length > 0 &&
                (!DateTime.TryParseExact(record.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dob) || dob.Date > DateTime.Today))
                throw new ArgumentException("clients.invalid_dob");
            var records = GetAll(); // Never overwrite an unreadable file with an empty list.
            if (records.Exists(c => c.Id != record.Id && record.Reference.Length > 0 &&
                string.Equals(c.Reference, record.Reference, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("clients.duplicate_reference");
            var before=records.Select(c=>c.Id).ToArray();
            string expected=File.Exists(_path)?File.ReadAllText(_path):null;
            var existing = records.Find(c => c.Id == record.Id);
            if (!async && capacity != null && existing == null)
                throw new InvalidOperationException("New clients require the reserved save flow.");
            if (!string.IsNullOrEmpty(record.Id) && existing == null && !isNew)
                throw new InvalidOperationException("Client no longer exists.");
            // Omitted account data on legacy edit paths must never silently unlink a client.
            if (record.Account == null) record.Account = existing?.Account;
            if (record.Account != null)
            {
                var account = record.Account;
                if (account.UserId <= 0 || string.IsNullOrWhiteSpace(account.Backend) ||
                    string.IsNullOrWhiteSpace(account.AccountCode))
                    throw new ArgumentException("clients.invalid_account");
                if (existing?.Account != null && (existing.Account.UserId != account.UserId || existing.Account.Backend != account.Backend))
                    throw new ArgumentException("clients.account_locked");
                if (records.Exists(c => c.Id != record.Id && c.Account != null &&
                    c.Account.UserId == account.UserId && c.Account.Backend == account.Backend))
                    throw new ArgumentException("clients.duplicate_account");
                account.LinkedAt = existing?.Account?.LinkedAt ?? DateTime.UtcNow.ToString("o");
            }
            if (existing != null) records.Remove(existing);
            else if(string.IsNullOrEmpty(record.Id)) record.Id = Guid.NewGuid().ToString("N");
            record.CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow.ToString("o");
            record.UpdatedAt = DateTime.UtcNow.ToString("o");
            // Keep the previous photo for clients.json.bak. Never modify the source library image.
            string newPhotoPath = null;
            string previousPhoto = record.PhotoFile;
            if (replacePhoto)
            {
                record.PhotoFile = "";
                if (photoPng != null && photoPng.Length > 0)
                {
                    record.PhotoFile = Guid.NewGuid().ToString("N") + ".png";
                    newPhotoPath = PhotoPath(record.PhotoFile);
                }
            }
            else record.PhotoFile = existing?.PhotoFile ?? "";
            records.Add(record);
            string content=JsonUtility.ToJson(new Records { Clients = records }, true);
            void Persist()
            {
              requireCurrent?.Invoke();
              if((File.Exists(_path)?File.ReadAllText(_path):null)!=expected)
                  throw new InvalidOperationException("Client records changed while saving; reopen the form before retrying.");
              try
              {
                if (newPhotoPath != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newPhotoPath));
                    File.WriteAllBytes(newPhotoPath, photoPng);
                }
                LocalRecordFile.Write(_path, content);
            }
            catch
            {
                record.PhotoFile = previousPhoto;
                if (!(async && capacity!=null) && newPhotoPath != null && File.Exists(newPhotoPath)) File.Delete(newPhotoPath);
                throw;
            }
            }
            if(async && capacity!=null)
            {
                // Keep the proposed aggregate for review after an interrupted reservation.
                // Photos are preserved alongside it before reserving the logical record slot.
                if(newPhotoPath!=null){Directory.CreateDirectory(Path.GetDirectoryName(newPhotoPath));File.WriteAllBytes(newPhotoPath,photoPng);}
                LocalRecordFile.Write(_path+".interrupted-"+LocalCapacityJournal.Hash(record.Id),content);
                capacity.Write("Clients/clients.json","clients.capacity",before,records.Select(c=>c.Id),Persist,()=>saved?.Invoke(record),failed);
            }
            else {Persist();saved?.Invoke(record);}
            return record;
        }
    }

    internal static class LocalRecordFile
    {
        public static void Write(string path, string json, bool createOnly = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            string lockPath = path + ".write-lock";
            if (File.Exists(lockPath) && (File.GetAttributes(lockPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked record lock.");
            using var held = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (createOnly && (File.Exists(path) || File.Exists(path + ".bak")))
                throw new IOException("A table with this name already exists.");
            // A previous process may have stopped before replacement. Preserve its bytes before the next writer starts.
            if (File.Exists(temporary)) File.Move(temporary, path + ".interrupted-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = new System.Text.UTF8Encoding(false).GetBytes(json);
                    file.Write(bytes, 0, bytes.Length);
                    // Finish writing the new copy before replacing the last valid record.
                    file.Flush(true);
                }
                if (!createOnly && File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
