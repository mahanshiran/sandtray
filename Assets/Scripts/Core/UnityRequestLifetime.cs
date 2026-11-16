using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Sandplay.Core
{
    /// <summary>Own requests across coroutine cancellation and editor domain reloads.</summary>
    public sealed class UnityRequestLifetime : IDisposable
    {
        private static readonly HashSet<UnityRequestLifetime> Active = new();
        public static bool IsStopping { get; private set; }
        private UnityWebRequest request;
        private readonly MonoBehaviour owner;
        private UnityRequestLifetime(UnityWebRequest value,MonoBehaviour sender)
        {request=value;owner=sender;Active.Add(this);}
        public static UnityRequestLifetime Track(UnityWebRequest request,MonoBehaviour owner)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            return new UnityRequestLifetime(request,owner);
        }
        public void Dispose()
        {
            Active.Remove(this);
            var value=request;request=null;
            if(value==null)return;
            try{if(!value.isDone)value.Abort();}
            finally{value.Dispose();}
        }
        public static void Shutdown()
        {
            if(IsStopping)return;
            IsStopping=true;
            var requests=new List<UnityRequestLifetime>(Active);
            var owners=new HashSet<MonoBehaviour>();
            foreach(var lease in requests)if(lease.owner!=null)owners.Add(lease.owner);
            // Prevent an aborted request from resuming a coroutine with callbacks
            // that belong to a scene/account which is already being torn down.
            foreach(var owner in owners)if(owner!=null)owner.StopAllCoroutines();
            foreach(var lease in requests)
            {
                try{lease.Dispose();}
                catch(Exception ex){Debug.LogWarning("[HTTP cleanup] "+ex.GetType().Name);}
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Shutdown();IsStopping=false;
            Application.quitting-=Shutdown;Application.quitting+=Shutdown;
        }
    }
}
