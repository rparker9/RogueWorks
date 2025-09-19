using RogueWorks.Core.Animation;
using RogueWorks.Core.Model;
using RogueWorks.Core.Primitives;
using RogueWorks.Core.Skills;
using System.Collections.Generic;
using System.Linq;

namespace RogueWorks.Core.Actions
{
    /// <summary>
    /// Applies intent to the world/actor and returns an outcome (state + animation requests).
    /// </summary>
    public interface IAction
    {
        /// <summary>
        /// Apply to world and actor. Must be deterministic and engine-agnostic.
        /// </summary>
        ActionOutcome Apply(World world, Actor self);
    }

    /// <summary>
    /// Attempts to step one tile in Direction. Consumes a turn only if the move succeeds.
    /// Disallows diagonal "corner cutting": a diagonal is only allowed if BOTH adjacent
    /// cardinal tiles are walkable (i.e., you can slide past corners safely).
    /// </summary>
    public sealed class MoveAction : IAction
    {
        private readonly GridPos _dir;
        public MoveAction(GridPos dir) { _dir = dir; }

        /// <summary>
        /// Apply move action to world and actor. 
        /// </summary>
        /// <param name="world"></param>
        /// <param name="self"></param>
        /// <returns></returns>
        public ActionOutcome Apply(World world, Actor self)
        {
            var outcome = new ActionOutcome();

            // Always face the intended move direction and notify the view layer.
            self.SetFacing(_dir);
            outcome.Add(AnimationRequest.Face(self.Id, _dir)); // non-blocking orientation change

            // Compute target tile.
            var dest = new GridPos(self.Position.X + _dir.X, self.Position.Y + _dir.Y);

            // If attempting a diagonal, allow it as long as NOT blocked on both sides.
            // i.e., reject only when BOTH adjacent cardinals are unwalkable.
            bool isDiagonal = _dir.X != 0 && _dir.Y != 0;
            if (isDiagonal)
            {
                var adjX = new GridPos(self.Position.X + _dir.X, self.Position.Y);
                var adjY = new GridPos(self.Position.X, self.Position.Y + _dir.Y);

                // Old (strict): if (!walkable(adjX) || !walkable(adjY)) reject
                // New (loose): reject ONLY if both are blocked
                bool sideXBlocked = !world.IsWalkable(adjX);
                bool sideYBlocked = !world.IsWalkable(adjY);
                if (sideXBlocked && sideYBlocked)
                    return outcome; // both sides blocked -> no diagonal slip
            }


            // Final walkability gate for the destination.
            if (!world.IsWalkable(dest))
                return outcome; // blocked: no move, no turn consumption

            // Apply state: move succeeds -> consumes a turn.
            var from = self.Position;
            self.SetPosition(dest);
            outcome.ConsumedTurn = true;

            // Emit movement animation (non-blocking). Face() above guarantees snap when needed.
            outcome.Add(AnimationRequest.Movement(self.Id, from, dest));
            return outcome;
        }
    }

    /// <summary>
    /// Rotate actor to face Direction. Never consumes a turn.
    /// </summary>
    public sealed class RotateAction : IAction
    {
        private readonly GridPos _dir;
        public RotateAction(GridPos dir) { _dir = dir; }

        public ActionOutcome Apply(World world, Actor self)
        {
            var outcome = new ActionOutcome { ConsumedTurn = false };
            self.SetFacing(_dir);
            // Emit a facing change so the Unity view rotates immediately.
            outcome.Add(AnimationRequest.Face(self.Id, _dir));
            return outcome;
        }
    }


    /// <summary>
    /// Attempts using a skill in the facing (or otherwise provided) direction. 
    /// Consumes a turn even if it whiffs for Player team.
    /// </summary>
    public sealed class SkillAction : IAction
    {
        private readonly Skill _skill;          // May be null (whiff fallback).
        private readonly GridPos _dir;
        private readonly bool _playWhiffForPlayer;

        /// <summary>
        /// Create a skill action. If the direction is GridPos.Zero, the actor's current facing is used.
        /// </summary>
        public SkillAction(Skill skill, GridPos dir, bool playWhiffForPlayer = true)
        {
            _skill = skill;
            _dir = dir;
            _playWhiffForPlayer = playWhiffForPlayer;
        }

        public ActionOutcome Apply(World world, Actor self)
        {
            var outcome = new ActionOutcome();

            var dir = _dir.Equals(GridPos.Zero) ? self.Facing : _dir;
            self.SetFacing(dir);

            // Baseline label; Router will log at windup-time if desired
            string label = $"{self.Name} uses {_skill.DisplayName}!";

            // Compute affected tiles (minimal set; extend later)
            var tiles = ResolveTiles(world, self.Position, dir, _skill);

            // Collect enemy actors on those tiles
            var targets = new List<Actor>();
            foreach (var t in tiles)
            {
                foreach (var a in world.ActorsAt(t))
                {
                    if (a.Id == self.Id) continue; // skip self unless Self/current-tile in future
                    if (a.Team == self.Team) continue; // offensive default (extend for FriendlyOnly)
                    if (!targets.Any(x => x.Id == a.Id))
                        targets.Add(a);
                }
                // Stop if non-piercing Line hits something blocking
                if (_skill?.Targeting == SkillTargeting.Line && _skill.Piercing == false && world.ActorsAt(t).Any())
                    break;
            }

            // Consume turn rules
            if (targets.Count > 0 || (_playWhiffForPlayer && self.Team == TeamId.Player))
                outcome.ConsumedTurn = true;

            // Get the tile in front for animation purposes (fallback to front if no tiles) and the target refs.
            GridPos to = tiles.DefaultIfEmpty(self.Position + dir).First();
            TargetRef[] targetRefs = targets.Select(t => TargetRef.Of(t.Id, t.Position)).ToArray();

            // Emit a blocking skill animation request even if no targets hit.
            outcome.Add(AnimationRequest.Skill(
                actorId: self.Id,
                from: self.Position,
                to: to,
                targets: targetRefs,
                label: label
            ));

            return outcome;
        }

        // --- helpers ---
        private static IEnumerable<GridPos> ResolveTiles(World world, GridPos origin, GridPos dir, Skill s)
        {
            if (s == null) // fallback: swing the tile in front
            {
                yield return origin + dir;
                yield break;
            }

            switch (s.Targeting)
            {
                case SkillTargeting.Self:
                case SkillTargeting.CurrentTile:
                    yield return origin;
                    yield break;

                case SkillTargeting.TileInFront:
                    yield return origin + dir;
                    yield break;

                case SkillTargeting.Line:
                    {
                        var step = dir;
                        if (step.Equals(GridPos.Zero)) yield break;
                        var t = origin + step;
                        int steps = 0;
                        while (world.InInBounds(t) && steps < s.MaxRange)
                        {
                            yield return t;
                            // If not piercing and there is a blocking entity, stop after this tile.
                            if (!s.Piercing && world.ActorsAt(t).Any())
                                yield break;
                            t += step;
                            steps++;
                        }
                        yield break;
                    }

                default:
                    // Not yet implemented shapes (Ring/Arc/Room/Floor/Allies/RandomDirection)
                    // can be added here incrementally without touching Unity.
                    yield return origin + dir; // safe fallback
                    yield break;
            }
        }
    }
}
