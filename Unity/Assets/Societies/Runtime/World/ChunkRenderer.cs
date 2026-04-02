using UnityEngine;

namespace Societies.Runtime.World
{
    /// <summary>
    /// Renders a voxel chunk as a mesh
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class ChunkRenderer : MonoBehaviour
    {
        private static readonly Vector3 ChunkColliderSize = new(16f, 256f, 16f);
        private static readonly Vector3 ChunkColliderCenter = new(8f, 128f, 8f);

        private VoxelChunk _chunk;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private BoxCollider _boxCollider;
        private MeshingSystem _meshingSystem;
        private bool _needsRebuild = true;

        public ChunkCoord Coord => _chunk?.Coord ?? new ChunkCoord(0, 0);

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _boxCollider = GetComponent<BoxCollider>();
            _meshingSystem = new MeshingSystem();
            ConfigureCollider();
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
            ConfigureCollider();
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

        private void ConfigureCollider()
        {
            if (_boxCollider == null)
            {
                return;
            }

            _boxCollider.isTrigger = false;
            _boxCollider.size = ChunkColliderSize;
            _boxCollider.center = ChunkColliderCenter;
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
