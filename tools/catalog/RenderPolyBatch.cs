// Copy into an isolated Unity project's Assets/Editor, using the app's UnityGLTF
// package version. Run -batchmode -executeMethod RenderPolyBatch.Run (no -quit).
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF;
using UnityGLTF.Loader;

public static class RenderPolyBatch
{
    class Entry { public string title; }
    class EmbeddedLoader : IDataLoader
    {
        public Task<Stream> LoadStreamAsync(string path) =>
            throw new FileNotFoundException("External GLB resource: " + path);
    }

    public static async void Run()
    {
        string root = Environment.GetEnvironmentVariable("SANDTRAY_POLY_BATCH");
        var results = new List<object>();
        int failures = 0;
        try
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.75f, .75f, .75f);
            RenderSettings.ambientIntensity = 1f;
            var light = new GameObject("Key light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            var fill = new GameObject("Fill light").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .45f;
            fill.transform.rotation = Quaternion.Euler(30, 140, 0);
            var camera = new GameObject("Preview camera").AddComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear; camera.nearClipPlane = .01f;
            camera.farClipPlane = 100f; camera.allowHDR = false;
            var entries = JsonConvert.DeserializeObject<Entry[]>(File.ReadAllText(Path.Combine(root, "shortlist.json")));
            for (int i = 0; i < entries.Length; i++)
            {
                string title = entries[i].title;
                string stem = (i + 1).ToString("000") + "-" +
                    System.Text.RegularExpressions.Regex.Replace(title, "[^A-Za-z0-9_-]+", "-").Trim('-');
                string path = Path.Combine(root, stem + ".glb");
                string source = Path.Combine(root, stem + ".source.json");
                var deadline = DateTime.UtcNow.AddMinutes(15);
                while (!File.Exists(source))
                {
                    if (DateTime.UtcNow > deadline) throw new TimeoutException("Waiting for " + title);
                    await Task.Delay(500);
                }
                GameObject holder = new GameObject(title);
                GLTFSceneImporter importer = null;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    using (var stream = File.OpenRead(path))
                    using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        importer = new GLTFSceneImporter(stream, new ImportOptions {
                            // Batch edit-mode imports should not yield through a
                            // MonoBehaviour whose temporary materials can unload.
                            DataLoader = new EmbeddedLoader()
                        });
                        importer.SceneParent = holder.transform;
                        importer.IsMultithreaded = false;
                        await importer.LoadSceneAsync(showSceneObj: true, cancellationToken: cancellation.Token);
                    }
                    var renderers = holder.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) throw new Exception("No renderers");
                    foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                        if (material == null || material.shader == null || !material.shader.isSupported)
                            throw new Exception("Missing or unsupported material/shader");
                    Bounds bounds = BoundsOf(renderers);
                    float dimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    if (dimension <= .00001f || float.IsNaN(dimension) || float.IsInfinity(dimension))
                        throw new Exception("Invalid model bounds");
                    float scale = .6f / dimension;
                    // Same large-coordinate correction used by NetworkCatalogLoader.
                    var filters = holder.GetComponentsInChildren<MeshFilter>(true);
                    bool bake = scale < .001f && filters.Length > 0 &&
                        !holder.GetComponentInChildren<SkinnedMeshRenderer>(true) &&
                        !holder.GetComponentInChildren<Animator>(true) &&
                        !holder.GetComponentInChildren<Animation>(true) &&
                        filters.All(f => f.sharedMesh == null || (f.sharedMesh.isReadable && f.sharedMesh.blendShapeCount == 0));
                    if (bake)
                    {
                        foreach (var mesh in filters.Select(f => f.sharedMesh).Where(m => m).Distinct())
                        {
                            var vertices = mesh.vertices;
                            for (int v = 0; v < vertices.Length; v++) vertices[v] *= scale;
                            mesh.vertices = vertices; mesh.RecalculateBounds();
                        }
                        foreach (var node in holder.GetComponentsInChildren<Transform>(true))
                            if (node != holder.transform) node.localPosition *= scale;
                    }
                    else holder.transform.localScale *= scale;
                    bounds = BoundsOf(renderers);
                    var normalizedPivotOffset = bounds.center - holder.transform.position;
                    holder.transform.position -= bounds.center;
                    // Exercise the geometry/selection setup used by ObjectPlacer:
                    // fit a root BoxCollider, rotate, and seat the bounds on sand.
                    var fitted = BoundsOf(renderers);
                    var collider = holder.AddComponent<BoxCollider>();
                    collider.center = holder.transform.InverseTransformPoint(fitted.center);
                    var lossy = holder.transform.lossyScale;
                    collider.size = new Vector3(fitted.size.x / lossy.x,
                        fitted.size.y / lossy.y, fitted.size.z / lossy.z);
                    var savedPosition = holder.transform.position;
                    for (int rotation = 0; rotation < 4; rotation++)
                    {
                        holder.transform.rotation = Quaternion.Euler(0, rotation * 90f, 0);
                        var seated = BoundsOf(renderers);
                        holder.transform.position += Vector3.up * -seated.min.y;
                        Physics.SyncTransforms();
                        seated = BoundsOf(renderers);
                        if (Mathf.Abs(seated.min.y) > .001f)
                            throw new Exception("Model cannot seat on the sand plane");
                        var direction = new Vector3(1, 1.5f, -1).normalized;
                        var ray = new Ray(seated.center + direction * 3, -direction);
                        if (!collider.Raycast(ray, out var hit, 10))
                            throw new Exception("Fitted selection collider cannot be raycast at rotation " + rotation * 90);
                    }
                    holder.transform.rotation = Quaternion.identity;
                    holder.transform.position = savedPosition;
                    int triangleCount = filters.Where(f => f.sharedMesh)
                        .Sum(f => f.sharedMesh.triangles.Length / 3);
                    int materialCount = renderers.SelectMany(r => r.sharedMaterials).Distinct().Count();
                    float importMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;
                    var orbit = JObject.Parse(File.ReadAllText(source))["Orbit"];
                    float theta = .87089f, phi = 1.31884f;
                    if (float.TryParse((string)orbit?["theta"], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float sourceTheta)) theta = sourceTheta;
                    if (float.TryParse((string)orbit?["phi"], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float sourcePhi)) phi = sourcePhi;
                    // UnityGLTF mirrors glTF's X axis. Match the source preview's view.
                    camera.transform.position = new Vector3(-Mathf.Sin(theta) * Mathf.Sin(phi),
                        Mathf.Cos(phi), Mathf.Cos(theta) * Mathf.Sin(phi)) * 3f;
                    camera.transform.LookAt(Vector3.zero);
                    float extent = 0f;
                    bounds = BoundsOf(renderers);
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                            (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                            (corner & 4) == 0 ? -1 : 1));
                        var view = camera.transform.InverseTransformPoint(point);
                        extent = Mathf.Max(extent, Mathf.Abs(view.x), Mathf.Abs(view.y));
                    }
                    camera.orthographicSize = extent * 1.12f;
                    var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
                    var previous = RenderTexture.active;
                    camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                    var tex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); tex.Apply();
                    var pixels = tex.GetPixels32();
                    int opaque = 0, dark = 0, magenta = 0;
                    foreach (var p in pixels)
                    {
                        if (p.a < 128) continue;
                        opaque++;
                        if (p.r < 12 && p.g < 12 && p.b < 12) dark++;
                        if (p.r > 220 && p.b > 220 && p.g < 30) magenta++;
                    }
                    if (opaque < 500 || magenta > opaque * .1f)
                        throw new Exception("Empty render or error shader");
                    File.WriteAllBytes(Path.Combine(root, stem + ".png"), tex.EncodeToPNG());
                    results.Add(new { index = i + 1, title, status = "passed", renderer_count = renderers.Length,
                        source_max_dimension = dimension, scale_baked = bake, opaque_pixels = opaque,
                        dark_fraction = dark / (float)opaque, magenta_pixels = magenta,
                        normalized_size = new { x = fitted.size.x, y = fitted.size.y, z = fitted.size.z }, triangle_count = triangleCount,
                        normalized_pivot_offset = new { x = normalizedPivotOffset.x, y = normalizedPivotOffset.y, z = normalizedPivotOffset.z },
                        material_count = materialCount, import_milliseconds = importMilliseconds,
                        ground_placement_rotations_passed = 4, selection_raycast_rotations_passed = 4 });
                    RenderTexture.active = previous; camera.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                    Debug.Log("POLY_RENDER " + (i + 1) + "/" + entries.Length + " " + title + " passed");
                }
                catch (Exception e)
                {
                    failures++;
                    results.Add(new { index = i + 1, title, status = "failed", error = e.ToString() });
                    Debug.LogWarning("POLY_RENDER failed: " + title + " " + e);
                }
                finally
                {
                    importer?.Dispose(); UnityEngine.Object.DestroyImmediate(holder);
                }
                File.WriteAllText(Path.Combine(root, "unity-verification.json"), JsonConvert.SerializeObject(results, Formatting.Indented));
                await Task.Yield();
            }
            File.WriteAllText(Path.Combine(root, "unity-complete.json"), JsonConvert.SerializeObject(new { total = entries.Length, failures }));
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogError(e); EditorApplication.Exit(2);
        }
    }

    static Bounds BoundsOf(Renderer[] renderers)
    {
        Bounds result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
        return result;
    }
}
