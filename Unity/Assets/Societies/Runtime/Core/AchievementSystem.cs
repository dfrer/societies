using UnityEngine;
using System.Collections.Generic;

public class AchievementSystem : MonoBehaviour
{
    public static AchievementSystem Instance { get; private set; }
    private HashSet<string> _unlocked = new();
    
    private void Awake() { Instance = this; }
    
    public void Unlock(string achievementId)
    {
        if (!_unlocked.Contains(achievementId))
        {
            _unlocked.Add(achievementId);
            Debug.Log("Achievement: " + achievementId);
        }
    }
    
    public bool HasAchievement(string id) => _unlocked.Contains(id);
    
    public const string FIRST_LOGIN = "first_login";
    public const string FIRST_BLOCK = "first_block";
    public const string BUILD_SHELTER = "build_shelter";
    public const string MEET_AGENT = "meet_agent";
    public const string CRAFT_TOOL = "craft_tool";
}