using System;
using System.Linq;

namespace Sandplay.Data
{
    /// <summary>Explicitly release orphaned slots, including unknown allocation outcomes and partial batches.</summary>
    public sealed class AggregateCapacityRecovery
    {
        private readonly LocalCapacityJournal journal;
        private readonly LocalCapacitySync sync;
        private bool running;
        public AggregateCapacityRecovery(LocalCapacityJournal journal,Action<LocalCapacityRequest,Action<LocalCapacityReceipt>,Action<string>> send)
        {this.journal=journal;sync=new LocalCapacitySync(journal,send);}
        public void Release(string id,Action done,Action<string> failed)
        {
            if(running){failed?.Invoke("Recovery is already running.");return;}
            running=true;
            void Fail(string error){running=false;failed?.Invoke(error);}
            void Finish(){running=false;done?.Invoke();}
            void ReleaseKnown()
            {
                try{journal.WithAggregateLock(()=>journal.PrepareAggregateRelease(id));}
                catch(Exception ex){Fail(ex.Message);return;}
                sync.Retry(id,Finish,Fail);
            }
            try
            {
                var op=journal.Read().First(x=>x.Id==id);
                if(op.State=="released"){Finish();return;}
                if(!journal.CanReleaseAggregateSlot(id))throw new InvalidOperationException("The saved record still uses this slot.");
                if(op.State=="prepared")sync.Retry(id,ReleaseKnown,Fail);
                else ReleaseKnown();
            }
            catch(Exception ex){Fail(ex.Message);}
        }
    }
}
