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

        /// <summary>Square areas around the given cells, reaching the radius in cells along both sectors and rings.</summary>
        public static AttackStep Meteor(Board board, IReadOnlyList<Cell> centers, int radius, float warning) =>
            Area(board, cell => centers.Any(center =>
                board.SectorDistance(cell.X, center.X) <= radius && Math.Abs(cell.Y - center.Y) <= radius), warning);

        /// <summary>The rings closest to the boss, on the sectors in front of where it faces.</summary>
        public static AttackStep Bite(Board board, int facingSector, int depth, int halfWidth, float warning) =>
            Area(board, cell => cell.Y <= depth && board.SectorDistance(cell.X, facingSector) <= halfWidth, warning);

        /// <summary>
        /// A beam turning once around the boss. It runs outward from the boss, so a wall shelters itself and the
        /// cells behind it, and one wall always leaves cover within the pawn's reach before the first strike.
        /// </summary>
        public static BreathPlan Breath(Board board, Cell pawn, Random random, BossAttackSettings settings)
        {
            var walls = BreathWalls(board, pawn, random, settings);
            var start = new Cell(random.Next(board.Sectors), 0);
            var direction = random.Next(2) == 0 ? 1 : -1;

            var steps = new AttackStep[board.Sectors];
            for (var i = 0; i < steps.Length; i++)
            {
                var head = i;
                var beam = Enumerable.Range(0, settings.BreathBeamWidth)
                    .Select(behind => board.Step(start, direction * (head - behind), 0).X)
                    .ToArray();
                var cells = board.Cells
                    .Where(cell => board.IsWalkable(cell) && beam.Contains(cell.X)
                        && !walls.Any(wall => wall.X == cell.X && wall.Y <= cell.Y))
                    .ToArray();
                var strike = settings.BreathPrep + i * settings.BreathStepTime;
                steps[i] = new AttackStep(cells, strike - settings.BreathWarning, strike);
            }

            return new BreathPlan(walls, steps);
        }

        static List<Cell> BreathWalls(Board board, Cell pawn, Random random, BossAttackSettings settings)
        {
            // The first ring stays open so every beam step has cells; the last ring has nothing behind it.
            var spots = board.Cells.Where(cell => cell != pawn && cell.Y >= 2 && cell.Y <= board.Rings - 2).ToArray();
            bool LeavesCover(List<Cell> walls)
            {
                var hops = board.HopsFrom(pawn, walls);
                return walls.Any(wall =>
                    hops.TryGetValue(new Cell(wall.X, wall.Y + 1), out var count) && count <= settings.BreathCoverHops);
            }

            // A wall can only lengthen paths, so the open-board distance rules out far spots without a search for each.
            var open = board.HopsFrom(pawn, new Cell[0]);
            var near = spots
                .Where(spot => open.TryGetValue(new Cell(spot.X, spot.Y + 1), out var count) && count <= settings.BreathCoverHops)
                .Where(spot => LeavesCover(new List<Cell> { spot }))
                .ToArray();
            if (near.Length == 0)
                throw new InvalidOperationException($"Board has no wall spot leaving cover in reach of {pawn}.");

            var chosen = new List<Cell> { near[random.Next(near.Length)] };
            for (var i = 1; i < settings.BreathWalls; i++)
            {
                var extra = spots[random.Next(spots.Length)];
                if (chosen.Contains(extra)) continue;

                chosen.Add(extra);
                if (!LeavesCover(chosen)) chosen.Remove(extra);
            }

            return chosen;
        }

        static AttackStep Area(Board board, Func<Cell, bool> covers, float warning) =>
            new AttackStep(board.Cells.Where(cell => board.IsWalkable(cell) && covers(cell)).ToArray(), 0, warning);
    }

    public sealed class BreathPlan
    {
        public IReadOnlyList<Cell> Walls { get; }
        public IReadOnlyList<AttackStep> Steps { get; }

        public BreathPlan(IReadOnlyList<Cell> walls, IReadOnlyList<AttackStep> steps)
        {
            Walls = walls;
            Steps = steps;
        }
    }

    public enum BossAttackKind
    {
        Laser,
        Meteor,
        Bite,
        Breath,
    }

    public sealed class BossAttackSettings
    {
        public float Interval;
        public float LaserWarning;
        public int LaserShotsMin;
        public int LaserShotsMax;
        public float LaserShotGap;
        public int BiteWeight;
        public float MeteorWarning;
        public int MeteorRadius;
        public float BiteWarning;
        public int BiteDepth;
        public int BiteHalfWidth;
        public int BreathEvery;
        public float BreathPrep;
        public float BreathStepTime;
        public float BreathWarning;
        public int BreathBeamWidth;
        public int BreathWalls;
        public int BreathCoverHops;
    }

    /// <summary>Starts boss attacks at intervals and counts the strikes that land on the player.</summary>
    public sealed class BossAttacks
    {
        readonly PawnMovement _pawn;
        readonly Health _boss;
        readonly Random _random;
        readonly BossAttackSettings _settings;
        float _idle;
        int _attacksStarted;
        int _laserShotsLeft;
        float _nextLaserShot;
        BossAttackKind _lastBasicAttack;
        int _sameBasicAttackInARow;

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
                _laserShotsLeft = 0;
                _pawn.Board.SetWalls(new Cell[0]);
                return;
            }

            Timeline.Tick(deltaTime);
            if (_laserShotsLeft > 0)
            {
                _nextLaserShot -= deltaTime;
                if (_nextLaserShot <= 0) FireLaserShot();
                return;
            }

            if (Timeline.IsRunning) return;

            _pawn.Board.SetWalls(new Cell[0]);
            _idle += deltaTime;
            if (_idle < _settings.Interval) return;

            _idle = 0;
            StartNextAttack();
        }

        void StartNextAttack()
        {
            _attacksStarted++;
            // shortcut: breath comes on a fixed count, M6 replaces this with the boss health thresholds.
            if (_attacksStarted % _settings.BreathEvery == 0)
            {
                CurrentAttack = BossAttackKind.Breath;
                var breath = BossPatterns.Breath(_pawn.Board, _pawn.Position, _random, _settings);
                _pawn.Board.SetWalls(breath.Walls);
                Timeline.Start(breath.Steps);
                return;
            }

            CurrentAttack = PickBasicAttack();
            var board = _pawn.Board;
            var position = _pawn.Position;
            switch (CurrentAttack)
            {
                case BossAttackKind.Laser:
                    Timeline.Start(new AttackStep[0]);
                    _laserShotsLeft = _random.Next(_settings.LaserShotsMin, _settings.LaserShotsMax + 1);
                    _nextLaserShot = 0;
                    FireLaserShot();
                    break;
                case BossAttackKind.Meteor:
                    Timeline.Start(new[]
                    {
                        BossPatterns.Meteor(board, MeteorCenters(), _settings.MeteorRadius, _settings.MeteorWarning),
                    });
                    break;
                default:
                    Timeline.Start(new[]
                    {
                        BossPatterns.Bite(board, position.X, _settings.BiteDepth, _settings.BiteHalfWidth, _settings.BiteWarning),
                    });
                    break;
            }
        }

        // Near the boss, bite is favored; no attack runs three times in a row, and breath is decided separately.
        BossAttackKind PickBasicAttack()
        {
            var options = new List<BossAttackKind> { BossAttackKind.Laser, BossAttackKind.Meteor };
            if (_pawn.Position.Y <= _settings.BiteDepth)
                options.AddRange(Enumerable.Repeat(BossAttackKind.Bite, _settings.BiteWeight));
            if (_sameBasicAttackInARow >= 2)
                options.RemoveAll(kind => kind == _lastBasicAttack);

            var picked = options[_random.Next(options.Count)];
            _sameBasicAttackInARow = picked == _lastBasicAttack ? _sameBasicAttackInARow + 1 : 1;
            _lastBasicAttack = picked;
            return picked;
        }

        // Each shot aims at where the pawn is now, so the line follows a moving pawn.
        void FireLaserShot()
        {
            Timeline.Add(BossPatterns.Laser(_pawn.Board, new[] { _pawn.Position.X }, _settings.LaserWarning));
            _laserShotsLeft--;
            _nextLaserShot += _settings.LaserShotGap;
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
