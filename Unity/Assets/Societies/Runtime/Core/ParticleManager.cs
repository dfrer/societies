using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnParticles(string effect, Vector3 position)
    {
        Debug.Log("Particle: " + effect + " at " + position);
    }

    public void SpawnBreakBlock() => Debug.Log("Particle: break");
    public void SpawnPlaceBlock() => Debug.Log("Particle: place");
    public void SpawnCraft() => Debug.Log("Particle: craft");
    public void SpawnFootstep() => Debug.Log("Particle: step");
}
