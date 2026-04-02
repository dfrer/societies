using UnityEngine;

public enum WorkstationType { None, Workbench, Furnace, Anvil, Campfire }

public class Workstation : MonoBehaviour
{
    public WorkstationType Type = WorkstationType.None;
    public string StationId { get; private set; }
    
    public bool IsInUse { get; private set; }
    public string UsingPlayerId { get; private set; }
    
    private void Awake()
    {
        StationId = System.Guid.NewGuid().ToString();
    }
    
    public bool CanUse(string playerId)
    {
        return !IsInUse || UsingPlayerId == playerId;
    }
    
    public void StartUsing(string playerId)
    {
        if (CanUse(playerId))
        {
            IsInUse = true;
            UsingPlayerId = playerId;
        }
    }
    
    public void StopUsing()
    {
        IsInUse = false;
        UsingPlayerId = null;
    }
}
