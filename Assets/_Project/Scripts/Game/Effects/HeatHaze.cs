using ChessBoss.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ChessBoss.Game
{
    /// <summary>
    /// The air shimmering as a breath's beam comes near the pawn, sheltered or not. The camera can't see a beam
    /// coming from behind, so this is what warns of it. Runs on real time and holds still while the game is paused.
    /// </summary>
    public sealed class HeatHaze : MonoBehaviour
    {
        static readonly int Offset = Shader.PropertyToID("_HazeOffset");
        static readonly int Scale = Shader.PropertyToID("_HazeScale");
        static readonly int Speed = Shader.PropertyToID("_HazeSpeed");
        static readonly int Inner = Shader.PropertyToID("_HazeInner");
        static readonly int Clear = Shader.PropertyToID("_HazeClear");
        static readonly int Clock = Shader.PropertyToID("_HazeTime");

        BoardView _board;
        PawnController _pawn;
        BossAttackView _attacks;
        Material _material;
        HazePass _pass;
        float _clock;

        /// <summary>From 0 with no beam near to 1 with the beam on the pawn's sector.</summary>
        public float Strength { get; private set; }

        public void Init(BoardView board, PawnController pawn, BossAttackView attacks)
        {
            _board = board;
            _pawn = pawn;
            _attacks = attacks;
            var tuning = board.Tuning;
            // A copy, so the values set every frame don't end up written into the asset.
            _material = new Material(tuning.hazeMaterial);
            _material.SetFloat(Scale, tuning.hazeScale);
            _material.SetFloat(Speed, tuning.hazeSpeed);
            _material.SetFloat(Inner, tuning.hazeInner);
            _material.SetFloat(Clear, tuning.screenEdgeClearRadius);
            _pass = new HazePass(_material);
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += AddPass;

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= AddPass;

        void OnDestroy() => Destroy(_material);

        void Update()
        {
            if (BattleTime.IsPaused) return;

            var tuning = _board.Tuning;
            _clock += Time.unscaledDeltaTime;
            Strength = Mathf.MoveTowards(Strength, BeamNearness(), Time.unscaledDeltaTime / tuning.hazeFadeTime);
            _material.SetFloat(Offset, Strength * tuning.hazeMaxOffset);
            _material.SetFloat(Clock, _clock);
        }

        float BeamNearness()
        {
            var attacks = _attacks.Attacks;
            if (attacks == null || !attacks.Timeline.IsRunning) return 0;
            if (attacks.CurrentAttack != BossAttackKind.Breath && attacks.CurrentAttack != BossAttackKind.SlowBreath) return 0;

            var apart = _board.Board.SectorDistance(_pawn.Movement.Position.X, attacks.AimSector);
            return Mathf.Clamp01(1 - apart / _board.Tuning.hazeReachSectors);
        }

        // Added per frame from code instead of as a renderer feature, which would mean editing the renderer asset.
        void AddPass(ScriptableRenderContext context, Camera view)
        {
            if (Strength <= 0 || view.cameraType != CameraType.Game) return;

            view.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);
        }

        sealed class HazePass : ScriptableRenderPass
        {
            readonly Material _material;

            public HazePass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                // The frame has to be read back, which the screen's own buffer doesn't allow.
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var source = resources.activeColorTexture;
                var description = renderGraph.GetTextureDesc(source);
                description.name = "HeatHaze";
                description.clearBuffer = false;
                var bent = renderGraph.CreateTexture(description);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, bent, _material, 0), "HeatHaze");
                resources.cameraColor = bent;
            }
        }
    }
}
