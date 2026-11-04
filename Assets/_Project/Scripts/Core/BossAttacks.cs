using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessBoss.Core
{
    public static class BossPatterns
    {
        /// <summary>Straight lines running outward from the boss along the given sectors.</summary>
        public static AttackStep Laser(Board board, IEnumerable<int> sectors, float warning)
        {
            var cells = sectors
                .SelectMany(sector => board.Cells.Where(cell => cell.X == sector && board.IsWalkable(cell)))
                .ToArray();
            return new AttackStep(cells, 0, warning);
        }
    }

    /// <summary>Starts boss attacks at intervals and counts the strikes that land on the player.</summary>
    public sealed class BossAttacks
    {
        readonly PawnMovement _pawn;
        readonly Health _boss;
        readonly Random _random;
        readonly float _laserWarning;
        readonly float _interval;
        float _idle;

        public AttackTimeline Timeline { get; } = new AttackTimeline();
        public int PlayerHits { get; private set; }

        public BossAttacks(PawnMovement pawn, Health boss, Random random, float laserWarning, float interval)
        {
            _pawn = pawn;
            _boss = boss;
            _random = random;
            _laserWarning = laserWarning;
            _interval = interval;
            Timeline.Struck += cells =>
            {
                if (cells.Contains(_pawn.Position)) PlayerHits++;
            };
        }

        public void Tick(float deltaTime)
        {
            if (_boss.IsDead)
            {
                Timeline.Stop();
                return;
            }

            Timeline.Tick(deltaTime);
            if (Timeline.IsRunning) return;

            _idle += deltaTime;
            if (_idle < _interval) return;

            _idle = 0;
            Timeline.Start(new[] { BossPatterns.Laser(_pawn.Board, LaserSectors(), _laserWarning) });
        }

        // One line at the player, sometimes a second one elsewhere.
        IEnumerable<int> LaserSectors()
        {
            var sectors = _pawn.Board.Sectors;
            var target = _pawn.Position.X;
            yield return target;
            if (_random.Next(2) == 0)
                yield return (target + 1 + _random.Next(sectors - 1)) % sectors;
        }
    }
}
