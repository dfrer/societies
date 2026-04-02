using System.IO;
using UnityEngine;
using Societies.Runtime.World;

namespace Societies.Runtime.Persistence
{
    public class WorldSaver : MonoBehaviour
    {
        public static WorldSaver Instance { get; private set; }

        private string _savePath;
        private float _saveTimer;
        private const float SAVE_INTERVAL = 30f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _savePath = Path.Combine(Application.persistentDataPath, "saves");
            Directory.CreateDirectory(_savePath);
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _saveTimer += Time.deltaTime;
            if (_saveTimer >= SAVE_INTERVAL)
            {
                _saveTimer = 0f;
                SaveWorld("autosave");
            }
        }

        public void SaveWorld(string saveName)
        {
            int seed = VoxelWorld.Instance != null ? VoxelWorld.Instance.WorldSeed : 0;
            File.WriteAllText(Path.Combine(_savePath, $"{saveName}_seed.txt"), seed.ToString());

            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Vector3 pos = player.transform.position;
                File.WriteAllText(
                    Path.Combine(_savePath, $"{saveName}_player.txt"),
                    $"{pos.x},{pos.y},{pos.z}");
            }

            Debug.Log("[WorldSaver] Saved: " + saveName);
        }

        public void LoadWorld(string saveName)
        {
            string seedPath = Path.Combine(_savePath, $"{saveName}_seed.txt");
            if (File.Exists(seedPath))
            {
                int seed = int.Parse(File.ReadAllText(seedPath));
                PlayerPrefs.SetInt("WorldSeed", seed);
            }

            Debug.Log("[WorldSaver] Loaded: " + saveName);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
