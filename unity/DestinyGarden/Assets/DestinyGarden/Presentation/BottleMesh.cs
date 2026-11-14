using System.Collections.Generic;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    internal static class BottleMesh
    {
        internal const float WaterlineRadius = 14.28f;
        internal const float MouthHeight = 17.13f;
        internal const float NeckInnerRadius = 2.98f;

        // The profile climbs from below the waterline, rolls over the mouth and finishes inside the neck.
        // Cubic segments share tangents so the outline and reflected light flow around the bends.
        internal static Mesh Create()
        {
            var profile = new List<(Vector2 point, Vector2 tangent)>();
            Curve(13.82f, -0.52f, 13.86f, -0.22f, 14.12f, -0.18f, WaterlineRadius, 0f, 6);
            Curve(WaterlineRadius, 0f, 14.52f, 0.27f, 14.56f, 0.70f, 14.61f, 1.70f, 9);
            Curve(14.61f, 1.70f, 14.66f, 2.70f, 14.66f, 3.54f, 14.48f, 4.78f, 9);
            Curve(14.48f, 4.78f, 14.26f, 6.30f, 13.79f, 7.97f, 11.98f, 9.53f, 12);
            Curve(11.98f, 9.53f, 10.17f, 11.09f, 7.86f, 12.18f, 5.76f, 13.28f, 12);
            Curve(5.76f, 13.28f, 4.38f, 14.00f, 3.30f, 14.52f, 3.28f, 15.68f, 10);
            Curve(3.28f, 15.68f, 3.27f, 16.26f, 3.29f, 16.49f, 3.34f, 16.60f, 6);
            int lipStart = profile.Count - 1;
            Curve(3.34f, 16.60f, 3.40f, 16.73f, 3.63f, 16.62f, 3.66f, 16.93f, 8);
            Curve(3.66f, 16.93f, 3.69f, 17.24f, 3.30f, 17.32f, 3.08f, MouthHeight, 8);
            Curve(3.08f, MouthHeight, 2.94f, 17.01f, NeckInnerRadius, 16.80f, NeckInnerRadius, 16.57f, 10);

            const int sides = 120;
            var vertices = new Vector3[profile.Count * sides];
            var normals = new Vector3[vertices.Length];
            var wall = new List<int>(lipStart * sides * 6);
            var lip = new List<int>((profile.Count - 1 - lipStart) * sides * 6);
            for (int side = 0; side < sides; side++)
            {
                float angle = side * (2f * Mathf.PI / sides);
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                int nextSide = (side + 1) % sides;
                for (int row = 0; row < profile.Count; row++)
                {
                    var (point, tangent) = profile[row];
                    int a = row * sides + side;
                    vertices[a] = new Vector3(x * point.x, point.y, z * point.x);
                    normals[a] = new Vector3(x * tangent.y, -tangent.x, z * tangent.y);
                    if (row == profile.Count - 1) continue;

                    var triangles = row < lipStart ? wall : lip;
                    int b = row * sides + nextSide;
                    int c = a + sides;
                    int d = b + sides;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            var mesh = new Mesh
            {
                name = "Glass bottle",
                vertices = vertices,
                normals = normals,
                subMeshCount = 3
            };
            mesh.SetTriangles(wall, 0, false);
            mesh.SetTriangles(wall, 1, false);
            mesh.SetTriangles(lip, 2, false);
            mesh.RecalculateBounds();
            return mesh;

            void Curve(float r0, float y0, float r1, float y1, float r2, float y2, float r3, float y3, int segments)
            {
                var start = new Vector2(r0, y0);
                var controlA = new Vector2(r1, y1);
                var controlB = new Vector2(r2, y2);
                var end = new Vector2(r3, y3);
                for (int i = profile.Count == 0 ? 0 : 1; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    var a = Vector2.Lerp(start, controlA, t);
                    var b = Vector2.Lerp(controlA, controlB, t);
                    var c = Vector2.Lerp(controlB, end, t);
                    var left = Vector2.Lerp(a, b, t);
                    var right = Vector2.Lerp(b, c, t);
                    profile.Add((Vector2.Lerp(left, right, t), (right - left).normalized));
                }
            }
        }
    }
}
