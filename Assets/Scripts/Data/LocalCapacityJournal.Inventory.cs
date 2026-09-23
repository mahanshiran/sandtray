using System;
using System.IO;
namespace Sandplay.Data
{
    public sealed partial class LocalCapacityJournal
    {
        // Explicitly adopt reviewed existing bytes. Never rewrite user content or reset a prior operation.
        public LocalCapacityOperation PrepareInventoryTable(string path,string expectedHash) => Transaction(doc=>
        {
            string full=ResolvePath(path);
            if(!path.StartsWith("Sessions/",StringComparison.Ordinal) || !File.Exists(full) || Hash(File.ReadAllText(full))!=expectedHash)
                throw new InvalidDataException("Reviewed inventory changed.");
            var prior=doc.Operations.Find(x=>x.State!="released" && string.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase));
            if(prior!=null)
            {
                if(prior.Capability!="tables.capacity" || (prior.State!="active" && prior.State!="saved" && !prior.InventoryExisting))
                    throw new InvalidOperationException("Finish the existing table operation before migrating it.");
                if(prior.InventoryExisting && prior.State!="active" && prior.State!="saved" && prior.ContentHash!=expectedHash)
                { prior.ContentHash=expectedHash; Persist(doc); }
                return Copy(prior);
            }
            var op=new LocalCapacityOperation{Id=Guid.NewGuid().ToString(),Path=path,Capability="tables.capacity",State="prepared",ContentHash=expectedHash,InventoryExisting=true};
            doc.Operations.Add(op);Persist(doc);return Copy(op);
        });
        public void ConfirmInventoryTable(string id) => Transaction(doc=>
        {
            var op=Find(doc,id);
            if(op.State=="saved" || op.State=="active")return true;
            if(!op.InventoryExisting || !op.HasLease || op.State!="leased" || Hash(File.ReadAllText(ResolvePath(op.Path)))!=op.ContentHash)
                throw new InvalidDataException("Inventory receipt or reviewed file changed; preserve the reservation.");
            // Existing records predate this lease: reconcile rather than requiring an unexpired first-write lease.
            op.State="saved";Persist(doc);return true;
        });
    }
}
