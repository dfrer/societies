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
        
        [Header("World Settings")]
        [SerializeField] private Material _blockMaterial;
        [SerializeField] private int _loadRadius = 4;

        private bool _isInitialized;
        private VoxelWorld _world;

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            // Update chunk loading around player
            if (_isInitialized && _world != null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _world.LoadChunksAround(player.transform.position, _loadRadius);
                }
            }
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            UnityEngine.Debug.Log("[GameBootstrap] Starting initialization...");

            // Initialize simulation clock
            var clockGO = new GameObject("SimulationClock");
            clockGO.AddComponent<SimulationClock>();
            DontDestroyOnLoad(clockGO);

            // Initialize time and lighting
            var timeGO = new GameObject("TimeSystem");
            timeGO.AddComponent<TimeSystem>();
            DontDestroyOnLoad(timeGO);

            // Initialize voxel world
            var worldGO = new GameObject("VoxelWorld");
            _world = worldGO.AddComponent<VoxelWorld>();
            DontDestroyOnLoad(worldGO);

            // Initialize database
            var dbGO = new GameObject("DatabaseManager");
            dbGO.AddComponent<Persistence.DatabaseManager>();
            DontDestroyOnLoad(dbGO);

            // Create chunk container
            var chunkContainer = new GameObject("Chunks");
            DontDestroyOnLoad(chunkContainer);

            // Subscribe to chunk loading for rendering
            _world.OnChunkLoaded += OnChunkLoaded;

            // Ensure spawn chunks exist before the player is placed.
            _world.LoadChunksAround(Vector3.zero, _loadRadius);

            // Spawn player at surface
            SpawnPlayer();

            _isInitialized = true;
            UnityEngine.Debug.Log("[GameBootstrap] Initialization complete!");
        }

        private void SpawnPlayer()
        {
            // Get spawn position on surface
            Vector3 spawnPos = _world.GetSpawnPosition(0, 0);
            
            GameObject player;
            
            if (_playerPrefab != null)
            {
                player = Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // Create default player
                player = CreateDefaultPlayer(spawnPos);
            }
            
            player.name = "Player";
            player.tag = "Player";
            DontDestroyOnLoad(player);

            // Setup camera
            SetupPlayerCamera(player);
            
            // Give starting items
            var invManager = player.GetComponent<InventoryManager>();
            if (invManager != null)
            {
                invManager.TryAddItem(7, 16); // Wood
                invManager.TryAddItem(3, 8);  // Stone
            }
        }

        private Vector3 GetSpawnPosition()
        {
            const int spawnX = 0;
            const int spawnZ = 0;

            int surfaceY = _world != null ? _world.GetSurfaceHeight(spawnX, spawnZ) : 30;
            return new Vector3(spawnX + 0.5f, surfaceY + 0.1f, spawnZ + 0.5f);
        }

        private GameObject CreateDefaultPlayer(Vector3 spawnPos)
        {
            var player = new GameObject("Player");
            player.transform.position = spawnPos;
            player.tag = "Player";
            
            // Add CharacterController
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.9f, 0);
            
            // Add player controller
            var controller = player.AddComponent<PlayerController>();
            
            // Add inventory
            player.AddComponent<InventoryManager>();
            
            // Add interaction system
            player.AddComponent<InteractionSystem>();
            
            // Add rigidbody (for physics)
            var rb = player.AddComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.useGravity = false; // CharacterController handles gravity
            
            return player;
        }

        private void SetupPlayerCamera(GameObject player)
        {
            if (_mainCamera == null)
            {
                // Create camera
                var camGO = new GameObject("PlayerCamera");
                _mainCamera = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
            }
            
            _mainCamera.transform.SetParent(player.transform);
            _mainCamera.transform.localPosition = new Vector3(0, 1.6f, 0);
            _mainCamera.transform.localRotation = Quaternion.identity;
            _mainCamera.tag = "MainCamera";
            
            // Set camera settings
            _mainCamera.clearFlags = CameraClearFlags.Skybox;
            _mainCamera.fieldOfView = 60f;
            _mainCamera.nearClipPlane = 0.1f;
            _mainCamera.farClipPlane = 500f;
        }

        private void OnChunkLoaded(ChunkCoord coord)
        {
            // Create chunk GameObject with renderer
            var chunk = _world.GetChunk(coord);
            if (chunk == null) return;

            var chunkGO = new GameObject($"Chunk_{coord.X}_{coord.Z}");
            chunkGO.transform.SetParent(GameObject.Find("Chunks").transform);
            chunkGO.transform.position = new Vector3(
                coord.X * ChunkCoord.SIZE,
                0,
                coord.Z * ChunkCoord.SIZE
            );

            var renderer = chunkGO.AddComponent<ChunkRenderer>();
            renderer.SetChunk(chunk);

            // Setup mesh renderer
            var meshRenderer = chunkGO.GetComponent<MeshRenderer>();
            if (_blockMaterial != null)
            {
                meshRenderer.material = _blockMaterial;
            }
            else
            {
                // Use a vertex-color-friendly default so block tinting is visible.
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
                var mat = new Material(shader);
                mat.color = Color.white;
                meshRenderer.material = mat;
            }
        }

        private void OnApplicationQuit()
        {
            UnityEngine.Debug.Log("[GameBootstrap] Application quitting, saving world...");
            
            if (VoxelWorld.Instance != null)
            {
                VoxelWorld.Instance.SaveAllChunks();
            }
        }

        private void OnDestroy()
        {
            if (_world != null)
            {
                _world.OnChunkLoaded -= OnChunkLoaded;
            }
        }
    }
}
