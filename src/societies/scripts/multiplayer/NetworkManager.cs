using Godot;
using System;
using System.Collections.Generic;

namespace Societies.Multiplayer
{
    /// <summary>
    /// Central manager for all multiplayer functionality.
    /// Handles server/client initialization, connection management, and RPC dispatch.
    /// </summary>
    public partial class NetworkManager : Node
    {
        public static NetworkManager Instance { get; private set; }
        
        [Export] private int _port = 7777;
        [Export] private int _maxPlayers = 100;
        [Export] private string _serverAddress = "127.0.0.1";
        
        public bool IsServer { get; private set; }
        public bool IsConnected => Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
        
        // Events
        public event Action<long>? PlayerConnected;
        public event Action<long>? PlayerDisconnected;
        public event Action? ServerStarted;
        public event Action? ConnectedToServer;
        public event Action? ConnectionFailed;
        
        public override void _Ready()
        {
            Instance = this;
            
            // Subscribe to multiplayer events
            Multiplayer.PeerConnected += OnPeerConnected;
            Multiplayer.PeerDisconnected += OnPeerDisconnected;
            Multiplayer.ConnectedToServer += OnConnectedToServer;
            Multiplayer.ConnectionFailed += OnConnectionFailed;
            Multiplayer.ServerDisconnected += OnServerDisconnected;
        }
        
        /// <summary>
        /// Start a dedicated server
        /// </summary>
        public Error StartServer(int port = -1)
        {
            int actualPort = port > 0 ? port : _port;
            
            ENetMultiplayerPeer peer = new();
            Error err = peer.CreateServer(actualPort, _maxPlayers);
            
            if (err != Error.Ok)
            {
                GD.PrintErr($"Failed to start server on port {actualPort}: {err}");
                return err;
            }
            
            Multiplayer.MultiplayerPeer = peer;
            IsServer = true;
            
            GD.Print($"Server started on port {actualPort}");
            ServerStarted?.Invoke();
            
            return Error.Ok;
        }
        
        /// <summary>
        /// Connect to a server as a client
        /// </summary>
        public Error ConnectToServer(string address = "", int port = -1)
        {
            string actualAddress = string.IsNullOrEmpty(address) ? _serverAddress : address;
            int actualPort = port > 0 ? port : _port;
            
            ENetMultiplayerPeer peer = new();
            Error err = peer.CreateClient(actualAddress, actualPort);
            
            if (err != Error.Ok)
            {
                GD.PrintErr($"Failed to connect to {actualAddress}:{actualPort}: {err}");
                return err;
            }
            
            Multiplayer.MultiplayerPeer = peer;
            IsServer = false;
            
            GD.Print($"Connecting to {actualAddress}:{actualPort}...");
            
            return Error.Ok;
        }
        
        /// <summary>
        /// Disconnect from server or stop hosting
        /// </summary>
        public void Disconnect()
        {
            if (Multiplayer.HasMultiplayerPeer())
            {
                Multiplayer.MultiplayerPeer.Close();
                Multiplayer.MultiplayerPeer = null;
            }
            
            IsServer = false;
            GD.Print("Disconnected from network");
        }
        
        private void OnPeerConnected(long id)
        {
            GD.Print($"Peer connected: {id}");
            PlayerConnected?.Invoke(id);
            
            if (IsServer)
            {
                // Server handles new player
                OnServerPlayerConnected(id);
            }
        }
        
        private void OnPeerDisconnected(long id)
        {
            GD.Print($"Peer disconnected: {id}");
            PlayerDisconnected?.Invoke(id);
            
            if (IsServer)
            {
                OnServerPlayerDisconnected(id);
            }
        }
        
        private void OnConnectedToServer()
        {
            GD.Print("Connected to server");
            ConnectedToServer?.Invoke();
        }
        
        private void OnConnectionFailed()
        {
            GD.PrintErr("Connection failed");
            ConnectionFailed?.Invoke();
        }
        
        private void OnServerDisconnected()
        {
            GD.Print("Server disconnected");
            Disconnect();
        }
        
        private void OnServerPlayerConnected(long id)
        {
            // Server-side handling of new player
            // TODO: Spawn player entity, send world state, etc.
            GD.Print($"Server: New player {id} connected");
            
            // Send initial world state to new player
            RpcId(id, MethodName.ReceiveWorldState, "initial_state_data");
        }
        
        private void OnServerPlayerDisconnected(long id)
        {
            // Server-side cleanup
            GD.Print($"Server: Player {id} disconnected");
            
            // TODO: Save player state, remove from world, etc.
        }
        
        /// <summary>
        /// RPC: Receive world state from server (client-side)
        /// </summary>
        [Rpc(MultiplayerApi.RpcMode.Authority)]
        private void ReceiveWorldState(string stateData)
        {
            GD.Print($"Received world state: {stateData.Length} bytes");
            // TODO: Deserialize and apply world state
        }
        
        /// <summary>
        /// Get unique peer ID for this client
        /// </summary>
        public long GetPeerId()
        {
            return Multiplayer.GetUniqueId();
        }
        
        /// <summary>
        /// Check if we're the multiplayer authority
        /// </summary>
        public bool IsMultiplayerAuthority()
        {
            return IsServer || !IsConnected;
        }
        
        public override void _ExitTree()
        {
            Disconnect();
            Instance = null;
        }
    }
}
