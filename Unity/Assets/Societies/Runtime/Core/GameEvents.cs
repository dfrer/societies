using UnityEngine;
using System;

public static class GameEvents
{
    public static Action<string> OnMessage;
    public static Action OnPlayerSpawn;
    public static Action OnBlockPlaced;
    public static Action OnBlockBroken;
    public static Action OnItemCrafted;
    
    public static void SendMessage(string msg)
    {
        Debug.Log("[EVENT] " + msg);
        OnMessage?.Invoke(msg);
    }
    
    public static void PlayerSpawned() => OnPlayerSpawn?.Invoke();
    public static void BlockPlaced() => OnBlockPlaced?.Invoke();
    public static void BlockBroken() => OnBlockBroken?.Invoke();
    public static void ItemCrafted() => OnItemCrafted?.Invoke();
}