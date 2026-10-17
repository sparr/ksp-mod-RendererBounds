using System;
using System.Collections.Generic;
using QuickHull3D;
using UnityEngine;

namespace RendererBounds
{
    /// <summary>
    /// Reduces a mesh's vertices to the ones that can be extreme in some direction,
    /// and measures a point set's axis-aligned extents under a transform.
    /// </summary>
    /// <remarks>
    /// Uses only managed UnityEngine value types, so the unit tests can run it
    /// outside the game.
    /// </remarks>
    public static class MeshHull
    {

        /// <summary>
        /// The convex hull vertices of <paramref name="vertices"/>. The axis-aligned
        /// extents of the result under any affine transform equal those of the input,
        /// to within QuickHull3D's tolerance, which is a few multiples of double
        /// precision relative to the coordinates' magnitude.
        /// </summary>
        /// <remarks>
        /// QuickHull3D merges coplanar faces, so a vertex in the middle of a flat face
        /// or a straight edge is never part of the hull. It throws
        /// <see cref="ArgumentException"/> for input with no 3D hull: fewer than four
        /// points, or all of them coincident, collinear, or coplanar within its
        /// tolerance, as with decals and other flat meshes. Then the distinct input
        /// vertices are returned. Any other exception sets <paramref name="error"/>,
        /// and the distinct vertices are returned too. Non-finite vertices are
        /// ignored.
        /// </remarks>
        public static Vector3[] HullPoints(IList<Vector3> vertices, out string error)
        {
            error = null;
            var distinct = new HashSet<Vector3>();
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                if (IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z))
                    distinct.Add(v);
            }
            var points = new Vector3[distinct.Count];
            distinct.CopyTo(points);
            if (points.Length < 4)
                return points;

            var coords = new double[points.Length * 3];
            for (int i = 0; i < points.Length; i++)
            {
                coords[i * 3] = points[i].x;
                coords[i * 3 + 1] = points[i].y;
                coords[i * 3 + 2] = points[i].z;
            }

            int[] indices;
            try
            {
                var hull = new Hull();
                hull.Build(coords);
                indices = hull.GetVertexPointIndices();
            }
            catch (ArgumentException)
            {
                // No 3D hull. Every vertex of a flat mesh is a candidate extreme.
                return points;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return points;
            }
            if (indices.Length == 0)
                return points;

            // The indices refer to the input points, so the result is the original
            // float vertices, not values converted back from doubles.
            var result = new Vector3[indices.Length];
            for (int i = 0; i < indices.Length; i++)
                result[i] = points[indices[i]];
            return result;
        }

        private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        /// <summary>
        /// The axis-aligned box around <paramref name="points"/> transformed by
        /// <paramref name="m"/>. Points that come out non-finite are skipped, as a
        /// skinned mesh baked from a broken bone transform can have. False when none
        /// is finite.
        /// </summary>
        public static bool TryExtents(Vector3[] points, Matrix4x4 m, out Bounds bounds)
        {
            var box = new Box();
            for (int i = 0; i < points.Length; i++)
                box.Add(m.MultiplyPoint3x4(points[i]));
            return box.TryGet(out bounds);
        }

        /// <inheritdoc cref="TryExtents(Vector3[], Matrix4x4, out Bounds)"/>
        public static bool TryExtents(List<Vector3> points, Matrix4x4 m, out Bounds bounds)
        {
            var box = new Box();
            for (int i = 0; i < points.Count; i++)
                box.Add(m.MultiplyPoint3x4(points[i]));
            return box.TryGet(out bounds);
        }

        private struct Box
        {
            private Vector3 min, max;
            private bool any;

            public void Add(Vector3 p)
            {
                if (!IsFinite(p.x) || !IsFinite(p.y) || !IsFinite(p.z))
                    return;
                if (any)
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                else
                {
                    min = max = p;
                    any = true;
                }
            }

            public bool TryGet(out Bounds bounds)
            {
                bounds = new Bounds();
                if (any)
                    bounds.SetMinMax(min, max);
                return any;
            }
        }
    }
}
