using UnityEngine;

public static class GameRules
{
    // Movement
    public static float WalkSpeed = 3f;
    public static float SprintSpeed = 6f;
    public static float JumpForce = 5f;
    
    // Inventory
    public static int InventorySlots = 64;
    public static float InventoryCapacity = 100f;
    
    // World
    public static int ChunkSize = 16;
    public static int ChunkHeight = 256;
    public static float InteractionDistance = 5f;
    
    // Time
    public static float DayLengthSeconds = 600f;
    
    // Save
    public static float AutoSaveInterval = 30f;
    
    // AI
    public static int MaxAgents = 25;
    
    // Encumbrance
    public static float EncumbranceLight = 25f;
    public static float EncumbranceMedium = 50f;
    public static float EncumbranceHeavy = 75f;
}