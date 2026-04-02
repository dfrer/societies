using UnityEngine;
using Societies.Runtime.Inventory;

namespace Societies.Runtime.World
{
    /// <summary>
    /// Handles voxel interaction (mining, placement, targeting)
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _maxInteractionDistance = 5f;
        [SerializeField] private LayerMask _blockLayers;
        [SerializeField] private LayerMask _interactableLayers;

        [Header("Mining")]
        [SerializeField] private float _baseMiningTime = 1f;

        private Camera _playerCamera;
        private InventoryManager _inventory;
        private VoxelWorld _world;

        // Current target
        public BlockCoord TargetBlock { get; private set; }
        public Vector3 TargetPosition { get; private set; }
        public bool HasTarget { get; private set; }

        // Mining state
        public bool IsMining { get; private set; }
        public float MiningProgress { get; private set; }
        public BlockCoord MiningTarget { get; private set; }

        private void Awake()
        {
            _playerCamera = GetComponentInChildren<Camera>();
            _inventory = GetComponent<InventoryManager>();
            _world = VoxelWorld.Instance;
        }

        private void Update()
        {
            UpdateTarget();
        }

        private void UpdateTarget()
        {
            Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, _maxInteractionDistance, _blockLayers))
            {
                HasTarget = true;
                TargetPosition = hit.point;
                
                // Calculate block position
                Vector3Int hitPos = Vector3Int.FloorToInt(hit.point);
                Vector3 normal = hit.normal;

                // If hitting from below, target the block above
                if (normal.y > 0.5f)
                    TargetBlock = new BlockCoord(hitPos.x, hitPos.y + 1, hitPos.z);
                else if (normal.y < -0.5f)
                    TargetBlock = new BlockCoord(hitPos.x, hitPos.y - 1, hitPos.z);
                else
                    TargetBlock = new BlockCoord(hitPos.x, hitPos.y, hitPos.z);
            }
            else
            {
                HasTarget = false;
                TargetBlock = new BlockCoord(0, -1000, 0);
                TargetPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// Start mining current target
        /// </summary>
        public bool StartMining()
        {
            if (!HasTarget || !_world.IsInBounds(TargetBlock))
                return false;

            var block = _world.GetBlock(TargetBlock);
            if (block.IsAir) return false;

            IsMining = true;
            MiningTarget = TargetBlock;
            MiningProgress = 0f;
            return true;
        }

        /// <summary>
        /// Continue mining
        /// </summary>
        public void ContinueMining()
        {
            if (!IsMining) return;

            float miningSpeed = GetMiningSpeed();
            MiningProgress += Time.deltaTime * miningSpeed;

            float requiredTime = GetMiningTime(MiningTarget);
            if (MiningProgress >= requiredTime)
            {
                CompleteMining();
            }
        }

        /// <summary>
        /// Cancel mining
        /// </summary>
        public void CancelMining()
        {
            IsMining = false;
            MiningProgress = 0f;
            MiningTarget = new BlockCoord(0, -1000, 0);
        }

        private void CompleteMining()
        {
            if (!_world.IsInBounds(MiningTarget))
            {
                CancelMining();
                return;
            }

            var block = _world.GetBlock(MiningTarget);
            if (block.IsAir)
            {
                CancelMining();
                return;
            }

            // Get harvest yield
            int itemId = GetHarvestItemId(block.Id);
            int quantity = 1;

            // Try add to inventory
            if (_inventory != null && _inventory.TryAddItem(itemId, quantity))
            {
                // Remove block
                _world.SetBlock(MiningTarget, BlockData.Air);
                UnityEngine.Debug.Log($"[Interaction] Mined {(BlockType)block.Id}");
            }

            CancelMining();
        }

        private float GetMiningTime(BlockCoord coord)
        {
            var block = _world.GetBlock(coord);
            if (block.IsAir) return float.MaxValue;

            // Base time by block type
            float baseTime = (BlockType)block.Id switch
            {
                BlockType.Dirt => 0.5f,
                BlockType.Grass => 0.5f,
                BlockType.Sand => 0.4f,
                BlockType.Leaves => 0.3f,
                BlockType.Wood => 0.8f,
                BlockType.Stone => 1.5f,
                BlockType.Coal => 2.0f,
                BlockType.CopperOre => 2.5f,
                BlockType.IronOre => 3.0f,
                _ => 1.0f
            };

            return baseTime;
        }

        private float GetMiningSpeed()
        {
            // TODO: Check tool, apply modifiers
            return 1f / _baseMiningTime;
        }

        private int GetHarvestItemId(ushort blockId)
        {
            // Convert block to harvest item
            return blockId switch
            {
                (ushort)BlockType.Dirt => 1,
                (ushort)BlockType.Grass => 1,
                (ushort)BlockType.Sand => 9,
                (ushort)BlockType.Stone => 3,
                (ushort)BlockType.Coal => 4,
                (ushort)BlockType.CopperOre => 5,
                (ushort)BlockType.IronOre => 6,
                (ushort)BlockType.Wood => 7,
                (ushort)BlockType.Leaves => 7,
                _ => (int)blockId
            };
        }

        /// <summary>
        /// Place a block at position
        /// </summary>
        public bool PlaceBlock(int blockId)
        {
            if (!HasTarget || !_world.IsInBounds(TargetBlock))
                return false;

            // Check if target space is empty
            var existing = _world.GetBlock(TargetBlock);
            if (!existing.IsAir) return false;

            // Check inventory has block
            if (_inventory != null && _inventory.HasItem(blockId, 1))
            {
                _inventory.RemoveItem(blockId, 1);
                _world.SetBlock(TargetBlock, BlockData.FromType((BlockType)blockId));
                return true;
            }

            return false;
        }
    }
}
