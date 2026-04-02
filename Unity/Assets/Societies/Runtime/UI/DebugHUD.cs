using UnityEngine;
using Societies.Runtime.Simulation;
using Societies.Runtime.World;

namespace Societies.Runtime.UI
{
    public class DebugHUD : MonoBehaviour
    {
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label("=== SOCIETIES DEBUG ===");

            GUILayout.Label("FPS: " + (1f / Time.deltaTime).ToString("F0"));

            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                GUILayout.Label("Pos: " + player.transform.position);
            }

            var world = VoxelWorld.Instance;
            if (world != null)
            {
                GUILayout.Label("Chunks: " + world.LoadedChunkCount);
            }

            var time = FindObjectOfType<TimeSystem>();
            if (time != null)
            {
                GUILayout.Label("Day " + time.DayNumber + " Time: " + (time.TimeOfDay01 * 24f).ToString("F1") + "h");
            }

            GUILayout.Label("Controls:");
            GUILayout.Label("WASD - Move");
            GUILayout.Label("B - Toggle build mode");
            GUILayout.Label("1-5 - Select block");
            GUILayout.Label("LMB - Mine/Use");
            GUILayout.Label("Tab - Inventory");

            GUILayout.EndArea();
        }
    }
}
