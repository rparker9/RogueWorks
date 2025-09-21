using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Core API
using RogueWorks.Core.AI;
using RogueWorks.Core.Animation;
using RogueWorks.Core.Primitives;

// Unity API
using RogueWorks.Unity.Animation.Sinks;
using RogueWorks.Unity.Data;
using RogueWorks.Unity.Presentation;
using RogueWorks.Unity.UI;
using RogueWorks.Unity.Runtime.Input;

namespace RogueWorks.Unity.Runtime
{
    /// <summary>
    /// Scene host that builds Core, wires services, spawns actors/views, and advances the loop.
    /// Behavior preserved; heavy lifting delegated to three POCOs.
    /// </summary>
    public sealed class GameRuntime : MonoBehaviour
    {
        [Header("Input (Scene Instance)")]
        [SerializeField] private PlayerInputAdapter playerInputAdapter; // Required

        [Header("View Spawning")]
        [SerializeField] private ActorView actorPrefab;
        [SerializeField] private float actorYOffset = 0f;

        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 32;
        [SerializeField] private int gridHeight = 32;

        [Header("Mapping")]
        [SerializeField] private GridToWorldMapper gridMapper = new();

        [Header("Presentation (Databases & Profile)")]
        [SerializeField] private PresentationProfile presentationProfile;

        [Header("Roster (Scene Instance)")]
        [SerializeField] private RosterDefinition roster;

        [Header("UI (Scene Instances)")]
        [SerializeField] private SkillMenuController skillMenu;

        [Header("Services (Inspector Tunables)")]
        [SerializeField] private VfxService vfxSvc = new();
        [SerializeField] private SfxOneShotService sfxSvc = new();
        [SerializeField] private LogService logSvc = new();
        [SerializeField] private CameraShakeService shakeSvc = new();

        [Header("Service Runtime Roots")]
        [SerializeField] private Transform serviceRoot;
        [SerializeField] private Transform cameraRoot;

        [Header("Gizmos")]
        [SerializeField] private bool gizmosDrawGrid = true;
        [SerializeField] private bool gizmosDrawActors = true;
        [SerializeField] private float gizmoGridY = 0f;
        [SerializeField] private Color gizmoGridColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color gizmoActorModelColor = Color.red;
        [SerializeField] private Color gizmoActorViewColor = Color.green;
        [SerializeField] private int gizmoPreviewWidth = 32;
        [SerializeField] private int gizmoPreviewHeight = 32;

        // ---- Runtime singletons/registries (presentation) ----
        private readonly ActorViewRegistry _views = new();

        // ---- Extracted pieces (POCOs) ----
        private SceneBootstrap _boot;                // Core + Orchestrator + Sequencer
        private ActorSpawner _spawner;               // Roster -> Core Actors + Views
        private AnimationBatchRunner _batch;         // Plays batches of AnimationRequests

        // ---- Convenience state ----
        private int _playerActorId = -1;

        /// <summary>
        /// True while a blocking animation is in progress (gates Update and input).
        /// </summary>
        public bool IsBlockingView { get; private set; }

        private void Awake()
        {
            // Validate required scene refs
            if (!presentationProfile) { Debug.LogError("[GameRuntime] PresentationProfile is not assigned."); enabled = false; return; }
            if (!playerInputAdapter) { Debug.LogError("[GameRuntime] PlayerInputAdapter is not assigned."); enabled = false; return; }
            if (!actorPrefab) { Debug.LogError("[GameRuntime] ActorView prefab is not assigned."); enabled = false; return; }
            if (!roster) { Debug.LogError("[GameRuntime] RosterDefinition is not assigned."); enabled = false; return; }

            // Prepare lookups and service roots
            presentationProfile.InitializeDatabases();
            if (!serviceRoot)
            {
                var go = new GameObject("ServiceRoot");
                serviceRoot = go.transform;
                serviceRoot.SetParent(transform);
            }

            // Services init (your services already expose Initialize)
            vfxSvc.Initialize(serviceRoot, this);
            sfxSvc.Initialize(serviceRoot);
            shakeSvc.Initialize(cameraRoot);

            var opt = new SceneBootstrapOptions
            {
                Runner = this,
                GridWidth = gridWidth,
                GridHeight = gridHeight,
                ToWorld = (gp, y) => gridMapper.ToWorld(gp) + Vector3.up * y,

                Vfx = vfxSvc,
                Sfx = sfxSvc,
                Log = logSvc,
                Shake = shakeSvc,

                ViewLookup = _views,
                PresentationProfile = presentationProfile,

                OnBlockStart = () =>
                {
                    IsBlockingView = true;
                    playerInputAdapter.ClearContinuousBuffer();
                    playerInputAdapter.ClearAttackBuffersOnBlockStart();
                }
            };

            // Build Core + Orchestrator
            _boot = new SceneBootstrap(opt);

            // Spawner (roster -> actors + views)
            _spawner = new ActorSpawner(_boot.World, _views, gridMapper, actorPrefab, actorYOffset);
            _playerActorId = _spawner.SpawnAll(roster);

            // Bind input controller for the player
            if (_playerActorId >= 0)
            {
                _boot.Engine.RegisterController(_playerActorId, playerInputAdapter);

                var player = _boot.World.Actors.FirstOrDefault(a => a.Id == _playerActorId);
                if (player != null && skillMenu != null)
                    skillMenu.SetRuntimeSkills(player.Skills);
            }

            // Register SimpleAI for all non-player actors
            foreach (var a in _boot.World.Actors)
            {
                if (a.Id == _playerActorId) continue;
                _boot.Engine.RegisterController(a.Id, new SimpleAI(a.Id));
            }

            // Batch runner
            _batch = new AnimationBatchRunner(this, _boot.Orchestrator);
        }

