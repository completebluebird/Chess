using Unity.Cinemachine;
using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>
    /// The camera's framing, its shakes, and fading whatever stands between it and the pawn.
    /// It runs on real time and holds still while the game is paused.
    /// </summary>
    public sealed class CameraEffects : MonoBehaviour
    {
        BoardView _board;
        PawnController _pawn;
        BossView _boss;
        Transform _view;
        CinemachineCamera _camera;
        CinemachineFollow _follow;
        CinemachineImpulseSource _impulse;
        CinemachineImpulseListener _listener;

        public void Init(BoardView board, PawnController pawn, BossView boss)
        {
            _board = board;
            _pawn = pawn;
            _boss = boss;
            var tuning = board.Tuning;

            _camera = GetComponent<CinemachineCamera>();
            _follow = GetComponent<CinemachineFollow>();
            _follow.FollowOffset = tuning.cameraOffset;
            _camera.Lens.FieldOfView = tuning.cameraFieldOfView;

            // Slow motion and hitstop must not slow the camera or hold a shake.
            var brain = FindAnyObjectByType<CinemachineBrain>();
            brain.IgnoreTimeScale = true;
            _view = brain.transform;
            CinemachineImpulseManager.Instance.IgnoreTimeScale = true;

            // Moving the camera's transform directly would be overwritten by Cinemachine, so shakes go through its impulses.
            _listener = gameObject.AddComponent<CinemachineImpulseListener>();
            _listener.ChannelMask = 1;
            _listener.Gain = 1;
            _listener.UseCameraSpace = true;
            _impulse = gameObject.AddComponent<CinemachineImpulseSource>();
            _impulse.ImpulseDefinition.ImpulseChannel = 1;
            _impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion;
            _impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            _impulse.ImpulseDefinition.ImpulseDuration = tuning.shakeDuration;
        }

        public void ShakeWeak() => _impulse.GenerateImpulseWithForce(_board.Tuning.shakeWeak);

        public void ShakeStrong() => _impulse.GenerateImpulseWithForce(_board.Tuning.shakeStrong);

        void LateUpdate()
        {
            // shortcut: a shake caught by a pause is muted and has run out by the resume, instead of freezing mid-shake; revisit if pausing mid-hit looks wrong.
            _listener.Gain = BattleTime.IsPaused ? 0 : 1;
            if (BattleTime.IsPaused) return;

            FadeOccluders();
        }

        // Walls and the boss have no colliders, so the line from the camera to the pawn is tested against their bounds.
        void FadeOccluders()
        {
            var tuning = _board.Tuning;
            var toPawn = _pawn.Body.bounds.center - _view.position;
            var step = (1 - tuning.occluderAlpha) * Time.unscaledDeltaTime / tuning.occluderFadeTime;

            foreach (var wall in _board.Walls) Fade(wall);
            Fade(_boss.Body);
            if (_boss.Head != null) Fade(_boss.Head);

            void Fade(Renderer occluder)
            {
                // In the occluder's own space, where its box is exact. Its world bounds swell as it turns and would
                // swallow a pawn standing next to the boss.
                var space = occluder.transform;
                var reach = space.InverseTransformVector(toPawn);
                var line = new Ray(space.InverseTransformPoint(_view.position), reach);
                var hides = occluder.localBounds.IntersectRay(line, out var distance) && distance < reach.magnitude;
                var alpha = _board.AlphaOf(occluder);
                var target = hides ? tuning.occluderAlpha : 1;
                if (alpha != target) _board.SetAlpha(occluder, Mathf.MoveTowards(alpha, target, step));
            }
        }
    }
}
