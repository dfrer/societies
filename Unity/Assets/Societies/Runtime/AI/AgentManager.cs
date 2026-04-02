using UnityEngine;

namespace Societies.Runtime.AI
{
    public class AgentManager : MonoBehaviour
    {
        public GameObject agentPrefab;
        public int agentCount = 10;

        private void Start()
        {
            for (int i = 0; i < agentCount; i++)
            {
                SpawnAgent();
            }
        }

        private void SpawnAgent()
        {
            // Spawn at random position near origin
            float x = Random.Range(-20, 20);
            float z = Random.Range(-20, 20);
            Vector3 pos = new Vector3(x, 50, z); // Will fall to ground

            GameObject agent = agentPrefab != null
                ? Instantiate(agentPrefab, pos, Quaternion.identity)
                : new GameObject("Agent_" + Random.Range(1000, 9999));

            agent.name = "Agent_" + Random.Range(1000, 9999);
            agent.transform.position = pos;

            if (agent.GetComponent<AgentBrain>() == null)
            {
                agent.AddComponent<AgentBrain>();
            }

            if (agent.GetComponent<CapsuleCollider>() == null)
            {
                var collider = agent.AddComponent<CapsuleCollider>();
                collider.height = 1.8f;
                collider.radius = 0.3f;
            }
        }
    }
}
