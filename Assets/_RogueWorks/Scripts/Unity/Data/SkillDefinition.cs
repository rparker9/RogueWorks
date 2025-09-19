using UnityEngine;
using RogueWorks.Core.Skills;

namespace RogueWorks.Unity.Data
{
    [CreateAssetMenu(menuName = "RogueWorks/Skill Definition")]
    public class SkillDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public Sprite icon;
        [TextArea] public string description;

        // Add these so your catalog can use real values (remove defaults from catalog afterwards)
        public SkillType skillType = SkillType.Special;
        public DamageType damageType = DamageType.Normal;
        public SkillTargeting targeting = SkillTargeting.TileInFront;

        [Min(1)] public int power = 40;
        [Range(1, 100)] public int accuracy = 100;
        [Min(1)] public int maxRange = 8;
        [Range(30, 360)] public int arcDegrees = 90;
        [Min(1)] public int ringRadius = 2;
        public bool friendlyOnly = false;
        public bool piercing = false;
        public string statusId;
        [Range(0, 100)] public int statusChance = 0;
    }

}
