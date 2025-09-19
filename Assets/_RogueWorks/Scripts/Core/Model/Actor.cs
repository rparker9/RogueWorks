// File: Assets/_RogueWorks/Scripts/Core/Model/Actor.cs
using System.Collections.Generic;
using RogueWorks.Core.Primitives;
using RogueWorks.Core.Skills;

namespace RogueWorks.Core.Model
{
    public enum TeamId { None = 0, Player = 1, Enemy = 2 }

    /// <summary>Minimal actor model used by the turn engine. Pure state, no Unity.</summary>
    public sealed class Actor
    {
        public int Id { get; }
        public string Name { get; }
        public TeamId Team { get; }
        public GridPos Position { get; private set; }
        public GridPos Facing { get; private set; } = GridPos.Up;
        public int Energy { get; private set; }
        public int MaxEnergy { get; private set; }
        public int EnergyGainPerTick { get; }
        public int ActionCost { get; }

        // ---- Skills owned by this actor (engine-agnostic) ----
        private readonly List<Skill> _skills = new();
        public IReadOnlyList<Skill> Skills => _skills;

        /// <summary>
        /// Basic constructor with energy parameters. Pass an optional initial skill list.
        /// </summary>
        public Actor(int id, string name, TeamId team, GridPos pos,
                     int maxEnergy = 100, int energyGainPerTick = 10, int actionCost = 10,
                     IEnumerable<Skill> initialSkills = null)
        {
            Id = id; Name = name; Team = team; Position = pos;
            MaxEnergy = maxEnergy; EnergyGainPerTick = energyGainPerTick; ActionCost = actionCost;
            Energy = 0;

            // If initial skills provided, copy them in.
            if (initialSkills != null)
                _skills.AddRange(initialSkills);
        }

        /// <summary>Add/replace all skills at once (useful when seeding from Unity).</summary>
        public void SetSkills(IEnumerable<Skill> skills)
        {
            _skills.Clear();
            if (skills != null) _skills.AddRange(skills);
        }

        /// <summary>Add a single skill.</summary>
        public void AddSkill(Skill s)
        {
            if (s != null && !_skills.Exists(k => k.Id == s.Id))
                _skills.Add(s);
        }

        /// <summary>Try to get a skill by id from this actor.</summary>
        public bool TryGetSkill(string id, out Skill skill)
        {
            for (int i = 0; i < _skills.Count; i++)
                if (_skills[i].Id == id) { skill = _skills[i]; return true; }
            skill = null; return false;
        }

        public void GainEnergy()
        {
            if (Energy < MaxEnergy) Energy += EnergyGainPerTick;
        }

        public bool TryConsumeForAction()
        {
            if (Energy < ActionCost) return false;
            Energy -= ActionCost; return true;
        }

        public void SetPosition(GridPos p) => Position = p;
        public void SetFacing(GridPos dir) => Facing = dir;
    }
}
