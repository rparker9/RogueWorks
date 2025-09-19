using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

using RogueWorks.Core.Animation;
using RogueWorks.Core.Primitives;

using RogueWorks.Unity.Animation.Sinks;
using RogueWorks.Unity.Presentation;

namespace RogueWorks.Unity.Animation
{
    /// <summary>
    /// AnimationRouter: build clip graphs from semantic requests using profile, sinks, mappers and lookups.
    /// </summary>
    public sealed class AnimationRouter
    {
        private readonly PresentationProfile _profile;

        // Sinks
        private readonly IVfxSink _vfx;
        private readonly ISfxSink _sfx;
        private readonly ILogSink _log;
        private readonly ICameraShakeSink _shake;

        // Grid -> world
        private readonly System.Func<GridPos, float, Vector3> _toWorld;

        // Actor views
        private readonly IActorViewLookup _views;

        /// <summary>Create a router with sinks, mapper and lookups.</summary>
        public AnimationRouter(
            PresentationProfile profile,
            IVfxSink vfx, ISfxSink sfx, ILogSink log, ICameraShakeSink shake,
            System.Func<GridPos, float, Vector3> toWorld,
            IActorViewLookup viewLookup)
        {
            _profile = profile;
            _vfx = vfx; _sfx = sfx; _log = log; _shake = shake;
            _toWorld = toWorld;
            _views = viewLookup;

        }

        /// <summary>Build a clip graph for a single semantic request.</summary>
        public IClip Build(AnimationRequest rq)
        {
            return rq.Type switch
            {
                AnimationType.Movement => BuildMovement(rq),
                AnimationType.Face => BuildFace(rq),
                AnimationType.Skill => BuildSkill(rq),
                AnimationType.TakeDamage => BuildHit(rq),
                AnimationType.Death => BuildDeath(rq),
                AnimationType.Vfx => BuildVfxOnly(rq),
                AnimationType.None => null,             // explicit: no clip for label-only
                _ => null                               // let orchestrator log [NoClip]
            };
        }

        /// <summary>
        /// BuildMovement: step SFX (optional) + move the visual at constant speed. Non-blocking.
        /// </summary>
        /// <param name="rq"></param>
        /// <returns></returns>
        private IClip BuildMovement(AnimationRequest rq)
        {
            var cues = _profile.Resolve(AnimationType.Movement);
            var par = new ParallelGroup();

            // Optional step SFX
            var step = _profile.FindClip(cues.StepSfxId);
            if (step) par.Add(new CoroutineClip(_ => _sfx.PlayOneShot(step, _toWorld(rq.FromPosition, 0f))));

            // Move visual
            par.Add(new CoroutineClip(_ => MoveVisual(rq)));
            return par;
        }

        /// <summary>
        /// BuildFace: rotate view to FacingDirection. Non-blocking.
        /// </summary>
        /// <param name="rq"></param>
        /// <returns></returns>
        private IClip BuildFace(AnimationRequest rq)
        {
            // Rotate view to FacingDirection; non-blocking.
            return new CoroutineClip(_ => RotateVisual(rq.ActorId, rq.FacingDirection));
        }


