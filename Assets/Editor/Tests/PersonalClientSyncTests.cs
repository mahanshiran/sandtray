using NUnit.Framework;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class PersonalClientSyncTests
    {
        static PersonalClientSyncItem Item(string value)
        {
            if(value==null)return null;
            return new PersonalClientSyncItem { id="client", deleted=value=="deleted",
                record_json=value=="deleted"?"":"{\"Name\":\""+value+"\",\"Id\":\"client\"}" };
        }
        [TestCase(null,null,"base",PersonalClientSync.Decision.Download)]
        [TestCase(null,"base",null,PersonalClientSync.Decision.Upload)]
        [TestCase("base","local","base",PersonalClientSync.Decision.Upload)]
        [TestCase("base","base","cloud",PersonalClientSync.Decision.Download)]
        [TestCase("base","local","cloud",PersonalClientSync.Decision.Conflict)]
        [TestCase("base",null,"base",PersonalClientSync.Decision.Upload)]
        [TestCase("base","base","deleted",PersonalClientSync.Decision.Download)]
        [TestCase("base","local","deleted",PersonalClientSync.Decision.Conflict)]
        [TestCase("base",null,"cloud",PersonalClientSync.Decision.Conflict)]
        [TestCase(null,"base","deleted",PersonalClientSync.Decision.Conflict)]
        [TestCase("base","cloud","cloud",PersonalClientSync.Decision.Equal)]
        [TestCase("base",null,"deleted",PersonalClientSync.Decision.Equal)]
        [TestCase(null,null,"deleted",PersonalClientSync.Decision.Equal)]
        [TestCase(null,"local","cloud",PersonalClientSync.Decision.Conflict)]
        public void MergePreservesOfflineChangesAndDeletionHistory(string baseline,string local,string remote,PersonalClientSync.Decision expected)
        { Assert.AreEqual(expected,PersonalClientSync.Decide(Item(baseline),Item(local),Item(remote))); }

        [Test] public void DifferentPhotosConflictEvenWhenTextMatches()
        {
            var baseline=Item("base");var local=Item("base");var remote=Item("base");
            local.photo_base64="local";remote.photo_base64="remote";
            Assert.AreEqual(PersonalClientSync.Decision.Conflict,PersonalClientSync.Decide(baseline,local,remote));
        }
    }
}
