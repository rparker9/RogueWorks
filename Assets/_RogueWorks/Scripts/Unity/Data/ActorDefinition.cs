using RogueWorks.Core.Model;            // TeamId
using RogueWorks.Core.Primitives;       // GridPos
using RogueWorks.Unity.Presentation;    // ActorView
using System.Collections.Generic;
using UnityEngine;

namespace RogueWorks.Unity.Data
{
    /// <summary>
    /// Designer-authored archetype for spawning actors.
    /// Pure data: team, energy params, default facing, starting skills, optional prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "NewActorDefinition", menuName = "RogueWorks/Actor Definition", order = 0)]
    public sealed class ActorDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in UI/logs.")]
        public string displayName = "Actor";

        [Header("Team")]
        [Tooltip("Team for friend/foe logic.")]
        public TeamId team = TeamId.Enemy;

        [Header("Energy/Turn")]
        [Min(1)] public int maxEnergy = 100;
        [Min(1)] public int energyGainPerTick = 10;
        [Min(1)] public int actionCost = 10;

        [Header("Facing")]
        [Tooltip("Default facing as unit grid vector (e.g., (1,0) = right / east).")]
        public GridPos defaultFacing = new GridPos(1, 0);

        [Header("Skills")]
        [Tooltip("Initial skills granted to this actor.")]
        public List<SkillDefinition> startingSkills = new();

        [Header("View (Optional)")]
        [Tooltip("Optional view prefab override for this actor archetype.")]
        public ActorView prefabOverride;
    }
}
