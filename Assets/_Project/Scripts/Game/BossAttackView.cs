using ChessBoss.Core;
using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>Runs the Core boss attacks and shows their warnings on the board.</summary>
    public sealed class BossAttackView : MonoBehaviour
    {
        [SerializeField] BoardView _board;
        [SerializeField] PawnController _pawn;
        [SerializeField] BossView _boss;

        BossPhase _shownPhase;
        float _stunSlowLeft;

        public BossAttacks Attacks { get; private set; }

        void Start()
        {
            var tuning = _board.Tuning;
            Attacks = new BossAttacks(_pawn.Movement, _boss.Health, _pawn.Vitals, new System.Random(), new BossAttackSettings
            {
                Interval = tuning.attackInterval,
                LaserWarning = tuning.laserWarning,
                LaserShotsMin = tuning.laserShotsMin,
                LaserShotsMax = tuning.laserShotsMax,
                LaserShotGap = tuning.laserShotGap,
                LaserWeight = tuning.laserWeight,
                MeteorWeight = tuning.meteorWeight,
                SlowBreathWeight = tuning.slowBreathWeight,
                BiteWeight = tuning.biteWeight,
                MeteorWarning = tuning.meteorWarning,
                MeteorRadius = tuning.meteorRadius,
                BiteWarning = tuning.biteWarning,
                BiteDepth = tuning.biteDepth,
                BiteHalfWidth = tuning.biteHalfWidth,
                BreathAtHealth = tuning.stunHealthFractions,
                StunDuration = tuning.stunDuration,
                BreathPrep = tuning.breathPrep,
                BreathStepTime = tuning.breathStepTime,
                BreathWarning = tuning.breathWarning,
                BreathBeamWidth = tuning.breathBeamWidth,
                BreathWalls = tuning.breathWalls,
                BreathCoverHops = tuning.breathCoverHops,
                SlowBreathPrep = tuning.slowBreathPrep,
                SlowBreathStepTime = tuning.slowBreathLapTime / tuning.boardSectors,
                SlamAirTime = tuning.slamAirTime,
                SlamRingGap = tuning.slamRingGap,
                SlamWarning = tuning.slamWarning,
                LaserDamage = tuning.laserDamage,
                MeteorDamage = tuning.meteorDamage,
                BiteDamage = tuning.biteDamage,
                BreathDamage = tuning.breathDamage,
                SlamDamage = tuning.slamDamage,
            });
            Attacks.Timeline.Struck += cells => _pawn.Effects.Impact(Attacks.CurrentAttack, cells);
            _pawn.Vitals.Hurt += () => _pawn.Effects.PlayerHurt(Attacks.CurrentAttack);
        }

        void Update()
        {
            Attacks.Tick(Time.deltaTime);
            _board.ShowWarnings(Attacks.Timeline);
            _board.ShowWalls();
            ShowPhase();
            _boss.SetAirborne(Attacks.AirProgress);
            if (Attacks.Phase != BossPhase.Stunned) _boss.Face(_pawn.Movement.Position.X, Attacks.AimSector);
        }

        void ShowPhase()
        {
            var stunned = Attacks.Phase == BossPhase.Stunned;
            _pawn.Awakening.ChargePaused = stunned;
            if (_shownPhase != Attacks.Phase)
            {
                _shownPhase = Attacks.Phase;
                _boss.SetStunned(stunned);
                _stunSlowLeft = stunned ? _board.Tuning.stunSlowTime : 0;
                BattleTime.StunSlowMotion = stunned ? _board.Tuning.stunSlowTimeScale : 1;
            }
            else if (_stunSlowLeft > 0)
            {
                // Real time, so the signal lasts as long as it says however slow the game runs.
                _stunSlowLeft -= Time.unscaledDeltaTime;
                if (_stunSlowLeft <= 0) BattleTime.StunSlowMotion = 1;
            }
        }

        void OnDisable() => BattleTime.StunSlowMotion = 1;
    }
}
