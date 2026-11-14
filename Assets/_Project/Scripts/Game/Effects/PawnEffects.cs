using ChessBoss.Core;
using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>
    /// What clings to the pawn while a state lasts. It reads the Core state every frame instead of waiting for an
    /// event to switch things off, so nothing is left burning by a missed one.
    /// </summary>
    public sealed class PawnEffects : MonoBehaviour
    {
        BoardView _board;
        PawnController _pawn;
        BattleEffects _effects;
        ParticleSystem _flame;
        ParticleSystem _rim;
        ParticleSystem _embers;
        Renderer _pillar;
        float _pillarLeft;
        AwakenPiece? _shown;
        AwakenedStrike _lastStrike;
        bool _paused;
        Vector3 _staggerAxis;
        float _staggerLeft;

        public bool FlameShown => _flame.emission.enabled;
        public int FlameParticles => _flame.particleCount + _rim.particleCount;
        public bool PillarShown => _pillar.gameObject.activeSelf;
        public bool EmbersShown => _embers.emission.enabled;
        public Color PillarColor { get; private set; }
        public bool Staggering => _staggerLeft > 0;

        public void Init(BoardView board, PawnController pawn, BattleEffects effects)
        {
            _board = board;
            _pawn = pawn;
            _effects = effects;
            var tuning = board.Tuning;

            // The black flame and its colored rim rise around the piece on real time, so the awakening's slow motion doesn't slow them.
            _flame = BuildRising("Awakening flame", tuning.flameColor, tuning.flameRadius, tuning.flameSize, tuning.flameHeight, tuning.flameLifetime, tuning.flameRate, true);
            _rim = BuildRising("Awakening flame rim", Color.white, tuning.flameRadius + tuning.flameSize / 2, tuning.flameRimSize, tuning.flameHeight, tuning.flameLifetime, tuning.flameRimRate, true);
            // Small, orange and low at the feet, on game time like the burn itself: nothing like the awakening's tall black flame.
            _embers = BuildRising("Burn embers", tuning.emberColor, tuning.emberRadius, tuning.emberSize, tuning.emberHeight, tuning.emberLifetime, tuning.emberRate, false);

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Awakening pillar";
            Destroy(pillar.GetComponent<Collider>());
            // A cylinder is 2 units tall at scale 1.
            pillar.transform.localScale = new Vector3(tuning.pillarWidth, tuning.pillarHeight / 2, tuning.pillarWidth);
            _pillar = pillar.GetComponent<Renderer>();
            _pillar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pillar.SetActive(false);
        }

        /// <summary>Rocks the piece on its base and lets it settle, when a hit lands on it.</summary>
        public void Stagger()
        {
            var away = Random.insideUnitCircle.normalized;
            _staggerAxis = new Vector3(away.x, 0, away.y);
            _staggerLeft = _board.Tuning.staggerTime;
        }

        void Update()
        {
            // The flame and the pillar run on real time, which a pause doesn't stop by itself.
            if (_paused != BattleTime.IsPaused)
            {
                _paused = BattleTime.IsPaused;
                foreach (var system in new[] { _flame, _rim })
                    if (_paused) system.Pause();
                    else system.Play();
            }

            if (_paused) return;

            ShowAwakening();
            ShowPillar();
            ShowStagger();

            var vitals = _pawn.Vitals;
            if (EmbersShown != vitals.IsBurning) SetEmitting(_embers, vitals.IsBurning);
            // Rounded as Core rounds its health thresholds: 0.3f * 50 need not come out at exactly 15.
            _effects.Screen.LowHealth = vitals.Health.Current <= Mathf.Round(_board.Tuning.lowHealthFraction * vitals.Health.Max);
        }

        void ShowStagger()
        {
            if (!Staggering) return;

            var tuning = _board.Tuning;
            _staggerLeft = Mathf.Max(0, _staggerLeft - Time.deltaTime);
            var left = _staggerLeft / tuning.staggerTime;
            var lean = tuning.staggerDegrees * left * Mathf.Sin(left * tuning.staggerSwings * 2 * Mathf.PI);
            _pawn.Body.transform.localRotation = Quaternion.AngleAxis(lean, _staggerAxis);
        }

        void ShowAwakening()
        {
            var piece = _pawn.Awakening.Active;
            if (piece == _shown) return;

            _shown = piece;
            var struck = _pawn.LastStrike != _lastStrike && _pawn.LastStrike.Succeeded;
            _lastStrike = _pawn.LastStrike;
            SetEmitting(_flame, piece != null);
            SetEmitting(_rim, piece != null);
            if (piece == null)
            {
                // Used, the flame scatters upward as its last particles rise. Cancelled, it goes out at once.
                if (!struck)
                {
                    _flame.Clear();
                    _rim.Clear();
                }

                return;
            }

            // Until the piece models arrive in M9, the rim and the pillar's color tell the three pieces apart.
            var tuning = _board.Tuning;
            PillarColor = piece == AwakenPiece.Rook ? tuning.rookFlameColor
                : piece == AwakenPiece.Bishop ? tuning.bishopFlameColor
                : tuning.knightFlameColor;
            var rim = _rim.main;
            rim.startColor = PillarColor;

            _pillar.transform.position = _pawn.transform.position + Vector3.up * (tuning.pillarHeight / 2);
            _pillar.gameObject.SetActive(true);
            _board.Paint(_pillar, PillarColor);
            _pillarLeft = tuning.pillarTime;
            _effects.Sound(SoundCues.Awaken);
        }

        void ShowPillar()
        {
            if (!PillarShown) return;

            var tuning = _board.Tuning;
            _pillarLeft -= Time.unscaledDeltaTime;
            _board.SetAlpha(_pillar, tuning.pillarAlpha * Mathf.Clamp01(_pillarLeft / tuning.pillarTime));
            if (_pillarLeft <= 0) _pillar.gameObject.SetActive(false);
        }

        static void SetEmitting(ParticleSystem system, bool emitting)
        {
            var emission = system.emission;
            emission.enabled = emitting;
        }

        // Particles born on a circle around the pawn's feet that rise to the height over their lifetime, shrinking and fading.
        ParticleSystem BuildRising(string systemName, Color color, float radius, float size, float height, float lifetime, float rate, bool realTime)
        {
            var system = new GameObject(systemName).AddComponent<ParticleSystem>();
            system.transform.SetParent(transform, false);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.useUnscaledTime = realTime;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSpeed = height / lifetime;
            main.startSize = size;
            main.startColor = color;
            var emission = system.emission;
            emission.rateOverTime = rate;
            emission.enabled = false;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0;
            shape.radius = radius;
            // From the circle itself and not inside it, where the piece would hide the flame until it rose past its head.
            shape.radiusThickness = 0;
            // The cone opens along its local Z; this turns it to point up.
            shape.rotation = new Vector3(-90, 0, 0);
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(Color.white, new Color(1, 1, 1, 0));
            var shrink = system.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = _board.Tuning.particleMaterial;
            system.Play();
            return system;
        }
    }
}