        private void Update()
        {
            // Don’t advance Core while a blocking segment is in progress (windups, etc.)
            if (IsBlockingView) 
                return;

            // Advance Core -> get requests for the view
            List<AnimationRequest> rqs = _boot.Engine.Tick(_boot.World, _boot.Clock);
            if (rqs == null || rqs.Count == 0) return;

            // Gate while the batch plays; release when finished (done in LateUpdate check below)
            IsBlockingView = true;
            _batch.Play(rqs);
        }

        private void LateUpdate()
        {
            // When a batch completes, the runner won't be active; release gating.
            if (IsBlockingView && !_batch.IsRunning)
                IsBlockingView = false;
        }

        private void OnDrawGizmos()
        {
            int w = (_boot?.World != null ? _boot.World.Width : gizmoPreviewWidth);
            int h = (_boot?.World != null ? _boot.World.Height : gizmoPreviewHeight);

            var w00 = gridMapper.ToWorld(new GridPos(0, 0));
            var w10 = gridMapper.ToWorld(new GridPos(1, 0));
            var w01 = gridMapper.ToWorld(new GridPos(0, 1));
            var dx = w10 - w00;
            var dz = w01 - w00;
            float tileX = new Vector2(dx.x, dx.z).magnitude;
            float tileZ = new Vector2(dz.x, dz.z).magnitude;

            if (gizmosDrawGrid)
            {
                Gizmos.color = gizmoGridColor;
                for (int x = 0; x <= w; x++)
                {
                    var a = gridMapper.ToWorld(new GridPos(x, 0));
                    var b = gridMapper.ToWorld(new GridPos(x, h));
                    a.y = b.y = gizmoGridY;
                    Gizmos.DrawLine(a, b);
                }
                for (int y = 0; y <= h; y++)
                {
                    var a = gridMapper.ToWorld(new GridPos(0, y));
                    var b = gridMapper.ToWorld(new GridPos(w, y));
                    a.y = b.y = gizmoGridY;
                    Gizmos.DrawLine(a, b);
                }
            }

            if (gizmosDrawActors)
            {
                if (_boot?.World != null)
                {
                    Gizmos.color = gizmoActorModelColor;
                    var size = new Vector3(tileX, Mathf.Max(0.02f, tileX * 0.02f), tileZ);
                    foreach (var a in _boot.World.Actors)
                    {
                        var wp = gridMapper.ToWorld(a.Position);
                        wp.y = gizmoGridY + size.y * 0.5f;
                        Gizmos.DrawWireCube(wp, size);
                    }
                }

                if (_views != null)
                {
                    Gizmos.color = gizmoActorViewColor;
                    foreach (var view in _views.All())
                    {
                        if (view == null) continue;
                        var p = view.transform.position;
                        Gizmos.DrawSphere(p + Vector3.up * 0.01f, Mathf.Max(0.03f, 0.15f * Mathf.Min(tileX, tileZ)));
                    }
                }
            }
        }

        // ------------------ Public hooks used by PlayerInputAdapter ------------------

        /// <summary>
        /// Returns true if the player’s view exists and we are not in a blocking visual.
        /// </summary>
        public bool IsPlayerViewReadyForAction(float epsilon = 0.002f)
        {
            if (_playerActorId < 0) 
                return false;
            if (IsBlockingView) 
                return false;

            if (!_views.TryGet(_playerActorId, out var view) || view == null)
                return false;

            var actor = _boot.World.Actors.FirstOrDefault(a => a.Id == _playerActorId);
            if (actor == null) return false;

            var modelWorld = gridMapper.ToWorld(actor.Position) + Vector3.up * actorYOffset;
            bool noPath = !view.HasPath;
            bool aligned = (Vector3.SqrMagnitude(view.transform.position - modelWorld) <= epsilon * epsilon);
            return noPath && aligned;
        }
    }
}
