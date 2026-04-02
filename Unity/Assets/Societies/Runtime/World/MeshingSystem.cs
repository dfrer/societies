using UnityEngine;
namespace Societies.Runtime.World
{
    /// <summary>
    /// Greedy meshing system for voxel chunks
    /// </summary>
    public class MeshingSystem
    {
        private static readonly Vector3Int[] FaceDirections = new Vector3Int[]
        {
            new(1, 0, 0),   // Right
            new(-1, 0, 0),  // Left
            new(0, 1, 0),   // Top
            new(0, -1, 0),  // Bottom
            new(0, 0, 1),   // Front
            new(0, 0, -1)   // Back
        };

        /// <summary>
        /// Generate mesh for a chunk using greedy meshing
        /// </summary>
        public void GenerateMesh(VoxelChunk chunk, out Mesh mesh)
        {
            mesh = new Mesh();
            mesh.name = $"Chunk_{chunk.Coord.X}_{chunk.Coord.Z}";

            // Simple face-culled meshing for MVP
            // (greedy optimization can be added later)
            
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var indices = new System.Collections.Generic.List<int>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var colors = new System.Collections.Generic.List<Color>();

            var blocks = chunk.GetBlocks();

            for (int y = 0; y < VoxelChunk.HEIGHT; y++)
            {
                for (int z = 0; z < VoxelChunk.WIDTH; z++)
                {
                    for (int x = 0; x < VoxelChunk.WIDTH; x++)
                    {
                        var block = blocks[VoxelChunk.GetIndex(x, y, z)];
                        if (block.IsAir) continue;

                        // Check each face
                        for (int face = 0; face < 6; face++)
                        {
                            int nx = x + FaceDirections[face].x;
                            int ny = y + FaceDirections[face].y;
                            int nz = z + FaceDirections[face].z;

                            // Get neighbor block
                            BlockData neighbor = chunk.GetBlockSafe(nx, ny, nz);

                            // Only add face if neighbor is air
                            if (neighbor.IsAir)
                            {
                                AddFace(vertices, normals, indices, uvs, colors, x, y, z, face, block.Id);
                            }
                        }
                    }
                }
            }

            if (vertices.Count > 0)
            {
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(colors);
                mesh.SetIndices(indices, MeshTopology.Triangles, 0);
                mesh.RecalculateBounds();
            }
        }

        private void AddFace(System.Collections.Generic.List<Vector3> vertices,
                            System.Collections.Generic.List<Vector3> normals,
                            System.Collections.Generic.List<int> indices,
                            System.Collections.Generic.List<Vector2> uvs,
                            System.Collections.Generic.List<Color> colors,
                            int x, int y, int z, int face, ushort blockId)
        {
            // Simple face vertices (6 faces, 4 verts each)
            Vector3[] faceVerts = GetFaceVertices(x, y, z, face);
            Vector3 faceNormal = FaceDirections[face];
            Color blockColor = GetBlockColor(blockId);

            int baseIndex = vertices.Count;

            for (int i = 0; i < 4; i++)
            {
                vertices.Add(faceVerts[i]);
                normals.Add(faceNormal);
                uvs.Add(new Vector2(i % 2, i / 2));
                colors.Add(blockColor);
            }

            // Two triangles per face
            indices.Add(baseIndex);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 3);
        }

        private Vector3[] GetFaceVertices(int x, int y, int z, int face)
        {
            // 6 faces: right, left, top, bottom, front, back
            return face switch
            {
                0 => new Vector3[] { // Right
                    new Vector3(x + 1, y, z),
                    new Vector3(x + 1, y, z + 1),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x + 1, y + 1, z + 1)
                },
                1 => new Vector3[] { // Left
                    new Vector3(x, y, z + 1),
                    new Vector3(x, y, z),
                    new Vector3(x, y + 1, z + 1),
                    new Vector3(x, y + 1, z)
                },
                2 => new Vector3[] { // Top
                    new Vector3(x, y + 1, z + 1),
                    new Vector3(x, y + 1, z),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x + 1, y + 1, z)
                },
                3 => new Vector3[] { // Bottom
                    new Vector3(x, y, z),
                    new Vector3(x, y, z + 1),
                    new Vector3(x + 1, y, z),
                    new Vector3(x + 1, y, z + 1)
                },
                4 => new Vector3[] { // Front
                    new Vector3(x, y, z + 1),
                    new Vector3(x + 1, y, z + 1),
                    new Vector3(x, y + 1, z + 1),
                    new Vector3(x + 1, y + 1, z + 1)
                },
                _ => new Vector3[] { // Back
                    new Vector3(x + 1, y, z),
                    new Vector3(x, y, z),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x, y + 1, z)
                }
            };
        }

        private Color GetBlockColor(ushort blockId)
        {
            return (BlockType)blockId switch
            {
                BlockType.Dirt => new Color(0.45f, 0.30f, 0.18f),
                BlockType.Grass => new Color(0.33f, 0.68f, 0.25f),
                BlockType.Stone => new Color(0.55f, 0.55f, 0.58f),
                BlockType.Coal => new Color(0.15f, 0.15f, 0.18f),
                BlockType.CopperOre => new Color(0.65f, 0.42f, 0.24f),
                BlockType.IronOre => new Color(0.72f, 0.62f, 0.50f),
                BlockType.Wood => new Color(0.52f, 0.35f, 0.18f),
                BlockType.Leaves => new Color(0.24f, 0.52f, 0.20f),
                BlockType.Sand => new Color(0.86f, 0.80f, 0.56f),
                BlockType.Clay => new Color(0.66f, 0.54f, 0.46f),
                BlockType.Water => new Color(0.20f, 0.45f, 0.78f),
                BlockType.Snow => new Color(0.94f, 0.96f, 1.00f),
                _ => Color.white
            };
        }
    }
}
