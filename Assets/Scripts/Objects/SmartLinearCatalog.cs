using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandplay.Objects
{
    /// <summary>Built-in catalog entries and resolution of versioned parametric object IDs.</summary>
    public static class SmartLinearCatalog
    {
        public static readonly string[] DefinitionIds = { "smart.wall.v1", "smart.road.v1", "smart.fence.v1" };
        private static readonly Dictionary<string,GameObject> Templates = new Dictionary<string,GameObject>();
        public static NetworkCatalogItem Item(string id)
        {
            if(!LinearObjectState.TryParse(id,out var state))return null;
            string kind=state.DefinitionId.Split('.')[1];
            return new NetworkCatalogItem { id=state.ObjectId, catalog="local.smart-linear.v1",
                display_name=char.ToUpperInvariant(kind[0])+kind.Substring(1),
                objectSize=ObjectSizing.Recommend("buildings",char.ToUpperInvariant(kind[0])+kind.Substring(1)), category="buildings", model_hash="", model_url="", thumbnail_url="",
                description="Extend or shorten aligned sections from either end.",
                tags=new[]{kind,"smart","extend","sections",kind=="road"?"道路":kind=="wall"?"墙":"围栏"} };
        }
        public static NetworkCatalogItem[] WithDefaults(NetworkCatalogItem[] items)
        {
            var result=new List<NetworkCatalogItem>(items??Array.Empty<NetworkCatalogItem>());
            foreach(string id in DefinitionIds)
                if(!result.Exists(i=>i!=null && i.id==id))result.Add(Item(id));
            return result.ToArray();
        }
        public static bool EnsureTemplate(NetworkCatalogItem item)
        {
            if(item==null || !LinearObjectState.TryParse(item.id,out var state))return false;
            if(!Templates.TryGetValue(state.DefinitionId,out var template) || template==null)
            {
                template=new GameObject(state.DefinitionId); template.SetActive(false);
                template.AddComponent<SmartLinearObject>().Apply(new LinearObjectState{DefinitionId=state.DefinitionId});
                if(Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(template);
                else template.hideFlags=HideFlags.HideAndDontSave;
                Templates[state.DefinitionId]=template;
            }
            item.LoadedPrefab=template;
            return true;
        }
    }
}
