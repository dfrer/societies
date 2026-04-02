using UnityEngine;

namespace Societies.Runtime.World
{
    /// <summary>
    /// Renders a voxel chunk as a mesh
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ChunkRenderer : MonoBehaviour
    {
        private VoxelChunk _chunk;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshingSystem _meshingSystem;
        private bool _needsRebuild = true;

        public ChunkCoord Coord => _chunk?.Coord ?? new ChunkCoord(0, 0);

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshingSystem = new MeshingSystem();
        }

        public void SetChunk(VoxelChunk chunk)
        {
            _chunk = chunk;
            _needsRebuild = true;
            transform.position = new Vector3(
                chunk.Coord.X * ChunkCoord.SIZE,
                0,
                chunk.Coord.Z * ChunkCoord.SIZE
            );
            name = $"Chunk_{chunk.Coord.X}_{chunk.Coord.Z}";
        }

        private void Update()
        {
            if (_chunk != null && _chunk.NeedsMeshRebuild)
            {
                RebuildMesh();
                _chunk.NeedsMeshRebuild = false;
            }
        }

        public void RebuildMesh()
        {
            if (_chunk == null) return;

            _meshingSystem.GenerateMesh(_chunk, out Mesh mesh);
            mesh.name = $"ChunkMesh_{_chunk.Coord.X}_{_chunk.Coord.Z}";
            
            _meshFilter.sharedMesh = mesh;
            _meshFilter.mesh = mesh;
        }

        private void OnDestroy()
        {
            if (_meshFilter != null && _meshFilter.sharedMesh != null)
            {
                Destroy(_meshFilter.sharedMesh);
            }
        }
    }
}