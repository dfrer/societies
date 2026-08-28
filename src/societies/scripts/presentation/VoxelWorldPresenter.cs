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
        private readonly Dictionary<VoxelChunkCoord, StaticBody3D> _collisionBodies = new();
        private bool _active = true;

        public void Apply(VoxelWorldProjection projection)
        {
            foreach (VoxelChunkGeometryProjection chunk in projection.Chunks)
            {
                ArrayMesh mesh = BuildMesh(chunk);
                if (!_meshes.TryGetValue(chunk.Coord, out MeshInstance3D? instance))
                {
                    instance = new MeshInstance3D { Name = $"VoxelChunk_{chunk.Coord}" };
                    AddChild(instance);
                    _meshes.Add(chunk.Coord, instance);
                    StaticBody3D body = new() { Name = $"VoxelCollision_{chunk.Coord}" };
                    AddChild(body);
                    _collisionBodies.Add(chunk.Coord, body);
                }
                instance.Mesh = mesh;
                ReplaceGroundingCollisions(_collisionBodies[chunk.Coord], chunk);
            }
        }

        public void SetActive(bool active)
        {
            _active = active;
            Visible = active;
            foreach (CollisionShape3D collider in _collisionBodies.Values.SelectMany(body => body.GetChildren().OfType<CollisionShape3D>()))
            {
                collider.Disabled = !active;
            }
        }

        public bool HasChunkGeometryAndCollision(VoxelChunkCoord coord) =>
            _meshes.TryGetValue(coord, out MeshInstance3D? mesh) && mesh.Mesh != null &&
            _collisionBodies.TryGetValue(coord, out StaticBody3D? body) &&
            body.GetChildren().OfType<CollisionShape3D>().Any(collider => collider.Shape != null && !collider.Disabled);

        public bool HasActiveCollisions => _collisionBodies.Values
            .SelectMany(body => body.GetChildren().OfType<CollisionShape3D>())
            .Any(collider => collider.Shape != null && !collider.Disabled);

        public int GetGroundingCollisionCount(VoxelChunkCoord coord) =>
            _collisionBodies.TryGetValue(coord, out StaticBody3D? body)
                ? body.GetChildren().OfType<CollisionShape3D>().Count()
                : 0;

        public float? GetGroundingCollisionSurface(VoxelCoord coord)
        {
            VoxelChunkCoord chunk = new(
                ToChunkCoordinate(coord.X, VoxelWorldModule.ChunkWidth),
                0,
                ToChunkCoordinate(coord.Z, VoxelWorldModule.ChunkDepth));
            if (!_collisionBodies.TryGetValue(chunk, out StaticBody3D? body))
            {
                return null;
            }

            float? highest = null;
            foreach (CollisionShape3D candidate in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (candidate.Shape is not BoxShape3D box)
                {
                    continue;
                }

                Vector3 minimum = candidate.Position - (box.Size * 0.5f);
                Vector3 maximum = candidate.Position + (box.Size * 0.5f);
                if (coord.X + 0.5f >= minimum.X && coord.X + 0.5f <= maximum.X &&
                    coord.Z + 0.5f >= minimum.Z && coord.Z + 0.5f <= maximum.Z)
                {
                    highest = highest.HasValue ? Math.Max(highest.Value, maximum.Y) : maximum.Y;
                }
            }

            return highest;
        }

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
            }
            for (int faceStart = 0; faceStart < vertices.Length; faceStart += 4)
            {
                Vector3 inward = (vertices[faceStart + 2] - vertices[faceStart]).Cross(vertices[faceStart + 1] - vertices[faceStart]).Normalized();
                Vector3 outward = -inward;
                for (int corner = 0; corner < 4; corner++)
                {
                    normals[faceStart + corner] = outward;
                    VoxelMaterialId material = chunk.Vertices[faceStart + corner].Material;
                    colors[faceStart + corner] = material switch
                    {
                        VoxelMaterialId.Soil when outward.Y > 0.5f => new Color("4f7a3d"),
                        VoxelMaterialId.Soil => new Color("755039"),
                        VoxelMaterialId.Stone when Math.Abs(outward.Y) < 0.5f => new Color("697078"),
                        VoxelMaterialId.Stone => new Color("8b9093"),
                        VoxelMaterialId.Wood when Math.Abs(outward.X) > 0.5f => new Color("80552f"),
                        VoxelMaterialId.Wood => new Color("9b6a3c"),
                        _ => new Color("444444")
                    };
                }
            }
            arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices; arrays[(int)ArrayMesh.ArrayType.Normal] = normals; arrays[(int)ArrayMesh.ArrayType.Color] = colors; arrays[(int)ArrayMesh.ArrayType.Index] = chunk.Indices.ToArray();
            ArrayMesh mesh = new(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
                Roughness = 0.92f,
                Metallic = 0.0f
            });
            return mesh;
        }

        private void ReplaceGroundingCollisions(StaticBody3D body, VoxelChunkGeometryProjection chunk)
        {
            foreach (Node child in body.GetChildren())
            {
                child.Free();
            }

            foreach (IGrouping<(int MinY, int MaxY), VoxelVerticalRunProjection> interval in chunk.OccupiedRuns
                .GroupBy(run => (MinY: run.MinYInclusive, MaxY: run.MaxYExclusive))
                .OrderBy(group => group.Key.MinY)
                .ThenBy(group => group.Key.MaxY))
            {
                HashSet<(int X, int Z)> occupied = interval.Select(run => (run.X, run.Z)).ToHashSet();
                HashSet<(int X, int Z)> consumed = new();
                for (int localZ = 0; localZ < VoxelWorldModule.ChunkDepth; localZ++)
                {
                    for (int localX = 0; localX < VoxelWorldModule.ChunkWidth; localX++)
                    {
                        int worldX = (chunk.Coord.X * VoxelWorldModule.ChunkWidth) + localX;
                        int worldZ = (chunk.Coord.Z * VoxelWorldModule.ChunkDepth) + localZ;
                        if (!occupied.Contains((worldX, worldZ)) || consumed.Contains((worldX, worldZ)))
                        {
                            continue;
                        }

                        int spanWidth = 1;
                        while (localX + spanWidth < VoxelWorldModule.ChunkWidth &&
                            occupied.Contains((worldX + spanWidth, worldZ)) &&
                            !consumed.Contains((worldX + spanWidth, worldZ)))
                        {
                            spanWidth++;
                        }

                        int spanDepth = 1;
                        while (localZ + spanDepth < VoxelWorldModule.ChunkDepth &&
                            Enumerable.Range(0, spanWidth).All(offset =>
                                occupied.Contains((worldX + offset, worldZ + spanDepth)) &&
                                !consumed.Contains((worldX + offset, worldZ + spanDepth))))
                        {
                            spanDepth++;
                        }

                        for (int consumedZ = 0; consumedZ < spanDepth; consumedZ++)
                            for (int consumedX = 0; consumedX < spanWidth; consumedX++)
                                consumed.Add((worldX + consumedX, worldZ + consumedZ));

                        float height = interval.Key.MaxY - interval.Key.MinY;
                        CollisionShape3D collider = new()
                        {
                            Name = $"VoxelRun_{worldX}_{interval.Key.MinY}_{worldZ}",
                            Shape = new BoxShape3D { Size = new Vector3(spanWidth, height, spanDepth) },
                            Position = new Vector3(worldX + (spanWidth * 0.5f), interval.Key.MinY + (height * 0.5f), worldZ + (spanDepth * 0.5f)),
                            Disabled = !_active
                        };
                        body.AddChild(collider);
                    }
                }
            }
        }

        private static int ToChunkCoordinate(int value, int size) => value >= 0 ? value / size : ((value + 1) / size) - 1;

    }
}
