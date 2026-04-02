using UnityEngine;
using Societies.Runtime.Simulation;
using Societies.Runtime.World;
using Societies.Runtime.Inventory;

namespace Societies.Runtime.Core
{
    /// <summary>
    /// Main game bootstrap - initializes all systems
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Initialization")]
        [SerializeField] private bool _loadExistingWorld = true;
        [SerializeField] private string _worldName = "New World";

        [Header("Systems")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private Camera _mainCamera;

        private bool _isInitialized;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            UnityEngine.Debug.Log("[GameBootstrap] Starting initialization...");

            // Initialize simulation clock
            var clockGO = new GameObject("SimulationClock");
            clockGO.AddComponent<SimulationClock>();
            DontDestroyOnLoad(clockGO);

            // Initialize voxel world
            var worldGO = new GameObject("VoxelWorld");
            var world = worldGO.AddComponent<VoxelWorld>();
            DontDestroyOnLoad(worldGO);

            // Initialize database
            var dbGO = new GameObject("DatabaseManager");
            dbGO.AddComponent<Persistence.DatabaseManager>();
            DontDestroyOnLoad(dbGO);

            // Spawn player
            if (_playerPrefab != null)
            {
                var player = Instantiate(_playerPrefab, new Vector3(0, 50, 0), Quaternion.identity);
                player.name = "Player";
                DontDestroyOnLoad(player);

                // Setup camera if not already on player
                if (_mainCamera != null && _mainCamera.transform.parent != player.transform)
                {
                    _mainCamera.transform.SetParent(player.transform);
                    _mainCamera.transform.localPosition = new Vector3(0, 1.6f, 0);
                }

                var invManager = player.GetComponent<InventoryManager>();
                if (invManager != null)
                {
                    // Give starting items
                    invManager.TryAddItem(7, 16); // Wood
                    invManager.TryAddItem(3, 8);  // Stone
                }
            }

            _isInitialized = true;
            UnityEngine.Debug.Log("[GameBootstrap] Initialization complete!");
        }

        private void OnApplicationQuit()
        {
            UnityEngine.Debug.Log("[GameBootstrap] Application quitting, saving world...");
            
            // Save world
            if (VoxelWorld.Instance != null)
            {
                VoxelWorld.Instance.SaveAllChunks();
            }
        }
    }
}