using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Core
{
    /// <summary>Godot-only projection consumer. One mesh and collision shape are created per chunk.</summary>
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
                CollisionShape3D collider = _colliders[chunk.Coord];
                collider.Shape = BuildGroundingCollision(chunk);
                collider.Position = new Vector3(
                    (chunk.Coord.X * VoxelWorldModule.ChunkWidth) + (VoxelWorldModule.ChunkWidth * 0.5f),
                    0.0f,
                    (chunk.Coord.Z * VoxelWorldModule.ChunkDepth) + (VoxelWorldModule.ChunkDepth * 0.5f));
                collider.Disabled = !_active;
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

        public HeightMapShape3D? GetGroundingCollision(VoxelChunkCoord coord) =>
            _colliders.TryGetValue(coord, out CollisionShape3D? collider)
                ? collider.Shape as HeightMapShape3D
                : null;

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

        private static HeightMapShape3D BuildGroundingCollision(VoxelChunkGeometryProjection chunk)
        {
            const int width = VoxelWorldModule.ChunkWidth;
            const int depth = VoxelWorldModule.ChunkDepth;
            float[] cellHeights = new float[width * depth];
            Array.Fill(cellHeights, VoxelWorldModule.MinY);

            for (int faceStart = 0; faceStart < chunk.Indices.Count; faceStart += 6)
            {
                Vector3 a = ToVector3(chunk.Vertices[chunk.Indices[faceStart]]);
                Vector3 b = ToVector3(chunk.Vertices[chunk.Indices[faceStart + 1]]);
                Vector3 c = ToVector3(chunk.Vertices[chunk.Indices[faceStart + 2]]);
                if ((b - a).Cross(c - a).Y >= -0.5f)
                {
                    continue;
                }

                int localX = Mathf.FloorToInt(Mathf.Min(a.X, Mathf.Min(b.X, c.X))) - (chunk.Coord.X * width);
                int localZ = Mathf.FloorToInt(Mathf.Min(a.Z, Mathf.Min(b.Z, c.Z))) - (chunk.Coord.Z * depth);
                if ((uint)localX < width && (uint)localZ < depth)
                {
                    cellHeights[(localZ * width) + localX] = Math.Max(cellHeights[(localZ * width) + localX], a.Y);
                }
            }

            HeightMapShape3D collision = new()
            {
                MapWidth = width + 1,
                MapDepth = depth + 1
            };
            float[] heights = new float[(width + 1) * (depth + 1)];
            for (int z = 0; z <= depth; z++)
            {
                for (int x = 0; x <= width; x++)
                {
                    heights[(z * (width + 1)) + x] = cellHeights[(Math.Min(z, depth - 1) * width) + Math.Min(x, width - 1)];
                }
            }
            collision.MapData = heights;
            return collision;
        }

        private static Vector3 ToVector3(VoxelVertex vertex) => new(vertex.X, vertex.Y, vertex.Z);

    }
}
