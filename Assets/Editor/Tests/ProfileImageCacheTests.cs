using System;
using NUnit.Framework;
using Sandplay.Core;
namespace Sandplay.Tests
{
    public class ProfileImageCacheTests
    {
        [Test] public void SharesInFlightRequestAndReusesBytesUntilExpiry()
        {
            double now=0; int calls=0, delivered=0;
            var cache=new ProfileImageCache(()=>now);
            Action<byte[],double> complete=null;
            Action<Action<byte[],double>> fetch=done=>{calls++;complete=done;};
            cache.Get("a","photo",fetch,b=>delivered++);
            cache.Get("a","photo",fetch,b=>delivered++);
            Assert.AreEqual(1,calls);
            complete(new byte[]{1},60);Assert.AreEqual(2,delivered);
            cache.Get("a","photo",fetch,b=>Assert.AreEqual(1,b[0]));Assert.AreEqual(1,calls);
            now=61;cache.Get("a","photo",fetch,b=>{});Assert.AreEqual(2,calls);
        }
        [Test] public void AccountSwitchAndInvalidationRejectLateResponses()
        {
            var cache=new ProfileImageCache(()=>0);Action<byte[],double> old=null;int delivered=0;
            cache.Get("a","photo",d=>old=d,b=>delivered++);
            cache.Get("b","photo",d=>d(new byte[]{2},300),b=>Assert.AreEqual(2,b[0]));
            old(new byte[]{1},300);Assert.AreEqual(0,delivered);
            cache.Clear();int calls=0;
            cache.Get("b","photo",d=>{calls++;d(new byte[]{3},300);},b=>Assert.AreEqual(3,b[0]));
            Assert.AreEqual(1,calls);
        }
        [Test] public void FailureBackoffAndAbandonedRequestsCanRetry()
        {
            double now=0;int calls=0;var cache=new ProfileImageCache(()=>now);
            Action<Action<byte[],double>> fail=d=>{calls++;d(null,30);};
            cache.Get("a","photo",fail,b=>Assert.IsNull(b));
            cache.Get("a","photo",fail,b=>Assert.IsNull(b));Assert.AreEqual(1,calls);
            now=31;cache.Get("a","photo",fail,b=>{});Assert.AreEqual(2,calls);
            cache.Get("a","other",d=>calls++,b=>{});now=77;
            cache.Get("a","other",fail,b=>{});Assert.AreEqual(4,calls);
        }
        [Test] public void MemoryAndEntryLimitsEvictOldImages()
        {
            double now=0;var cache=new ProfileImageCache(()=>now,2,4);int calls=0;
            Action<Action<byte[],double>> fetch=d=>{calls++;d(new byte[3],300);};
            cache.Get("a","one",fetch,b=>{});now++;
            cache.Get("a","two",fetch,b=>{});now++;
            cache.Get("a","one",fetch,b=>{});Assert.AreEqual(3,calls);
        }
        [Test] public void NoStoreDoesNotReuseResponseAndHeadersLimitFreshness()
        {
            int calls=0;var cache=new ProfileImageCache(()=>0);
            Action<Action<byte[],double>> fetch=d=>{calls++;d(new byte[]{1},0);};
            cache.Get("a","photo",fetch,b=>{});cache.Get("a","photo",fetch,b=>{});
            Assert.AreEqual(2,calls);
            Assert.AreEqual(0,ProfileImageCache.Lifetime("private, no-store",null));
            Assert.AreEqual(0,ProfileImageCache.Lifetime("no-cache",null));
            Assert.AreEqual(40,ProfileImageCache.Lifetime("private, max-age=60","20"));
            Assert.AreEqual(300,ProfileImageCache.Lifetime("max-age=3600",null));
        }
    }
}
