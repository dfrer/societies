using Godot;
using System;

namespace Societies.Multiplayer
{
    /// <summary>
    /// Deferred networking facade. The authoritative prototype path is local-session only,
    /// but the public shape remains available for later ENet/server-authoritative work.
    /// </summary>
    public partial class NetworkManager : Node
    {
        public static NetworkManager? Instance { get; private set; }

        public bool IsServer { get; private set; }
        public new bool IsConnected { get; private set; }
        public bool IsLocalSession { get; private set; }

        public event Action<long>? PlayerConnected;
        public event Action<long>? PlayerDisconnected;
        public event Action? ServerStarted;
        public event Action? ConnectedToServer;

        public override void _Ready()
        {
            Instance = this;
        }

        public Error StartServer(int port = 7777)
        {
            // Deferred path: keep the interface stable without advertising real networking.
            IsServer = true;
            IsConnected = true;
            IsLocalSession = false;
            ServerStarted?.Invoke();
            return Error.Ok;
        }

        public Error ConnectToServer(string address = "127.0.0.1", int port = 7777)
        {
            // Deferred path: keep the interface stable without advertising real networking.
            IsServer = false;
            IsConnected = true;
            IsLocalSession = false;
            ConnectedToServer?.Invoke();
            return Error.Ok;
        }

        public void StartLocalSession()
        {
            IsServer = true;
            IsConnected = true;
            IsLocalSession = true;

            ServerStarted?.Invoke();
            ConnectedToServer?.Invoke();
            PlayerConnected?.Invoke(1);
        }

        public long GetPeerId()
        {
            return 1;
        }

        public new bool IsMultiplayerAuthority()
        {
            return true;
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                PlayerDisconnected?.Invoke(1);
            }

            IsServer = false;
            IsConnected = false;
            IsLocalSession = false;
        }

        public override void _ExitTree()
        {
            Disconnect();
            Instance = null;
        }
    }
}
