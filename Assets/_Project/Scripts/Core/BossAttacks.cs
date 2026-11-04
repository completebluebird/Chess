using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessBoss.Core
{
    public static class BossPatterns
    {
        /// <summary>Straight lines running outward from the boss along the given sectors.</summary>
        public static AttackStep Laser(Board board, IEnumerable<int> sectors, float warning) =>
            Area(board, cell => sectors.Contains(cell.X), warning);

        /// <summary>Round areas around the given cells, measured in cells.</summary>
        public static AttackStep Meteor(Board board, IReadOnlyList<Cell> centers, int radius, float warning) =>
            Area(board, cell => centers.Any(center =>
            {
                var sectors = board.SectorDistance(cell.X, center.X);
                var rings = cell.Y - center.Y;
                return sectors * sectors + rings * rings <= radius * radius;
            }), warning);

        /// <summary>The rings closest to the boss, on the sectors in front of where it faces.</summary>
        public static AttackStep Bite(Board board, int facingSector, int depth, int halfWidth, float warning) =>
            Area(board, cell => cell.Y <= depth && board.SectorDistance(cell.X, facingSector) <= halfWidth, warning);

        static AttackStep Area(Board board, Func<Cell, bool> covers, float warning) =>
            new AttackStep(board.Cells.Where(cell => board.IsWalkable(cell) && covers(cell)).ToArray(), 0, warning);
    }

    public enum BossAttackKind
    {
        Laser,
        Meteor,
        Bite,
    }

    public sealed class BossAttackSettings
    {
        public float Interval;
        public float LaserWarning;
        public float MeteorWarning;
        public int MeteorRadius;
        public float BiteWarning;
        public int BiteDepth;
        public int BiteHalfWidth;
    }

    /// <summary>Starts boss attacks at intervals and counts the strikes that land on the player.</summary>
    public sealed class BossAttacks
    {
        readonly PawnMovement _pawn;
        readonly Health _boss;
        readonly Random _random;
        readonly BossAttackSettings _settings;
        float _idle;

        public AttackTimeline Timeline { get; } = new AttackTimeline();
        public BossAttackKind CurrentAttack { get; private set; }
        public int PlayerHits { get; private set; }

        public BossAttacks(PawnMovement pawn, Health boss, Random random, BossAttackSettings settings)
        {
            _pawn = pawn;
            _boss = boss;
            _random = random;
            _settings = settings;
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
            if (_idle < _settings.Interval) return;

            _idle = 0;
            Timeline.Start(new[] { NextAttack() });
        }

        AttackStep NextAttack()
        {
            var board = _pawn.Board;
            var position = _pawn.Position;
            // Bite is last in the enum, so a pawn out of its reach never rolls it.
            var inBiteReach = position.Y <= _settings.BiteDepth;
            CurrentAttack = (BossAttackKind)_random.Next(inBiteReach ? 3 : 2);

            switch (CurrentAttack)
            {
                case BossAttackKind.Laser:
                    return BossPatterns.Laser(board, LaserSectors(), _settings.LaserWarning);
                case BossAttackKind.Meteor:
                    return BossPatterns.Meteor(board, MeteorCenters(), _settings.MeteorRadius, _settings.MeteorWarning);
                default:
                    return BossPatterns.Bite(
                        board, position.X, _settings.BiteDepth, _settings.BiteHalfWidth, _settings.BiteWarning);
            }
        }

        // One line at the player, sometimes a second one elsewhere.
        int[] LaserSectors()
        {
            var sectors = _pawn.Board.Sectors;
            var target = _pawn.Position.X;
            return _random.Next(2) == 0
                ? new[] { target, (target + 1 + _random.Next(sectors - 1)) % sectors }
                : new[] { target };
        }

        // One meteor at the player, sometimes a second one anywhere.
        Cell[] MeteorCenters()
        {
            var walkable = _pawn.Board.Cells.Where(_pawn.Board.IsWalkable).ToArray();
            return _random.Next(2) == 0
                ? new[] { _pawn.Position, walkable[_random.Next(walkable.Length)] }
                : new[] { _pawn.Position };
        }
    }
}
