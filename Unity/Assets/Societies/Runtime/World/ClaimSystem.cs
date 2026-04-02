using System.Collections.Generic;
using UnityEngine;

namespace Societies.Runtime.World
{
    public class ClaimSystem : MonoBehaviour
    {
        public static ClaimSystem Instance { get; private set; }

        public struct Claim
        {
            public string ClaimId;
            public string OwnerId;
            public Vector3 Center;
            public float Radius;
        }

        private readonly Dictionary<string, Claim> _claims = new();

        private void Awake()
        {
            Instance = this;
        }

        // Create a personal claim
        public string CreateClaim(string playerId, Vector3 center, float radius = 10f)
        {
            string claimId = System.Guid.NewGuid().ToString();
            _claims[claimId] = new Claim
            {
                ClaimId = claimId,
                OwnerId = playerId,
                Center = center,
                Radius = radius
            };
            return claimId;
        }

        // Check if position is in a claim
        public bool CanBuild(Vector3 position, string playerId)
        {
            foreach (var claim in _claims.Values)
            {
                float dist = Vector3.Distance(position, claim.Center);
                if (dist < claim.Radius)
                {
                    // Allow if owner
                    return claim.OwnerId == playerId;
                }
            }

            return true; // No claim = build allowed
        }
    }
}
