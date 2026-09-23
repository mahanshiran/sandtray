using System;
using System.IO;
namespace Sandplay.Data
{
    public sealed partial class LocalCapacityJournal
    {
        // Only metadata sidecars are removed here; the owning adapter already persisted the aggregate deletion.
        public void DeleteSlotFile(string id) => Transaction(doc =>
        {
            var op=Find(doc,id);
            if(op.State!="deleting" || !op.Path.StartsWith("RecordSlots/",StringComparison.Ordinal))
                throw new InvalidOperationException("Only a deleting logical-record slot can be removed.");
            string path=ResolvePath(op.Path);
            if(File.Exists(path+".bak"))File.Delete(path+".bak");
            if(File.Exists(path))File.Delete(path);
            return true;
        });
    }
}
