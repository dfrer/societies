using Godot;
using System;

namespace Societies.Multiplayer
{
    /// <summary>
    /// Deferred multiplayer session placeholder. The current authoritative prototype
    /// does not use this in the active runtime path.
    /// </summary>
    public partial class PlayerSession : Node
    {
        [Export] public string PlayerId { get; set; } = "";
        [Export] public string PlayerName { get; set; } = "Unknown";
        [Export] public bool IsAuthenticated { get; private set; }
        
        // Player state
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public float Health { get; set; } = 100f;
        public float Hunger { get; set; } = 100f;
        
        // Metadata
        public DateTime ConnectedAt { get; private set; }
        public DateTime LastActivity { get; private set; }
        public int Ping { get; private set; }
        
        public override void _Ready()
        {
            ConnectedAt = DateTime.UtcNow;
            LastActivity = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Authenticate the player (server-side)
        /// </summary>
        public void Authenticate(string playerId, string playerName)
        {
            if (NetworkManager.Instance?.IsServer != true)
            {
                GD.PrintErr("Authentication can only be performed on server");
                return;
            }
            
            PlayerId = playerId;
            PlayerName = playerName;
            IsAuthenticated = true;
            
            GD.Print($"Player authenticated: {PlayerName} ({PlayerId})");
            
            // Notify client of successful auth
            RpcId(GetMultiplayerAuthority(), MethodName.OnAuthenticated, PlayerId, PlayerName);
        }
        
        [Rpc(MultiplayerApi.RpcMode.Authority)]
        private void OnAuthenticated(string id, string name)
        {
            PlayerId = id;
            PlayerName = name;
            IsAuthenticated = true;
            GD.Print($"Authenticated as: {PlayerName}");
        }
        
        /// <summary>
        /// Update activity timestamp
        /// </summary>
        public void UpdateActivity()
        {
            LastActivity = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Check if session is timed out (server-side)
        /// </summary>
        public bool IsTimedOut(double timeoutSeconds = 300)
        {
            return (DateTime.UtcNow - LastActivity).TotalSeconds > timeoutSeconds;
        }
        
        /// <summary>
        /// Deferred persistence hook retained for future non-local runtime work.
        /// </summary>
        public void SaveState()
        {
            PlayerStateData state = new()
            {
                PlayerId = PlayerId,
                Position = Position,
                Rotation = Rotation,
                Health = Health,
                Hunger = Hunger,
                LastSaved = DateTime.UtcNow
            };

            GD.Print($"Deferred PlayerSession.SaveState invoked for {state.PlayerId}");
        }
        
        /// <summary>
        /// Deferred persistence hook retained for future non-local runtime work.
        /// </summary>
        public void LoadState()
        {
            GD.Print($"Deferred PlayerSession.LoadState invoked for {PlayerName}");
        }
        
        public override void _Process(double delta)
        {
            // Update activity on input
            if (Input.IsAnythingPressed())
            {
                UpdateActivity();
            }
        }
    }
    
    /// <summary>
    /// Serializable player state data
    /// </summary>
    public struct PlayerStateData
    {
        public string PlayerId;
        public Vector3 Position;
        public Vector3 Rotation;
        public float Health;
        public float Hunger;
        public DateTime LastSaved;
    }
}
