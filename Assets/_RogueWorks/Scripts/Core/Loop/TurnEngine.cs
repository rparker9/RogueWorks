// File: Assets/_RogueWorks/Scripts/Core/Loop/TurnEngine.cs
using RogueWorks.Core.Actions;
using RogueWorks.Core.Animation;
using RogueWorks.Core.Controllers;
using RogueWorks.Core.Model;
using RogueWorks.Core.Skills;
using System.Collections.Generic;
using System.Linq;

namespace RogueWorks.Core.Loop
{
    /// <summary>
    /// Advances the world state in discrete turn slices (“ticks”).
    /// Collects intents from controllers, maps them to actions, applies those actions,
    /// and returns a list of AnimationRequests for the presentation layer.
    /// </summary>
    public sealed class TurnEngine
    {
        // Controller registry: actorId -> controller (player or AI).
        private readonly Dictionary<int, IActorController> _controllers = new();

        // Actors that picked a discrete action this tick (i.e., Skill).
        private readonly HashSet<int> _actorsWithDiscreteThisTick = new();

        public TurnEngine() { }

        /// <summary>Attach a controller to an actor.</summary>
        public void RegisterController(int actorId, IActorController controller) => _controllers[actorId] = controller;

        /// <summary>Returns true if the intent type is discrete (not continuous).</summary>
        private static bool IsDiscrete(IntentType t) => t == IntentType.Skill;

        /// <summary>
        /// One tick: accrue energy, pull intents, apply actions, emit animation requests.
        /// </summary>
        public List<AnimationRequest> Tick(World world, EnergyClock clock)
        {
            _actorsWithDiscreteThisTick.Clear();
            var emitted = new List<AnimationRequest>();

            // 1) Time/energy.
            clock.Tick(world);

            // 2) Gather intents from actors that can afford an action.
            var ready = world.Actors.Where(a => a.Energy >= a.ActionCost).ToList();
            var intents = new List<(Actor actor, IAction action)>();

            foreach (var a in ready)
            {
                if (!_controllers.TryGetValue(a.Id, out var ctrl))
                    continue;

                var intentOpt = ctrl.GetIntent();
                if (intentOpt is null)
                    continue;

                var intent = intentOpt.Value;

                // Map intent -> action. For Skill, resolve from the ACTOR’s own skills.
                IAction action = intent.Type switch
                {
                    IntentType.Move => new MoveAction(intent.Direction),
                    IntentType.Rotate => new RotateAction(intent.Direction),
                    IntentType.Skill => ResolveSkillAction(a, intent),
                    _ => null
                };
                if (action == null) continue;

                if (IsDiscrete(intent.Type))
                    _actorsWithDiscreteThisTick.Add(a.Id);

                intents.Add((a, action));
            }

            if (intents.Count == 0)
                return emitted;

            // 3) Split: moves/rotates vs. skills.
            var moveIntents = intents.Where(t => t.action is MoveAction || t.action is RotateAction).ToList();
            var skillIntents = intents.Where(t => t.action is SkillAction).ToList();

            // If an actor picked a discrete (skill), drop their continuous this tick.
            if (_actorsWithDiscreteThisTick.Count > 0)
                moveIntents = moveIntents.Where(t => !_actorsWithDiscreteThisTick.Contains(t.actor.Id)).ToList();

            // 4) Apply movement “in parallel”.
            foreach (var (actor, action) in moveIntents)
            {
                var outcome = action.Apply(world, actor);

                /// (label-only, plus null-safe animation add)
                if (outcome.ConsumedTurn && actor.TryConsumeForAction())
                {
                    if (!string.IsNullOrEmpty(outcome.LogText))
                        emitted.Add(AnimationRequest.Label(outcome.LogText));
                }
                
                if (outcome.Animations != null && outcome.Animations.Count > 0)
                    emitted.AddRange(outcome.Animations);
            }

            // 5) Apply skills “sequentially”: Player team first, then actor id.
            foreach (var (actor, action) in skillIntents
                         .OrderByDescending(t => t.actor.Team == TeamId.Player)
                         .ThenBy(t => t.actor.Id))
            {
                var outcome = action.Apply(world, actor);
                if (outcome.ConsumedTurn && actor.TryConsumeForAction())
                {
                    if (!string.IsNullOrEmpty(outcome.LogText))
                        emitted.Add(AnimationRequest.Label(outcome.LogText));
                }
                if (outcome.Animations != null && outcome.Animations.Count > 0)
                    emitted.AddRange(outcome.Animations);
            }

            return emitted;
        }

        /// <summary>
        /// Resolve a skill intent using the actor’s own skill list.
        /// </summary>
        private static IAction ResolveSkillAction(Actor actor, ActionIntent intent)
        {
            // If no skill id provided, it’s a “no skill” intent (e.g., cancel).
            if (!string.IsNullOrEmpty(intent.SkillId) && actor.TryGetSkill(intent.SkillId, out var s))
                return new SkillAction(s, intent.Direction);

            // Skill id not found: fallback to a no-op skill action (whiff).
            return new SkillAction(null, intent.Direction);
        }
    }
}
