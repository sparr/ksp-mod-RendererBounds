using System;
using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace RendererBounds.Tests
{
    public class MeshHullTests
    {
        private const float Tol = 1e-4f;

        /// <summary>Rotation about a unit axis as a matrix, built without Unity's native code.</summary>
        private static Matrix4x4 Rotation(Vector3 axis, float degrees, Vector3 translation = default, float scale = 1f)
        {
            axis = axis.normalized;
            double a = degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a), t = 1f - c;
            float x = axis.x, y = axis.y, z = axis.z;
            return new Matrix4x4(
                new Vector4(t * x * x + c, t * x * y + s * z, t * x * z - s * y, 0f) * scale,
                new Vector4(t * x * y - s * z, t * y * y + c, t * y * z + s * x, 0f) * scale,
                new Vector4(t * x * z + s * y, t * y * z - s * x, t * z * z + c, 0f) * scale,
                new Vector4(translation.x, translation.y, translation.z, 1f));
        }

        /// <summary>A capped cylinder along Y, as a mesh would list its vertices, with seam duplicates.</summary>
        private static List<Vector3> Cylinder(float radius, float height, int segments)
        {
            var v = new List<Vector3>();
            for (int i = 0; i <= segments; i++)
            {
                double a = 2 * Math.PI * i / segments;
                var p = new Vector3((float)(radius * Math.Cos(a)), 0f, (float)(radius * Math.Sin(a)));
                v.Add(p + Vector3.up * (height / 2));
                v.Add(p - Vector3.up * (height / 2));
            }
            v.Add(Vector3.up * (height / 2));
            v.Add(-Vector3.up * (height / 2));
            return v;
        }

        private static Bounds Extents(IList<Vector3> points, Matrix4x4 m)
        {
            Assert.True(MeshHull.TryExtents(new List<Vector3>(points), m, out Bounds b));
            return b;
        }

        /// <summary>Extents of every vertex, the answer the hull must reproduce.</summary>
        private static Bounds BruteForce(IList<Vector3> points, Matrix4x4 m)
        {
            var all = new List<Vector3>(points);
            return Extents(all, m);
        }

        private static void AssertSame(Bounds expected, Bounds actual)
        {
            Assert.InRange((expected.min - actual.min).magnitude, 0f, Tol);
            Assert.InRange((expected.max - actual.max).magnitude, 0f, Tol);
        }

        /// <summary>What Renderer.bounds does: transform the local box's corners and box them again.</summary>
        private static Bounds RendererStyle(IList<Vector3> points, Matrix4x4 m)
        {
            Bounds local = BruteForce(points, Matrix4x4.identity);
            var corners = new List<Vector3>();
            for (int i = 0; i < 8; i++)
                corners.Add(new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z));
            return Extents(corners, m);
        }

        [Fact]
        public void RotatedCylinderMeasuresItsDiameter()
        {
            List<Vector3> cylinder = Cylinder(1f, 2f, 64);
            Matrix4x4 m = Rotation(Vector3.up, 45f);

            Bounds tight = Extents(MeshHull.HullPoints(cylinder, out string error), m);
            Bounds inflated = RendererStyle(cylinder, m);

            Assert.Null(error);
            Assert.InRange(tight.size.x, 1.99f, 2.001f);
            Assert.InRange(tight.size.z, 1.99f, 2.001f);
            Assert.InRange(inflated.size.x, 2.82f, 2.83f);
        }

        [Fact]
        public void HullExtentsMatchAllVerticesUnderAnyTransform()
        {
            var rng = new System.Random(1);
            var points = new List<Vector3>();
            for (int i = 0; i < 2000; i++)
                points.Add(new Vector3((float)rng.NextDouble() * 3f - 1f, (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() * 2f - 4f));
            Vector3[] hull = MeshHull.HullPoints(points, out string error);
            Assert.Null(error);
            Assert.True(hull.Length < points.Count / 4, $"hull kept {hull.Length} of {points.Count} points");

            for (int k = 0; k < 50; k++)
            {
                var axis = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
                Matrix4x4 m = Rotation(axis, (float)rng.NextDouble() * 360f, new Vector3(5f, -3f, 2f), 1.6f);
                AssertSame(BruteForce(points, m), Extents(hull, m));
            }
        }

        [Fact]
        public void TightBoundsNeverExceedRendererStyleBounds()
        {
            List<Vector3> cylinder = Cylinder(0.5f, 3f, 24);
            Vector3[] hull = MeshHull.HullPoints(cylinder, out _);
            for (int deg = 0; deg < 360; deg += 7)
            {
                Matrix4x4 m = Rotation(new Vector3(1f, 2f, 0.5f), deg);
                Bounds tight = Extents(hull, m), loose = RendererStyle(cylinder, m);
                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.True(tight.min[axis] >= loose.min[axis] - Tol, $"min[{axis}] at {deg} degrees");
                    Assert.True(tight.max[axis] <= loose.max[axis] + Tol, $"max[{axis}] at {deg} degrees");
                }
            }
        }

        [Fact]
        public void FlatMeshFallsBackToItsVertices()
        {
            // A decal: a 10 x 10 grid in one plane, which has no 3D hull.
            var grid = new List<Vector3>();
            for (int i = 0; i < 10; i++)
                for (int j = 0; j < 10; j++)
                    grid.Add(new Vector3(i * 0.1f, j * 0.1f, 0.25f));
            Vector3[] hull = MeshHull.HullPoints(grid, out string error);

            Assert.Null(error);
            Assert.Equal(100, hull.Length);
            Matrix4x4 m = Rotation(new Vector3(0.3f, 1f, 0.2f), 33f);
            AssertSame(BruteForce(grid, m), Extents(hull, m));
        }

        /// <summary>Runs <paramref name="work"/> on a worker thread and fails if it takes longer than <paramref name="ms"/>.</summary>
        private static T Within<T>(int ms, Func<T> work)
        {
            T result = default;
            Exception thrown = null;
            var t = new System.Threading.Thread(() => { try { result = work(); } catch (Exception e) { thrown = e; } }) { IsBackground = true };
            t.Start();
            Assert.True(t.Join(ms), $"did not finish within {ms} ms");
            if (thrown != null)
                throw new Exception("worker threw", thrown);
            return result;
        }

        /// <summary>A UV sphere's vertices: rings of points, so a hull with many small, thin faces.</summary>
        private static List<Vector3> Sphere(float radius, int rings, int segments)
        {
            var v = new List<Vector3>();
            for (int r = 0; r <= rings; r++)
            {
                double phi = Math.PI * r / rings;
                for (int s = 0; s <= segments; s++)
                {
                    double theta = 2 * Math.PI * s / segments;
                    v.Add(new Vector3((float)(radius * Math.Sin(phi) * Math.Cos(theta)), (float)(radius * Math.Cos(phi)), (float)(radius * Math.Sin(phi) * Math.Sin(theta))));
                }
            }
            return v;
        }

        [Fact]
        public void MeshInSmallUnitsCompletesExactly()
        {
            // Benjee10's shuttle docking system has a 5194-vertex shell 0.029 units
            // across in its own units, scaled up by its transform. A hull tolerance
            // that does not scale with the input fails on meshes this small.
            List<Vector3> sphere = Sphere(0.0145f, 40, 64);
            Vector3[] hull = Within(5000, () => MeshHull.HullPoints(sphere, out _));
            Matrix4x4 m = Rotation(new Vector3(1f, 2f, 3f), 40f, default, 60f);
            Bounds expected = BruteForce(sphere, m), actual = Extents(hull, m);
            float tol = 1e-4f * expected.size.magnitude;
            Assert.InRange((expected.min - actual.min).magnitude, 0f, tol);
            Assert.InRange((expected.max - actual.max).magnitude, 0f, tol);
        }

        [Fact]
        public void LargeCoplanarCloudIsRejectedQuickly()
        {
            // Flat meshes have no 3D hull and must be rejected quickly, however many
            // vertices they have.
            var rng = new System.Random(2);
            var flat = new List<Vector3>();
            for (int i = 0; i < 30000; i++)
                flat.Add(new Vector3((float)rng.NextDouble(), 0.5f, (float)rng.NextDouble()));
            Vector3[] hull = Within(2000, () => MeshHull.HullPoints(flat, out string error));
            Assert.Equal(30000, hull.Length);
        }

        [Fact]
        public void GridFacedBoxGivesExactExtents()
        {
            // Exactly collinear and coplanar vertices on every face, as on a
            // subdivided box, where a cone face can come out with zero area.
            var box = new List<Vector3>();
            for (int i = 0; i <= 8; i++)
                for (int j = 0; j <= 8; j++)
                    for (int k = 0; k <= 8; k++)
                        if (i == 0 || i == 8 || j == 0 || j == 8 || k == 0 || k == 8)
                            box.Add(new Vector3(i * 0.25f, j * 0.125f, k * 0.5f));
            Vector3[] hull = Within(5000, () => MeshHull.HullPoints(box, out _));
            Matrix4x4 m = Rotation(new Vector3(0.2f, 1f, 0.7f), 23f);
            AssertSame(BruteForce(box, m), Extents(hull, m));
        }

        [Fact]
        public void NonFinitePointsAreSkippedInExtents()
        {
            // Every position of the NaN, since Vector3.Min and Max keep or drop a NaN
            // depending on which argument it is.
            var pts = new List<Vector3> { Vector3.zero, Vector3.one, new Vector3(2f, -1f, 0.5f) };
            Bounds expected = Extents(pts, Matrix4x4.identity);
            for (int i = 0; i <= pts.Count; i++)
            {
                var withBad = new List<Vector3>(pts);
                withBad.Insert(i, new Vector3(float.NaN, 0f, float.PositiveInfinity));
                AssertSame(expected, Extents(withBad, Matrix4x4.identity));
                Assert.True(MeshHull.TryExtents(withBad.ToArray(), Matrix4x4.identity, out Bounds b));
                AssertSame(expected, b);
            }
        }

        [Fact]
        public void NoFinitePointsGiveNoExtents()
        {
            var pts = new List<Vector3> { new Vector3(float.NaN, 0f, 0f), new Vector3(0f, float.NegativeInfinity, 0f) };
            Assert.False(MeshHull.TryExtents(pts, Matrix4x4.identity, out _));
            Assert.False(MeshHull.TryExtents(new Vector3[0], Matrix4x4.identity, out _));
        }

        [Fact]
        public void FewerThanFourDistinctPointsAreReturnedAsIs()
        {
            var pts = new List<Vector3> { Vector3.zero, Vector3.one, Vector3.one, Vector3.right, Vector3.zero };
            Vector3[] hull = MeshHull.HullPoints(pts, out string error);
            Assert.Null(error);
            Assert.Equal(3, hull.Length);
        }
    }
}
