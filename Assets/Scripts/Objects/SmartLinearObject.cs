using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Data;

namespace Sandplay.Objects
{
    [Serializable]
    public sealed class LinearObjectState
    {
        public string DefinitionId;
        public int FirstSegment;
        public int SegmentCount = 1;
        public LinearObjectState Copy() => new LinearObjectState { DefinitionId = DefinitionId, FirstSegment = FirstSegment, SegmentCount = SegmentCount };
        public string ObjectId => FirstSegment == 0 && SegmentCount == 1 ? DefinitionId :
            DefinitionId + ":" + FirstSegment.ToString(CultureInfo.InvariantCulture) + ":" + SegmentCount.ToString(CultureInfo.InvariantCulture);
        public static bool TryParse(string id, out LinearObjectState state)
        {
            state = null;
            if (string.IsNullOrEmpty(id) || id.Length > 100) return false;
            var parts = id.Split(':');
            if (parts[0] != "smart.wall.v1" && parts[0] != "smart.road.v1" && parts[0] != "smart.fence.v1") return false;
            int first = 0, count = 1;
            if (parts.Length != 1 && (parts.Length != 3 ||
                !int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out first) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out count))) return false;
            if (count < 1 || count > 128 || first < -4096 || first > 4096 || (long)first + count > 4096) return false;
            state = new LinearObjectState { DefinitionId = parts[0], FirstSegment = first, SegmentCount = count };
            return true;
        }
    }

    /// <summary>One placed object whose aligned geometry is generated locally.</summary>
    public sealed class SmartLinearObject : MonoBehaviour
    {
        public LinearObjectState State;
        private static Material wall, fence, asphalt, marking;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private void ReleaseMeshes()
        {
            foreach (var mesh in ownedMeshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            ownedMeshes.Clear();
        }
        private void OnDestroy() => ReleaseMeshes();
        private void CombineSurfaces(Transform geometry)
        {
            var groups = new Dictionary<Material,List<CombineInstance>>();
            var renderers = geometry.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var material = renderer.sharedMaterial;
                if (!groups.TryGetValue(material, out var parts)) groups[material] = parts = new List<CombineInstance>();
                parts.Add(new CombineInstance { mesh=filter.sharedMesh, transform=geometry.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            foreach (var group in groups)
            {
                var mesh = new Mesh { name=State.DefinitionId + " surface" };
                mesh.CombineMeshes(group.Value.ToArray(), true, true);
                ownedMeshes.Add(mesh);
                var surface = new GameObject("Surface", typeof(MeshFilter), typeof(MeshRenderer));
                surface.transform.SetParent(geometry,false);
                surface.GetComponent<MeshFilter>().sharedMesh=mesh;
                surface.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            // Retain fitted BoxColliders and their transforms, but render each
            // material once instead of issuing one draw for every picket/section.
            foreach (var renderer in renderers)
            {
                renderer.enabled=false;
                var filter=renderer.GetComponent<MeshFilter>();
                if (Application.isPlaying) { Destroy(renderer); Destroy(filter); }
                else { DestroyImmediate(renderer); DestroyImmediate(filter); }
            }
        }
        public const float SectionLength = .6f;
        public static bool AwaitingConfirmation(PlacedObject placed) => placed != null &&
            placed.GetComponent<SmartLinearObject>() != null && placed.NetworkId == 0 &&
            NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline && !NetworkBootstrapper.Instance.IsHost;
        public static Bounds LocalBounds(LinearObjectState state)
        {
            bool road = state.DefinitionId == "smart.road.v1";
            float height = road ? .025f : .42f;
            float depth = road ? .38f : state.DefinitionId == "smart.wall.v1" ? .075f : .06f;
            float extra = state.DefinitionId == "smart.fence.v1" ? .035f : 0f;
            return new Bounds(new Vector3((state.FirstSegment + state.SegmentCount * .5f) * SectionLength, height * .5f, 0),
                new Vector3(state.SegmentCount * SectionLength + extra, height, depth));
        }
        public bool FitsTray(LinearObjectState state) => FitsTray(state, transform.localToWorldMatrix);
        public static bool FitsTray(LinearObjectState state, Matrix4x4 matrix)
        {
            var config = GameManager.Instance != null ? GameManager.Instance.Config : null;
            if (config == null) return true;
            var bounds = LocalBounds(state);
            float halfW = config.SandboxWidth * .5f - .02f, halfD = config.SandboxDepth * .5f - .02f;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var point = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x,y,z)));
                if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) ||
                    float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z)) return false;
                if (config.CircularTray ? new Vector2(point.x,point.z).magnitude > Mathf.Min(halfW,halfD) :
                    Mathf.Abs(point.x) > halfW || Mathf.Abs(point.z) > halfD) return false;
            }
            return true;
        }
        public bool Apply(LinearObjectState state)
        {
            if (state == null || !LinearObjectState.TryParse(state.ObjectId, out var validated)) return false;
            if (State != null && State.ObjectId == validated.ObjectId && transform.Find("GeneratedGeometry") != null) return true;
            State = validated;
            var previous = transform.Find("GeneratedGeometry");
            if (previous != null) { previous.name="DiscardedGeometry"; previous.gameObject.SetActive(false); if(Application.isPlaying) Destroy(previous.gameObject); else DestroyImmediate(previous.gameObject); }
            ReleaseMeshes();
            var geometry = new GameObject("GeneratedGeometry"); geometry.transform.SetParent(transform,false);
            EnsureMaterials();
            bool isWall = State.DefinitionId == "smart.wall.v1", isRoad = State.DefinitionId == "smart.road.v1";
            for (int i = 0; i < State.SegmentCount; i++)
            {
                float x = (State.FirstSegment+i+.5f)*SectionLength;
                if (isWall)
                {
                    Part(geometry.transform,"Wall",new Vector3(x,.195f,0),new Vector3(SectionLength,.39f,.06f),wall);
                    Part(geometry.transform,"Coping",new Vector3(x,.405f,0),new Vector3(SectionLength,.03f,.075f),wall);
                }
                else if (isRoad)
                {
                    Part(geometry.transform,"Road",new Vector3(x,.01f,0),new Vector3(SectionLength,.02f,.38f),asphalt);
                    Part(geometry.transform,"Center line",new Vector3(x,.021f,0),new Vector3(.30f,.002f,.008f),marking,false);
                    foreach(float edge in new[]{-.17f,.17f}) Part(geometry.transform,"Edge line",new Vector3(x,.021f,edge),new Vector3(SectionLength,.002f,.005f),marking,false);
                }
                else
                {
                    foreach(float y in new[]{.13f,.29f}) Part(geometry.transform,"Rail",new Vector3(x,y,0),new Vector3(SectionLength,.035f,.025f),fence);
                    for(int p=0;p<5;p++) Part(geometry.transform,"Picket",new Vector3(x-SectionLength*.5f+(p+.5f)*SectionLength/5,.21f,0),new Vector3(.04f,.38f,.025f),fence);
                }
            }
            if (!isRoad && !isWall)
                for(int i=0;i<=State.SegmentCount;i++) Part(geometry.transform,"Post",new Vector3((State.FirstSegment+i)*SectionLength,.21f,0),new Vector3(.035f,.42f,.045f),fence);
            CombineSurfaces(geometry.transform);
            var placed = GetComponent<PlacedObject>();
            if (placed != null)
            {
                var item = SmartLinearCatalog.Item(State.ObjectId);
                item.LoadedPrefab = placed.NetworkItem?.LoadedPrefab;
                if (item.LoadedPrefab == null) SmartLinearCatalog.EnsureTemplate(item);
                placed.NetworkItem = item;
                placed.RefreshGeometry();
            }
            return true;
        }
        private static void EnsureMaterials()
        {
            if (wall != null) return;
            Material Make(string name, Color color) { var m = new Material(Shader.Find("Standard")); m.name=name; m.color=color; m.SetFloat("_Glossiness",.18f); m.enableInstancing=true; return m; }
            wall=Make("Smart wall stone",new Color(.72f,.66f,.56f));
            fence=Make("Smart fence wood",new Color(.48f,.27f,.12f));
            asphalt=Make("Smart road asphalt",new Color(.17f,.20f,.23f));
            marking=Make("Smart road marking",new Color(.95f,.90f,.65f));
        }
        private static void Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool collision=true)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube); part.name=name;
            part.transform.SetParent(parent,false); part.transform.localPosition=position; part.transform.localScale=scale;
            part.GetComponent<Renderer>().sharedMaterial=material;
            part.GetComponent<BoxCollider>().enabled=collision;
        }
        public bool Change(int leftDelta,int rightDelta)
        {
            if (State == null || (GameManager.Instance != null && GameManager.Instance.IsSpectator)) return false;
            var placed=GetComponent<PlacedObject>();
            if(NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline && (placed==null || placed.NetworkId==0)) return false;
            var next=State.Copy(); next.FirstSegment-=leftDelta; next.SegmentCount+=leftDelta+rightDelta;
            if (!LinearObjectState.TryParse(next.ObjectId,out _) || !FitsTray(next)) return false;
            var command=new ChangeLinearObjectCommand(this,State.Copy(),next);
            if(UndoManager.Instance != null) UndoManager.Instance.Execute(command); else command.Execute();
            return true;
        }
    }

    public sealed class ChangeLinearObjectCommand : ICommand
    {
        private readonly SmartLinearObject original; private readonly string instanceId;
        private SmartLinearObject target => original != null ? original : PlacedObject.ResolveInstance(instanceId)?.GetComponent<SmartLinearObject>();
        private readonly LinearObjectState before,after;
        public ChangeLinearObjectCommand(SmartLinearObject target,LinearObjectState before,LinearObjectState after)
        { original=target; instanceId=target.GetComponent<PlacedObject>()?.InstanceId; this.before=before.Copy(); this.after=after.Copy(); }
        private void Apply(LinearObjectState state)
        {
            if(target==null)return;
            target.Apply(state);
            var placed=target.GetComponent<PlacedObject>();
            if(placed!=null)
            {
                bool previous = ObjectSyncManager.SuppressNetworkSync;
                ObjectSyncManager.SuppressNetworkSync = true;
                try { EventBus.Publish(new ObjectTransformedEvent { PlacedObject = placed }); }
                finally { ObjectSyncManager.SuppressNetworkSync = previous; }
                NetworkBootstrapper.Instance?.SendLinearObjectState(placed);
            }
        }
        public void Execute()=>Apply(after);
        public void Undo()=>Apply(before);
    }
}
