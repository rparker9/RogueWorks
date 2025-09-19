// File: Assets/_RogueWorks/Scripts/Unity/Presentation/PresentationProfile.cs
using System;
using System.Collections.Generic;
using RogueWorks.Core.Animation;
using RogueWorks.Core.Primitives;
using RogueWorks.Unity.Animation.Sinks;
using RogueWorks.Unity.Data;
using UnityEngine;
using UnityEngine.VFX;

namespace RogueWorks.Unity.Presentation
{
    /// <summary>All presentation lookups, defaults, and per-event rules for the AnimationRouter.</summary>
    [CreateAssetMenu(fileName = "PresentationProfile", menuName = "RogueWorks/Presentation/Profile", order = 10)]
    public sealed class PresentationProfile : ScriptableObject
    {
        [Header("Databases (Lookup)")]
        [SerializeField] private VfxDatabase vfxDb;
        [SerializeField] private SfxDatabase sfxDb;

        [Header("Global Defaults (IDs & Tunables)")]
        public string stepSfxId           = "STEP";
        public string swingWhooshSfxId    = "SWING_WHOOSH";
        public string hitSfxId            = "HIT";
        public string hurtSfxId           = "HURT";
        public string deathSfxId          = "DEATH";

        public string defaultImpactVfxId  = "DEFAULT_IMPACT";
        public string deathBurstVfxId     = "DEATH_BURST";

        [Tooltip("seconds, amplitude")]
        public float windupShakeDuration  = 0.05f;
        public float windupShakeAmplitude = 0.07f;
        public float impactShakeDuration  = 0.07f;
        public float impactShakeAmplitude = 0.05f;

        [Tooltip("Fallback pauses when a view is missing (keeps pacing reasonable).")]
        public float attackAnimFallbackDelay = 0.06f;
        public float hitAnimFallbackDelay    = 0.05f;
        public float deathAnimFallbackDelay  = 0.12f;

        [Header("Travel Times")]
        public float projectileTravelTime = 0f;
        public float beamTravelTime       = 0f;

        [Header("Rules (Specific Overrides)")]
        [SerializeField] private List<Rule> rules = new();

        [Serializable]
        public struct RuleKey
        {
            public AnimationType type;        // e.g., Skill, TakeDamage, Death
            public string skillIdContains;    // substring match on skillId (empty = ignore)
            public string tag;                // optional generic tag (e.g., "fire", "ice")
        }

        [Serializable]
        public struct Rule
        {
            public RuleKey key;

            [Header("Override IDs (leave empty to inherit default)")]
            public string whooshSfxId;
            public string impactVfxId;
            public string hitSfxId;
            public string deathBurstVfxId;
            public string deathSfxId;

            [Header("Shake Overrides (-1 = inherit)")]
            public float windupShakeDuration;
            public float windupShakeAmplitude;
            public float impactShakeDuration;
            public float impactShakeAmplitude;

            [Header("Travel Time Overrides (-1 = inherit)")]
            public float projectileTravelTime;
            public float beamTravelTime;
        }

        /// <summary>Value object the Router will use—already merged with defaults.</summary>
        public readonly struct CuePack
        {
            public readonly string StepSfxId, WhooshSfxId, ImpactVfxId, HitSfxId, DeathBurstVfxId, DeathSfxId;
            public readonly float WindupShakeDur, WindupShakeAmp, ImpactShakeDur, ImpactShakeAmp;
            public readonly float ProjectileTime, BeamTime;
            public readonly float AttackFallback, HitFallback, DeathFallback;

            public CuePack(
                string step, string whoosh, string impact, string hit, string deathBurst, string deathSfx,
                float wDur, float wAmp, float iDur, float iAmp,
                float proj, float beam,
                float atkFb, float hitFb, float deathFb)
            {
                StepSfxId = step;
                WhooshSfxId = whoosh;
                ImpactVfxId = impact;
                HitSfxId = hit;
                DeathBurstVfxId = deathBurst;
                DeathSfxId = deathSfx; // + set
                WindupShakeDur = wDur; WindupShakeAmp = wAmp;
                ImpactShakeDur = iDur; ImpactShakeAmp = iAmp;
                ProjectileTime = proj; BeamTime = beam;
                AttackFallback = atkFb; HitFallback = hitFb; DeathFallback = deathFb;
            }
        }


        /// <summary>
        /// Resolve a CuePack using type + optional skillId + optional tag; merges rule over defaults.
        /// </summary>
        public CuePack Resolve(AnimationType type, string skillId = null, string tag = null)
        {
            // Start with defaults
            string step = stepSfxId, whoosh = swingWhooshSfxId, impact = defaultImpactVfxId, hit = hitSfxId, deathBurst = deathBurstVfxId, deathSfx = deathSfxId;
            float wDur = windupShakeDuration, wAmp = windupShakeAmplitude, iDur = impactShakeDuration, iAmp = impactShakeAmplitude;
            float proj = projectileTravelTime, beam = beamTravelTime;

            // Find the first matching rule (prioritize more specific first, so order your list accordingly)
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r.key.type != type) continue;
                if (!string.IsNullOrEmpty(r.key.skillIdContains) && (skillId == null || !skillId.Contains(r.key.skillIdContains))) continue;
                if (!string.IsNullOrEmpty(r.key.tag) && r.key.tag != tag) continue;

                // Merge overrides if present
                if (!string.IsNullOrEmpty(r.whooshSfxId))     whoosh = r.whooshSfxId;
                if (!string.IsNullOrEmpty(r.impactVfxId))     impact = r.impactVfxId;
                if (!string.IsNullOrEmpty(r.hitSfxId))        hit = r.hitSfxId;
                if (!string.IsNullOrEmpty(r.deathBurstVfxId)) deathBurst = r.deathBurstVfxId;
                if (!string.IsNullOrEmpty(r.deathSfxId)) deathSfx = r.deathSfxId;

                if (r.windupShakeDuration  >= 0f) wDur = r.windupShakeDuration;
                if (r.windupShakeAmplitude >= 0f) wAmp = r.windupShakeAmplitude;
                if (r.impactShakeDuration  >= 0f) iDur = r.impactShakeDuration;
                if (r.impactShakeAmplitude >= 0f) iAmp = r.impactShakeAmplitude;

                if (r.projectileTravelTime >= 0f) proj = r.projectileTravelTime;
                if (r.beamTravelTime       >= 0f) beam = r.beamTravelTime;

                break; // first match wins
            }

            return new CuePack(
                step, whoosh, impact, hit, deathBurst, deathSfx,
                wDur, wAmp, iDur, iAmp, proj, beam,
                attackAnimFallbackDelay, hitAnimFallbackDelay, deathAnimFallbackDelay);
        }

        // ---- Databases passthrough (keeps Router oblivious to where assets live) ----
        public VisualEffectAsset FindEffect(string id)   => vfxDb ? vfxDb.FindEffect(id) : null;
        public GameObject FindProjectile(string id)      => vfxDb ? vfxDb.FindProjectile(id) : null;
        public AudioClip FindClip(string id)             => sfxDb ? sfxDb.FindClip(id) : null;

        /// <summary>Call on startup to build the internal dictionaries.</summary>
        public void InitializeDatabases()
        {
            if (vfxDb) vfxDb.InitializeLookup();
            if (sfxDb) sfxDb.InitializeLookup();
        }
    }
}
