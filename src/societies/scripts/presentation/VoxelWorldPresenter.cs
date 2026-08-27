using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Core
{
    /// <summary>Godot-only projection consumer. One mesh/collision node is created per chunk, never per voxel.</summary>
    public partial class VoxelWorldPresenter : Node3D
    {
        private readonly Dictionary<VoxelChunkCoord, MeshInstance3D> _meshes = new();
        private readonly Dictionary<VoxelChunkCoord, CollisionShape3D> _colliders = new();
        private bool _active = true;

        public void Apply(VoxelWorldProjection projection)
        {
            foreach (VoxelChunkGeometryProjection chunk in projection.Chunks)
            {
                ArrayMesh mesh = BuildMesh(chunk);
                if (!_meshes.TryGetValue(chunk.Coord, out MeshInstance3D? instance))
                {
                    instance = new MeshInstance3D { Name = $"VoxelChunk_{chunk.Coord}" };
                    AddChild(instance); _meshes.Add(chunk.Coord, instance);
                    StaticBody3D body = new() { Name = $"VoxelCollision_{chunk.Coord}" }; AddChild(body);
                    CollisionShape3D shape = new(); body.AddChild(shape); _colliders.Add(chunk.Coord, shape);
                }
                instance.Mesh = mesh;
                _colliders[chunk.Coord].Shape = mesh.CreateTrimeshShape();
                _colliders[chunk.Coord].Disabled = !_active;
            }
        }

        public void SetActive(bool active)
        {
            _active = active;
            Visible = active;
            foreach (CollisionShape3D collider in _colliders.Values)
            {
                collider.Disabled = !active;
            }
        }

        public bool HasChunkGeometryAndCollision(VoxelChunkCoord coord) =>
            _meshes.TryGetValue(coord, out MeshInstance3D? mesh) && mesh.Mesh != null &&
            _colliders.TryGetValue(coord, out CollisionShape3D? collider) && collider.Shape != null && !collider.Disabled;

        public bool HasActiveCollisions => _colliders.Values.Any(collider => collider.Shape != null && !collider.Disabled);

        public bool HasLitVertexColorMaterial()
        {
            return _meshes.Values.All(instance =>
            {
                if (instance.Mesh is not ArrayMesh mesh || mesh.GetSurfaceCount() == 0)
                {
                    return false;
                }

                Mesh.ArrayFormat format = mesh.SurfaceGetFormat(0);
                return (format & Mesh.ArrayFormat.FormatNormal) != 0 &&
                    (format & Mesh.ArrayFormat.FormatColor) != 0 &&
                    mesh.SurfaceGetMaterial(0) is StandardMaterial3D material &&
                    material.VertexColorUseAsAlbedo;
            });
        }

        private static ArrayMesh BuildMesh(VoxelChunkGeometryProjection chunk)
        {
            Godot.Collections.Array arrays = new(); arrays.Resize((int)ArrayMesh.ArrayType.Max);
            Vector3[] vertices = new Vector3[chunk.Vertices.Count]; Color[] colors = new Color[chunk.Vertices.Count]; Vector3[] normals = new Vector3[chunk.Vertices.Count];
            for (int index = 0; index < chunk.Vertices.Count; index++)
            {
                VoxelVertex vertex = chunk.Vertices[index]; vertices[index] = new Vector3(vertex.X, vertex.Y, vertex.Z);
                colors[index] = vertex.Material switch { VoxelMaterialId.Soil => new Color("76523a"), VoxelMaterialId.Stone => new Color("777777"), VoxelMaterialId.Wood => new Color("704421"), _ => new Color("444444") };
            }
            for (int faceStart = 0; faceStart < vertices.Length; faceStart += 4)
            {
                Vector3 inward = (vertices[faceStart + 2] - vertices[faceStart]).Cross(vertices[faceStart + 1] - vertices[faceStart]).Normalized();
                Vector3 outward = -inward;
                for (int corner = 0; corner < 4; corner++) normals[faceStart + corner] = outward;
            }
            arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices; arrays[(int)ArrayMesh.ArrayType.Normal] = normals; arrays[(int)ArrayMesh.ArrayType.Color] = colors; arrays[(int)ArrayMesh.ArrayType.Index] = chunk.Indices.ToArray();
            ArrayMesh mesh = new(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1.0f });
            return mesh;
        }
    }
}
