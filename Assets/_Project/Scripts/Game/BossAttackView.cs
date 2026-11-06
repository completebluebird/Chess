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
                BiteWeight = tuning.biteWeight,
                MeteorWarning = tuning.meteorWarning,
                MeteorRadius = tuning.meteorRadius,
                BiteWarning = tuning.biteWarning,
                BiteDepth = tuning.biteDepth,
                BiteHalfWidth = tuning.biteHalfWidth,
                BreathEvery = tuning.breathEvery,
                BreathPrep = tuning.breathPrep,
                BreathStepTime = tuning.breathStepTime,
                BreathWarning = tuning.breathWarning,
                BreathBeamWidth = tuning.breathBeamWidth,
                BreathWalls = tuning.breathWalls,
                BreathCoverHops = tuning.breathCoverHops,
                LaserDamage = tuning.laserDamage,
                MeteorDamage = tuning.meteorDamage,
                BiteDamage = tuning.biteDamage,
                BreathDamage = tuning.breathDamage,
            });
        }

        void Update()
        {
            Attacks.Tick(Time.deltaTime);
            _board.ShowWarnings(Attacks.Timeline);
            _board.ShowWalls();
            _boss.Face(_pawn.Movement.Position.X, Attacks.AimSector);
        }
    }
}
