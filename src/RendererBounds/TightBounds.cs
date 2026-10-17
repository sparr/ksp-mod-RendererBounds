using System.Collections.Generic;
using UnityEngine;

namespace RendererBounds
{
    /// <summary>
    /// World-space bounds of a renderer measured from its mesh's vertices, in place of
    /// <see cref="Renderer.bounds"/>.
    /// </summary>
    public static class TightBounds
    {
        private struct Entry
        {
            public int VertexCount;
            public Bounds LocalBounds;
            public Vector3[] Hull;
        }

        /// <summary>Hulls by mesh instance ID. IDs are not reused within a session.</summary>
        private static readonly Dictionary<int, Entry> Hulls = new Dictionary<int, Entry>();
        private static readonly HashSet<int> Reported = new HashSet<int>();
        private static readonly List<Vector3> BakedVerts = new List<Vector3>();
        private static Mesh baked;

        private static bool settingsRead;

        /// <summary>
        /// When false, every call returns <see cref="Renderer.bounds"/> unchanged. Set
        /// from <c>RENDERER_BOUNDS { enabled }</c> on the first call after the game
        /// database has loaded, and settable at runtime for comparisons.
        /// </summary>
        public static bool Enabled = true;

        /// <summary>Called in place of <see cref="Renderer.bounds"/> by the patched methods.</summary>
        public static Bounds Of(Renderer renderer)
        {
            if (!settingsRead)
                ReadSettings();

            // A null or destroyed renderer throws here exactly as the original call did.
            if (!Enabled || renderer == null)
                return renderer.bounds;

            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                Vector3[] hull = mesh != null ? HullOf(mesh) : null;
                if (hull == null || hull.Length == 0)
                    return renderer.bounds;
                return MeshHull.TryExtents(hull, renderer.transform.localToWorldMatrix, out Bounds b) ? b : renderer.bounds;
            }

            if (renderer is SkinnedMeshRenderer skinned)
            {
                if (skinned.sharedMesh == null)
                    return renderer.bounds;
                if (baked == null)
                    baked = new Mesh();
                // In Unity 2019.4 the baked vertices already include the transform's
                // scale, so only position and rotation are applied here.
                skinned.BakeMesh(baked);
                baked.GetVertices(BakedVerts);
                if (BakedVerts.Count == 0)
                    return renderer.bounds;
                Transform t = renderer.transform;
                if (MeshHull.TryExtents(BakedVerts, Matrix4x4.TRS(t.position, t.rotation, Vector3.one), out Bounds b))
                    return b;
                if (Reported.Add(skinned.sharedMesh.GetInstanceID()))
                    Debug.Log($"[RendererBounds] skinned mesh '{skinned.sharedMesh.name}' baked to no finite vertices, using Renderer.bounds");
                return renderer.bounds;
            }

            return renderer.bounds;
        }

        /// <summary>
        /// The cached hull of <paramref name="mesh"/>, recomputed when its vertex count
        /// or local bounds change, as they do when procedural parts rebuild a mesh in
        /// place. Null when the mesh cannot be read.
        /// </summary>
        private static Vector3[] HullOf(Mesh mesh)
        {
            int id = mesh.GetInstanceID();
            int count = mesh.vertexCount;
            Bounds local = mesh.bounds;
            if (Hulls.TryGetValue(id, out Entry entry) && entry.VertexCount == count && entry.LocalBounds == local)
                return entry.Hull;

            Vector3[] hull = null;
            if (mesh.isReadable && count > 0)
            {
                hull = MeshHull.HullPoints(mesh.vertices, out string error);
                if (error != null && Reported.Add(id))
                    Debug.Log($"[RendererBounds] no hull for mesh '{mesh.name}', using its {hull.Length} distinct vertices: {error}");
            }
            else if (Reported.Add(id))
            {
                Debug.Log($"[RendererBounds] mesh '{mesh.name}' is not readable, using Renderer.bounds");
            }
            Hulls[id] = new Entry { VertexCount = count, LocalBounds = local, Hull = hull };
            return hull;
        }

        /// <summary>
        /// Reads the settings once the game database is ready. Part compilation, the
        /// first caller, runs after that, so the setting applies to every part.
        /// </summary>
        private static void ReadSettings()
        {
            if (GameDatabase.Instance == null || !GameDatabase.Instance.IsReady())
                return;
            settingsRead = true;
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("RENDERER_BOUNDS");
            bool enabled = true;
            if (nodes.Length > 0 && nodes[0].TryGetValue("enabled", ref enabled))
                Enabled = enabled;
            Debug.Log($"[RendererBounds] enabled={Enabled}");
        }

        /// <summary>Drops every cached hull. Meshes destroyed with a scene leave entries behind otherwise.</summary>
        public static void Clear()
        {
            Hulls.Clear();
        }

        /// <summary>Number of cached hulls, for diagnostics.</summary>
        public static int CachedCount => Hulls.Count;
    }
}
