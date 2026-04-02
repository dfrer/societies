using System.Collections.Generic;
using UnityEngine;

namespace Societies.Runtime.Core
{
    public enum SkillType
    {
        Mining,
        Woodcutting,
        Crafting,
        Building,
        Farming,
        Cooking,
        Trading
    }

    public class PlayerSkills : MonoBehaviour
    {
        public Dictionary<SkillType, int> Skills = new();

        private void Awake()
        {
            foreach (SkillType skill in System.Enum.GetValues(typeof(SkillType)))
            {
                Skills[skill] = 1;
            }
        }

        public void AddXP(SkillType skill, float xp)
        {
            Skills[skill] += (int)(xp * 0.1f);
        }

        public int GetLevel(SkillType skill) => Skills[skill];

        public float GetMiningSpeed() => 1f + (Skills[SkillType.Mining] - 1) * 0.1f;
    }
}
