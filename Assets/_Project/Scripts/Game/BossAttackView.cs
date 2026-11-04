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
            Attacks = new BossAttacks(
                _pawn.Movement, _boss.Health, new System.Random(), tuning.laserWarning, tuning.attackInterval);
        }

        void Update()
        {
            Attacks.Tick(Time.deltaTime);
            _board.ShowWarnings(Attacks.Timeline);
        }
    }
}
