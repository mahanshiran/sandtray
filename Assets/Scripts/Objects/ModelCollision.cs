using System.Collections.Generic;
using UnityEngine;

namespace Sandplay.Objects
{
    // Static placed models share their render mesh with PhysX. No convex hull: gaps stay open.
    public sealed class ModelCollision : MonoBehaviour
    {
        private readonly List<Mesh> bakedMeshes = new List<Mesh>();

        public static void Install(GameObject root)
        {
            if (root.GetComponent<ModelCollision>() != null) return;
            var owner = root.AddComponent<ModelCollision>();
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    mesh = new Mesh { name = "Static collision pose" };
                    skin.BakeMesh(mesh);
                    owner.bakedMeshes.Add(mesh);
                }
                else if (renderer is MeshRenderer)
                    mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0) continue;
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.convex = false;
                collider.sharedMesh = mesh;
            }
        }

        private void OnDestroy()
        {
            foreach (var mesh in bakedMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