        /// <summary>
        /// BuildSkill: windup (swing anim + whoosh + shake + log) -> travel (proj/beam) -> per-target impacts (hit anim + impact VFX + hit SFX + shake).
        /// </summary>
        /// <param name="rq"></param>
        /// <returns></returns>
        private IClip BuildSkill(AnimationRequest rq)
        {
            // Resolve cues from profile
            PresentationProfile.CuePack cues = _profile.Resolve(AnimationType.Skill, rq.SkillId, rq.Tag);
            SequenceGroup seq = new SequenceGroup();

            // windup
            var windup = new ParallelGroup();

            // whoosh SFX
            var whoosh = _profile.FindClip(cues.WhooshSfxId);
            if (whoosh) 
                windup.Add(new CoroutineClip(_ => _sfx.PlayOneShot(whoosh, _toWorld(rq.FromPosition, rq.Vfx?.YOffset ?? 0f))));

            // attack anim
            windup.Add(new CoroutineClip(_ => PlayAttackAnim(rq, cues.AttackFallback)));

            // shake
            windup.Add(new CoroutineClip(_ => _shake.Shake(cues.WindupShakeDur, cues.WindupShakeAmp)));

            // log text (if any)
            if (!string.IsNullOrEmpty(rq.LogTextLabel)) windup.Add(new CoroutineClip(_ => _log.Write(rq.LogTextLabel)));
            seq.Add(windup);

            // travel (proj/beam)
            if (rq.Vfx != null && 
                rq.Vfx.Family == VfxFamily.Projectile && 
                !string.IsNullOrEmpty(rq.Vfx.ProjectileId))
            {
                var proj = _profile.FindProjectile(rq.Vfx.ProjectileId);
                if (proj) seq.Add(new CoroutineClip(_ => _vfx.PlayProjectile(proj,
                    _toWorld(rq.FromPosition, rq.Vfx.YOffset), _toWorld(rq.ToPosition, rq.Vfx.YOffset), cues.ProjectileTime)));
            }
            else if (rq.Vfx != null && rq.Vfx.Family == VfxFamily.Beam && !string.IsNullOrEmpty(rq.Vfx.BeamId))
            {
                var beam = _profile.FindEffect(rq.Vfx.BeamId);
                if (beam) seq.Add(new CoroutineClip(_ => _vfx.PlayBeam(beam,
                    _toWorld(rq.FromPosition, rq.Vfx.YOffset), _toWorld(rq.ToPosition, rq.Vfx.YOffset), cues.BeamTime)));
            }

            // impacts
            if (rq.Targets != null && rq.Targets.Count > 0)
            {
                var impactsSeq = new SequenceGroup();
                foreach (var t in rq.Targets)
                {
                    var tgtPar = new ParallelGroup();
                    tgtPar.Add(new CoroutineClip(_ => PlayHitAnimOn(t.ActorId, cues.HitFallback)));

                    var pos = _toWorld(t.Position, rq.Vfx?.YOffset ?? 0f);
                    var impactAsset = _profile.FindEffect(rq.Vfx?.ImpactId ?? cues.ImpactVfxId);
                    if (impactAsset) tgtPar.Add(new CoroutineClip(_ => _vfx.PlayOneShot(impactAsset, pos)));

                    var hitSfx = _profile.FindClip(cues.HitSfxId);
                    if (hitSfx) tgtPar.Add(new CoroutineClip(_ => _sfx.PlayOneShot(hitSfx, pos)));

                    tgtPar.Add(new CoroutineClip(_ => _shake.Shake(cues.ImpactShakeDur, cues.ImpactShakeAmp)));

                    impactsSeq.Add(tgtPar);
                }
                seq.Add(impactsSeq);
            }
            return seq;
        }

        /// <summary>
        /// BuildHit: hit anim + impact VFX + hit SFX. Blocking.
        /// </summary>
        /// <param name="rq"></param>
        /// <returns></returns>
        private IClip BuildHit(AnimationRequest rq)
        {
            var cues = _profile.Resolve(AnimationType.TakeDamage, rq.SkillId, rq.Tag);
            var pos = _toWorld(rq.ToPosition, rq.Vfx?.YOffset ?? 0f);
            var par = new ParallelGroup();

            par.Add(new CoroutineClip(_ => PlayHitAnim(rq, cues.HitFallback)));
            var impact = _profile.FindEffect(rq.Vfx?.ImpactId ?? cues.ImpactVfxId);
            if (impact) par.Add(new CoroutineClip(_ => _vfx.PlayOneShot(impact, pos)));
            var sfx = _profile.FindClip(cues.HitSfxId);
            if (sfx) par.Add(new CoroutineClip(_ => _sfx.PlayOneShot(sfx, pos)));
            return par;
        }

        /// <summary>
        /// BuildDeath: death anim + death burst VFX + death SFX. Blocking.
        /// </summary>
        /// <param name="rq"></param>
        /// <returns></returns>
        private IClip BuildDeath(AnimationRequest rq)
        {
            var cues = _profile.Resolve(AnimationType.Death, rq.SkillId, rq.Tag);
            var seq = new SequenceGroup();
            seq.Add(new CoroutineClip(_ => PlayDeathAnim(rq, cues.DeathFallback)));

            var burst = _profile.FindEffect(cues.DeathBurstVfxId);
            if (burst) seq.Add(new CoroutineClip(_ => _vfx.PlayOneShot(burst, _toWorld(rq.ToPosition, rq.Vfx?.YOffset ?? 0f))));
            var sfx = _profile.FindClip(cues.DeathSfxId);
            if (sfx) seq.Add(new CoroutineClip(_ => _sfx.PlayOneShot(sfx, _toWorld(rq.ToPosition, rq.Vfx?.YOffset ?? 0f))));
            seq.Add(new DelayClip(0.05f));
            return seq;
        }

