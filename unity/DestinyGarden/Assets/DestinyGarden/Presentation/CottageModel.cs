using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>Assembles the house from shared meshes, with one opaque draw per material per addition.</summary>
    internal sealed class CottageModel
    {
        private readonly Mesh _box;
        private readonly Mesh _stone;
        private readonly Mesh _gable;
        private readonly Dictionary<Material, List<CombineInstance>> _parts = new Dictionary<Material, List<CombineInstance>>();

        internal CottageModel(Mesh box, Mesh stone, Mesh gable)
        {
            _box = box;
            _stone = stone;
            _gable = gable;
        }

        internal void Box(Material material, Vector3 center, Vector3 size, float pitch = 0f, float yaw = 0f) =>
            Add(_box, material, center, size, Quaternion.Euler(pitch, yaw, 0f));

        internal void Board(Material material, Vector3 center, Vector3 size, Quaternion rotation) =>
            Add(_box, material, center, size, rotation);

        internal void Stone(Material material, Vector3 center, Vector3 size, float yaw) =>
            Add(_stone, material, center, size, Quaternion.Euler(0f, yaw, 0f));

        internal void Gable(Material material, Vector3 center, Vector3 size) =>
            Add(_gable, material, center, size, Quaternion.identity);

        internal void Plants(Material material, Mesh mesh, Vector3 at, float size) =>
            Add(mesh, material, at, Vector3.one * size, Quaternion.identity);

        internal void Post(Material material, Vector3 start, Vector3 end, float width)
        {
            Vector3 span = end - start;
            Add(_box, material, (start + end) * 0.5f, new Vector3(width, span.magnitude, width),
                Quaternion.FromToRotation(Vector3.up, span));
        }

        private void Add(Mesh mesh, Material material, Vector3 center, Vector3 size, Quaternion rotation)
        {
            if (!_parts.TryGetValue(material, out var batch))
            {
                batch = new List<CombineInstance>();
                _parts.Add(material, batch);
            }
            batch.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(center, rotation, size) });
        }

        internal void Build(Transform parent, List<Mesh> meshes)
        {
            foreach (var pair in _parts)
            {
                var obj = new GameObject(pair.Key.name);
                obj.transform.SetParent(parent, false);
                var mesh = new Mesh { name = parent.name + " " + pair.Key.name };
                mesh.CombineMeshes(pair.Value.ToArray());
                meshes.Add(mesh);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key;
                // Panes and window light should not darken the furniture behind them.
                if (pair.Key.renderQueue >= (int)RenderQueue.Transparent || pair.Key.IsKeywordEnabled("_EMISSION"))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        internal static Mesh CreateGable()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var a = new Vector3(-0.5f, 0f, -0.5f);
            var b = new Vector3(0.5f, 0f, -0.5f);
            var c = new Vector3(0f, 1f, -0.5f);
            var d = new Vector3(-0.5f, 0f, 0.5f);
            var e = new Vector3(0.5f, 0f, 0.5f);
            var f = new Vector3(0f, 1f, 0.5f);
            Face(a, c, b);
            Face(d, e, f);
            Face(a, d, f, c);
            Face(b, c, f, e);
            Face(a, b, e, d);
            var mesh = new Mesh { name = "Cottage gable" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            return mesh;

            void Face(params Vector3[] points)
            {
                int start = vertices.Count;
                Vector3 normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized;
                foreach (Vector3 point in points)
                {
                    vertices.Add(point);
                    normals.Add(normal);
                    uv.Add(new Vector2(Mathf.Abs(normal.z) > 0.5f ? point.x + 0.5f : point.z + 0.5f, point.y));
                }
                for (int i = 1; i + 1 < points.Length; i++)
                {
                    triangles.Add(start);
                    triangles.Add(start + i);
                    triangles.Add(start + i + 1);
                }
            }
        }
    }
}
