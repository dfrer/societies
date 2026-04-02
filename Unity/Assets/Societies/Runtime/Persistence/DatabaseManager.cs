using System;
using System.IO;
using UnityEngine;

#if UNITY_STANDALONE && !UNITY_EDITOR
using Mono.Data.Sqlite;
#else
using System.Data.SQLite;
#endif

namespace Societies.Runtime.Persistence
{
    /// <summary>
    /// Database manager for SQLite persistence
    /// </summary>
    public class DatabaseManager : MonoBehaviour
    {
        public static DatabaseManager Instance { get; private set; }

        private string _databasePath;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializeDatabase();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void InitializeDatabase()
        {
            // Create database in persistent data path
            string dataPath = Application.persistentDataPath;
            _databasePath = Path.Combine(dataPath, "societies.db");

            UnityEngine.Debug.Log($"[DatabaseManager] Database path: {_databasePath}");

            try
            {
                CreateTables();
                _isInitialized = true;
                UnityEngine.Debug.Log("[DatabaseManager] Database initialized successfully");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DatabaseManager] Failed to initialize database: {ex.Message}");
            }
        }

        private void CreateTables()
        {
            // World metadata table
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS worlds (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    seed INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    last_played TEXT NOT NULL
                )");

            // Players table
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS players (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    world_id INTEGER NOT NULL,
                    player_id TEXT NOT NULL UNIQUE,
                    position_x REAL,
                    position_y REAL,
                    position_z REAL,
                    inventory_data BLOB,
                    last_login TEXT NOT NULL,
                    FOREIGN KEY (world_id) REFERENCES worlds(id)
                )");

            // Chunks table
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS chunks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    world_id INTEGER NOT NULL,
                    chunk_x INTEGER NOT NULL,
                    chunk_z INTEGER NOT NULL,
                    data BLOB NOT NULL,
                    modified_tick INTEGER,
                    UNIQUE(world_id, chunk_x, chunk_z),
                    FOREIGN KEY (world_id) REFERENCES worlds(id)
                )");

            // Entities table
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS entities (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    world_id INTEGER NOT NULL,
                    entity_type TEXT NOT NULL,
                    position_x REAL,
                    position_y REAL,
                    position_z REAL,
                    rotation_x REAL,
                    rotation_y REAL,
                    rotation_z REAL,
                    rotation_w REAL,
                    data BLOB,
                    FOREIGN KEY (world_id) REFERENCES worlds(id)
                )");

            // Claims table
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS claims (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    world_id INTEGER NOT NULL,
                    claim_id TEXT NOT NULL UNIQUE,
                    owner_id TEXT NOT NULL,
                    min_x INTEGER NOT NULL,
                    min_y INTEGER NOT NULL,
                    min_z INTEGER NOT NULL,
                    max_x INTEGER NOT NULL,
                    max_y INTEGER NOT NULL,
                    max_z INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    FOREIGN KEY (world_id) REFERENCES worlds(id)
                )");

            // Stores table
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS stores (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    world_id INTEGER NOT NULL,
                    owner_id TEXT NOT NULL,
                    data BLOB NOT NULL,
                    FOREIGN KEY (world_id) REFERENCES worlds(id)
                )");

            // Transactions table
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS transactions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    world_id INTEGER NOT NULL,
                    buyer_id TEXT NOT NULL,
                    seller_id TEXT NOT NULL,
                    item_id INTEGER NOT NULL,
                    quantity INTEGER NOT NULL,
                    unit_price INTEGER NOT NULL,
                    transaction_time TEXT NOT NULL,
                    FOREIGN KEY (world_id) REFERENCES worlds(id)
                )");

            // Save events table
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS save_events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    world_id INTEGER NOT NULL,
                    event_type TEXT NOT NULL,
                    event_data TEXT,
                    tick INTEGER,
                    created_at TEXT NOT NULL,
                    FOREIGN KEY (world_id) REFERENCES worlds(id)
                )");

            UnityEngine.Debug.Log("[DatabaseManager] Tables created");
        }

        public int ExecuteNonQuery(string sql)
        {
            // Simplified MVP version - just log the SQL
            // In full implementation, would use SQLite connection
            UnityEngine.Debug.Log($"[DatabaseManager] SQL: {sql.Substring(0, Math.Min(100, sql.Length))}...");
            return 0;
        }

        /// <summary>
        /// Get database path
        /// </summary>
        public string GetDatabasePath() => _databasePath;

        /// <summary>
        /// Create new world
        /// </summary>
        public int CreateWorld(string name, int seed)
        {
            string now = DateTime.UtcNow.ToString("o");
            // In full implementation: INSERT INTO worlds VALUES (null, name, seed, now, now)
            UnityEngine.Debug.Log($"[DatabaseManager] Creating world: {name}, seed: {seed}");
            return 1;
        }

        /// <summary>
        /// Save chunk data
        /// </summary>
        public void SaveChunk(int worldId, ChunkCoord coord, byte[] data, ulong modifiedTick)
        {
            // In full implementation: INSERT OR REPLACE INTO chunks VALUES (...)
            UnityEngine.Debug.Log($"[DatabaseManager] Saving chunk {coord}");
        }

        /// <summary>
        /// Load chunk data
        /// </summary>
        public byte[] LoadChunk(int worldId, ChunkCoord coord)
        {
            // In full implementation: SELECT data FROM chunks WHERE ...
            return null;
        }

        /// <summary>
        /// Save player state
        /// </summary>
        public void SavePlayer(int worldId, string playerId, Vector3 position, byte[] inventoryData)
        {
            UnityEngine.Debug.Log($"[DatabaseManager] Saving player: {playerId} at {position}");
        }

        /// <summary>
        /// Load player state
        /// </summary>
        public (Vector3 position, byte[] inventory) LoadPlayer(int worldId, string playerId)
        {
            return (Vector3.zero, null);
        }
    }
}