        /// <summary>
        /// BuildVfxOnly: play a one-shot impact VFX at ToPosition. Non-blocking.
        /// </summary>
        /// <param name="rq"></param>
        /// <returns></returns>
        private IClip BuildVfxOnly(AnimationRequest rq)
        {
            if (rq.Vfx == null || string.IsNullOrEmpty(rq.Vfx.ImpactId))
                return new DelayClip(0f);

            var asset = _profile.FindEffect(rq.Vfx.ImpactId);  // use profile
            if (!asset) return new DelayClip(0f);

            var pos = _toWorld(rq.ToPosition, rq.Vfx.YOffset);
            return new CoroutineClip(_ => _vfx.PlayOneShot(asset, pos));
        }


        /// <summary>
        /// MoveVisual: move the actor's view to the target position at constant speed.
        /// </summary>
        /// <param name="rq"></param>
        /// <returns></returns>
        private IEnumerator MoveVisual(AnimationRequest rq)
        {
            if (_views != null && _views.TryGet(rq.ActorId, out var view))
                yield return view.MoveTo(_toWorld(rq.ToPosition, rq.Vfx?.YOffset ?? 0f));
            else
                yield return null;
        }

        /// <summary>
        /// RotateVisual: rotate the actor's view to face the given grid direction.
        /// </summary>
        /// <param name="actorId"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        private IEnumerator RotateVisual(int actorId, GridPos dir)
        {
            if (_views != null && _views.TryGet(actorId, out var view))
            {
                float yaw = GridDirToYawDeg(dir);
                if (!float.IsNaN(yaw))
                    yield return view.RotateToYaw(yaw);
            }
            else yield return null;
        }

        /// <summary>
        /// PlayAttackAnim: play the attack animation on the actor's view (stub).
        /// If no view or no anim, just wait the fallback time (if any).
        /// </summary>
        /// <param name="rq"></param>
        /// <returns></returns>
        private IEnumerator PlayAttackAnim(AnimationRequest rq, float fallbackSec)
        {
            // If no view or no anim, just wait the fallback time (if any)
            if (_views != null && _views.TryGet(rq.ActorId, out var view))
                yield return view.PlayAttack();
            else
                yield return new WaitForSeconds(Mathf.Max(0f, fallbackSec));
        }

        /// <summary>
        /// PlayHitAnim: play the hit reaction animation on the actor's view (stub).
        /// </summary>
        /// <param name="rq"></param>
        /// <returns></returns>
        private IEnumerator PlayHitAnim(AnimationRequest rq, float fallbackSec)
        {
            if (_views != null && _views.TryGet(rq.ActorId, out var view))
                yield return view.PlayHit();
            else
                yield return new WaitForSeconds(Mathf.Max(0f, fallbackSec));
        }

        /// <summary>
        /// PlayHitAnimOn: play the hit reaction animation on the given actor's view (stub).
        /// </summary>
        /// <param name="actorId"></param>
        /// <returns></returns>
        private IEnumerator PlayHitAnimOn(int actorId, float fallbackSec)
        {
            if (_views != null && _views.TryGet(actorId, out var view))
                yield return view.PlayHit();
            else
                yield return new WaitForSeconds(Mathf.Max(0f, fallbackSec));
        }

        /// <summary>
        /// PlayDeathAnim: play the death animation on the actor's view (stub).
        /// </summary>
        /// <param name="rq"></param>
        /// <param name="fallbackSec"></param>
        /// <returns></returns>
        private IEnumerator PlayDeathAnim(AnimationRequest rq, float fallbackSec)
        {
            if (_views != null && _views.TryGet(rq.ActorId, out var view))
                yield return view.PlayDeath();
            else
                yield return new WaitForSeconds(Mathf.Max(0f, fallbackSec));
        }

        private static float GridDirToYawDeg(GridPos dir)
        {
            // Zero means "no change"
            if (dir.X == 0 && dir.Y == 0) return float.NaN;

            // +Z (Y>0) is 0°, +X (X>0) is 90° -> use atan2(x, y)
            float yaw = Mathf.Atan2(dir.X, dir.Y) * Mathf.Rad2Deg;
            if (yaw < 0f) yaw += 360f;
            return yaw;
        }
    }
}
