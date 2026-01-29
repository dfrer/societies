using Godot;
using System;
using Societies.Core;
using Societies.Multiplayer;

namespace Societies.Core
{
    /// <summary>
    /// Main game manager and entry point.
    /// Coordinates all major systems and handles game lifecycle.
    /// </summary>
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }
        
        // Configuration
        [Export] private bool _startAsServer = false;
        [Export] private bool _autoConnect = false;
        
        // Systems
        private NetworkManager? _networkManager;
        private EntityManager? _entityManager;
        private Label? _debugInfo;
        
        // Game state
        public bool IsGameRunning { get; private set; }
        public DateTime GameStartTime { get; private set; }
        
        public override void _Ready()
        {
            Instance = this;
            
            // Get references
            _networkManager = GetNode<NetworkManager>("NetworkManager");
            _entityManager = GetNode<EntityManager>("EntityManager");
            _debugInfo = GetNode<Label>("UI/DebugInfo");
            
            // Setup network callbacks
            if (_networkManager != null)
            {
                _networkManager.PlayerConnected += OnPlayerConnected;
                _networkManager.PlayerDisconnected += OnPlayerDisconnected;
                _networkManager.ServerStarted += OnServerStarted;
                _networkManager.ConnectedToServer += OnConnectedToServer;
            }
            
            // Auto-start if configured
            if (_startAsServer)
            {
                StartServer();
            }
            else if (_autoConnect)
            {
                ConnectToServer();
            }
            
            GD.Print("Societies GameManager initialized");
            GD.Print($"Version: 0.1.0-alpha");
            GD.Print($"Engine: Godot {Engine.GetVersionInfo()["string"]}");
        }
        
        /// <summary>
        /// Start as dedicated server
        /// </summary>
        public void StartServer(int port = 7777)
        {
            GD.Print("Starting server...");
            
            if (_networkManager == null)
            {
                GD.PrintErr("NetworkManager not found");
                return;
            }
            
            var result = _networkManager.StartServer(port);
            if (result == Error.Ok)
            {
                GameStartTime = DateTime.UtcNow;
                IsGameRunning = true;
                GD.Print($"Server started on port {port}");
            }
            else
            {
                GD.PrintErr($"Failed to start server: {result}");
            }
        }
        
        /// <summary>
        /// Connect to server as client
        /// </summary>
        public void ConnectToServer(string address = "127.0.0.1", int port = 7777)
        {
            GD.Print($"Connecting to {address}:{port}...");
            
            if (_networkManager == null)
            {
                GD.PrintErr("NetworkManager not found");
                return;
            }
            
            var result = _networkManager.ConnectToServer(address, port);
            if (result != Error.Ok)
            {
                GD.PrintErr($"Failed to connect: {result}");
            }
        }
        
        /// <summary>
        /// Start local single-player mode (offline)
        /// </summary>
        public void StartSinglePlayer()
        {
            GD.Print("Starting single-player mode...");
            
            // In single-player, we run a local server
            StartServer(0); // Port 0 = any available port
            
            // Then connect to it
            ConnectToServer("127.0.0.1", 0);
        }
        
        private void OnServerStarted()
        {
            GD.Print("Server is running");
            InitializeWorld();
        }
        
        private void OnConnectedToServer()
        {
            GD.Print("Connected to server");
            IsGameRunning = true;
        }
        
        private void OnPlayerConnected(long id)
        {
            GD.Print($"Player {id} connected");
            
            // TODO: Spawn player character, send world state, etc.
        }
        
        private void OnPlayerDisconnected(long id)
        {
            GD.Print($"Player {id} disconnected");
            
            // TODO: Clean up player entity, save state, etc.
        }
        
        /// <summary>
        /// Initialize the game world
        /// </summary>
        private void InitializeWorld()
        {
            GD.Print("Initializing world...");
            
            // TODO: Generate terrain
            // TODO: Spawn initial resources
            // TODO: Spawn AI agents
            // TODO: Set up ecosystem
            
            GD.Print("World initialized");
        }
        
        public override void _Process(double delta)
        {
            // Update debug info
            UpdateDebugInfo();
            
            // Game tick processing
            if (IsGameRunning)
            {
                ProcessGameTick(delta);
            }
        }
        
        private void UpdateDebugInfo()
        {
            if (_debugInfo == null) return;
            
            var fps = Engine.GetFramesPerSecond();
            var entityCount = _entityManager?.EntityCount ?? 0;
            
            _debugInfo.Text = $"Societies v0.1.0-alpha\n" +
                $"FPS: {fps:F0}\n" +
                $"Entities: {entityCount}\n" +
                $"Mode: {(_networkManager?.IsServer == true ? "Server" : "Client")}\n" +
                $"Connected: {_networkManager?.IsConnected == true}";
        }
        
        private void ProcessGameTick(double delta)
        {
            // TODO: Game simulation logic
            // - Update ecosystem
            // - Process AI agents
            // - Update economy
            // - Enforce laws
        }
        
        public override void _ExitTree()
        {
            Instance = null;
            
            // Cleanup
            if (_networkManager != null)
            {
                _networkManager.Disconnect();
            }
        }
    }
}
