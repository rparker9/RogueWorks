using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using RogueWorks.Core.Model;
using RogueWorks.Core.Primitives;
using RogueWorks.Core.Skills;

using RogueWorks.Unity.Data;
using RogueWorks.Unity.Presentation;


namespace RogueWorks.Unity.Runtime
{
    /// <summary>
    /// Translates <see cref="RosterDefinition"/> entries into Core Actors and ActorView instances.
    /// Pure POCO: no MonoBehaviour inheritance.
    /// </summary>
    public sealed class ActorSpawner
    {
        private readonly World _world;                 // Core world to add actors into
        private readonly ActorViewRegistry _views;     // View registry for id->view bindings
        private readonly GridToWorldMapper _mapper;    // Grid->world for initial placement
        private readonly ActorView _fallbackView;      // Optional default view prefab
        private readonly float _actorYOffset;          // Constant Y offset for spawn

        /// <summary>Create a spawner bound to a world/registry/mapper.</summary>
        public ActorSpawner(World world, ActorViewRegistry views, GridToWorldMapper mapper, ActorView fallbackView, float actorYOffset)
        {
            _world = world;
            _views = views;
            _mapper = mapper;
            _fallbackView = fallbackView;
            _actorYOffset = actorYOffset;
        }

        /// <summary>
        /// Spawn all entries from a roster. Returns the last player-controlled actor id (or -1 if none).
        /// </summary>
        public int SpawnAll(RosterDefinition roster)
        {
            int playerId = -1;

            if (roster == null || roster.entries == null) return playerId;

            foreach (var entry in roster.entries)
            {
                if (entry?.definition == null) continue;

                // Positions listed explicitly -> spawn at each.
                if (entry.positions != null && entry.positions.Count > 0)
                {
                    foreach (var p in entry.positions)
                    {
                        var clamped = ClampToWorld(new GridPos(p.x, p.y));
                        var pos = FindValidSpawn(clamped);
                        var actor = CreateActor(entry.definition, pos);
                        SpawnView(entry.definition, actor);
                        if (entry.isPlayerControlled) playerId = actor.Id;
                    }
                }
                else
                {
                    // Count-based spawns -> place on any valid tile.
                    int count = Mathf.Max(1, entry.count);
                    for (int i = 0; i < count; i++)
                    {
                        var pos = FindAnyOpen();
                        var actor = CreateActor(entry.definition, pos);
                        SpawnView(entry.definition, actor);
                        if (entry.isPlayerControlled) playerId = actor.Id;
                    }
                }
            }

            return playerId;
        }

        /// <summary>
        /// Create a Core Actor from definition and register in the world.</summary>
        private Actor CreateActor(ActorDefinition def, GridPos pos)
        {
            // Map SkillDefinition -> Core.Skill (data-only copy)
            IEnumerable<Skill> coreSkills = (def.startingSkills ?? Enumerable.Empty<SkillDefinition>())
                .Where(sd => sd != null && !string.IsNullOrWhiteSpace(sd.id))
                .Select(sd => new Skill
                {
                    Id = sd.id,
                    DisplayName = string.IsNullOrWhiteSpace(sd.displayName) ? sd.id : sd.displayName,
                    SkillType = sd.skillType,
                    DamageType = sd.damageType,
                    Targeting = sd.targeting,
                    Power = sd.power,
                    Accuracy = sd.accuracy,
                    MaxRange = sd.maxRange,
                    ArcDegrees = sd.arcDegrees,
                    RingRadius = sd.ringRadius,
                    FriendlyOnly = sd.friendlyOnly,
                    Piercing = sd.piercing,
                    StatusId = sd.statusId,
                    StatusChance = sd.statusChance
                });

            // Add to world (uses your existing signature)
            var actor = _world.AddActor(
                name: string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName,
                team: def.team,
                position: pos,
                maxEnergy: def.maxEnergy);

            // Assign skills
            actor.SetSkills(coreSkills);

            // Facing
            var f = def.defaultFacing;
            var face = new GridPos(Mathf.Clamp(f.X, -1, 1), Mathf.Clamp(f.Y, -1, 1));
            if (face.X != 0 || face.Y != 0) actor.SetFacing(face);

            return actor;
        }

        /// <summary>
        /// Instantiate an ActorView for the actor and register in the lookup.
        /// </summary>
        private void SpawnView(ActorDefinition def, Actor actor)
        {
            var prefab = def.prefabOverride ? def.prefabOverride : _fallbackView;
            if (!prefab)
            {
                Debug.LogWarning($"[ActorSpawner] No ActorView prefab for '{def.displayName}'. Core-only actor.");
                return;
            }

            var pos = _mapper.ToWorld(actor.Position);
            pos.y += _actorYOffset;

            var view = Object.Instantiate(prefab, pos, Quaternion.identity);
            view.ActorId = actor.Id;
            _views.Register(view);
        }

        // --- Small helpers (matching your GameRuntime logic) ---

        private GridPos ClampToWorld(GridPos p)
        {
            int x = Mathf.Clamp(p.X, 0, _world.Width - 1);
            int y = Mathf.Clamp(p.Y, 0, _world.Height - 1);
            return new GridPos(x, y);
        }

        private GridPos FindAnyOpen()
        {
            for (int y = 0; y < _world.Height; y++)
                for (int x = 0; x < _world.Width; x++)
                {
                    var gp = new GridPos(x, y);
                    if (_world.IsWalkable(gp) && !_world.ActorsAt(gp).Any())
                        return gp;
                }
            return new GridPos(0, 0);
        }

        private GridPos FindValidSpawn(GridPos preferred)
        {
            if (_world.IsWalkable(preferred) && !_world.ActorsAt(preferred).Any())
                return preferred;

            // Small spiral search around the preferred position
            for (int r = 1; r <= 3; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var p = ClampToWorld(new GridPos(preferred.X + dx, preferred.Y + dy));
                        if (_world.IsWalkable(p) && !_world.ActorsAt(p).Any())
                            return p;
                    }
            }
            return FindAnyOpen();
        }
    }
}
