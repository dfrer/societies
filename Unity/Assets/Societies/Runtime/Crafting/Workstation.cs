using UnityEngine;

public enum WorkstationType
{
    None,
    Workbench,
    Furnace,
    Anvil,
    Campfire
}

public class Workstation : MonoBehaviour
{
    public WorkstationType Type = WorkstationType.None;
    public string StationId { get; private set; }
    public bool IsInUse { get; private set; }

    private void Awake()
    {
        StationId = System.Guid.NewGuid().ToString();
    }

    public bool CanUse(string playerId) => !IsInUse;

    public void StartUsing(string playerId)
    {
        IsInUse = true;
    }

    public void StopUsing()
    {
        IsInUse = false;
    }
}
