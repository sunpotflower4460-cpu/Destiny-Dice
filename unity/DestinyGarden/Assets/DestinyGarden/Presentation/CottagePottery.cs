using System.Collections.Generic;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    internal static class CottagePottery
    {
        internal static Mesh Pot() => Revolve("Clay pot", new[]
        {
            new Vector2(0.001f, 0f), new Vector2(0.32f, 0f), new Vector2(0.35f, 0.08f),
            new Vector2(0.38f, 0.44f), new Vector2(0.46f, 0.91f), new Vector2(0.51f, 0.91f),
            new Vector2(0.51f, 1f), new Vector2(0.42f, 1f), new Vector2(0.40f, 0.86f),
            new Vector2(0.30f, 0.25f), new Vector2(0.001f, 0.25f)
        });

        internal static Mesh Basin() => Revolve("Stone water basin", new[]
        {
            new Vector2(0.001f, 0f), new Vector2(0.29f, 0f), new Vector2(0.34f, 0.18f),
            new Vector2(0.38f, 0.42f), new Vector2(0.48f, 0.72f), new Vector2(0.50f, 0.91f),
            new Vector2(0.47f, 1f), new Vector2(0.38f, 0.96f), new Vector2(0.34f, 0.68f),
            new Vector2(0.27f, 0.53f), new Vector2(0.001f, 0.53f)
        });

        // Duplicating each ring gives the thrown lip and the foot a defined edge.
        private static Mesh Revolve(string name, Vector2[] profile)
        {
            const int sides = 24;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row + 1 < profile.Length; row++)
            {
                Vector2 start = profile[row], end = profile[row + 1];
                Vector2 tangent = (end - start).normalized;
                int first = vertices.Count;
                for (int side = 0; side <= sides; side++)
                {
                    float angle = 2f * Mathf.PI * side / sides;
                    float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                    var normal = new Vector3(x * tangent.y, -tangent.x, z * tangent.y);
                    vertices.Add(new Vector3(x * start.x, start.y, z * start.x));
                    vertices.Add(new Vector3(x * end.x, end.y, z * end.x));
                    normals.Add(normal);
                    normals.Add(normal);
                    uv.Add(new Vector2(side / (float)sides, start.y));
                    uv.Add(new Vector2(side / (float)sides, end.y));
                    if (side == sides) continue;
                    int a = first + side * 2;
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(a + 2);
                    triangles.Add(a + 2);
                    triangles.Add(a + 1);
                    triangles.Add(a + 3);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }
    }
}
