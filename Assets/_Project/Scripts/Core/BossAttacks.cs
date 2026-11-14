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
                board.SectorDistance(cell.X, center.X) <= radius && Math.Abs(cell.Y - center.Y) <= radius), warning, centers[0].X);

        /// <summary>The rings closest to the boss, on the sectors in front of where it faces.</summary>
        public static AttackStep Bite(Board board, int facingSector, int depth, int halfWidth, float warning) =>
            Area(board, cell => cell.Y <= depth && board.SectorDistance(cell.X, facingSector) <= halfWidth, warning, facingSector);

        /// <summary>
        /// A beam turning once around the boss. It runs outward from the boss, so a wall shelters itself and the
        /// cells behind it, and one wall always leaves cover within the pawn's reach before the first strike.
        /// </summary>
        public static BreathPlan Breath(Board board, Cell pawn, Random random, BossAttackSettings settings)
        {
            var walls = BreathWalls(board, pawn, random, settings);
            return new BreathPlan(walls, Sweep(board, walls, random, settings.BreathPrep, settings.BreathStepTime, settings));
        }

        /// <summary>
        /// The same beam with no walls, turning slower than the pawn hops: the player stays ahead of it or
        /// crosses it between two strikes.
        /// </summary>
        public static AttackStep[] SlowBreath(Board board, Random random, BossAttackSettings settings) =>
            Sweep(board, new Cell[0], random, settings.SlowBreathPrep, settings.SlowBreathStepTime, settings);

        static AttackStep[] Sweep(Board board, IReadOnlyList<Cell> walls, Random random, float prep, float stepTime, BossAttackSettings settings)
        {
            var start = new Cell(random.Next(board.Sectors), 0);
            var direction = random.Next(2) == 0 ? 1 : -1;

            var steps = new AttackStep[board.Sectors];
            for (var i = 0; i < steps.Length; i++)
            {
                var head = i;
                // The beam grows from its start, or it would strike the sector behind the start now and again at the end of the turn.
                var beam = Enumerable.Range(0, Math.Min(settings.BreathBeamWidth, head + 1))
                    .Select(behind => board.Step(start, direction * (head - behind), 0).X)
                    .ToArray();
                var cells = board.Cells
                    .Where(cell => board.IsWalkable(cell) && beam.Contains(cell.X)
                        && !walls.Any(wall => wall.X == cell.X && wall.Y <= cell.Y))
                    .ToArray();
                var strike = prep + i * stepTime;
                steps[i] = new AttackStep(cells, strike - settings.BreathWarning, strike, beam[0]);
            }

            return steps;
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
                throw new InvalidOperationException($"Board has no wall spot leaving cover in reach of {pawn}. The breath needs at least 4 rings.");

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

        static AttackStep Area(Board board, Func<Cell, bool> covers, float warning, int? aimSector = null) =>
            new AttackStep(board.Cells.Where(cell => board.IsWalkable(cell) && covers(cell)).ToArray(), 0, warning, aimSector);
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
        SlowBreath,
    }

    public enum BossPhase
    {
        Pressing,
        Breathing,
        Stunned,
    }

    public sealed class BossAttackSettings
    {
        public float Interval;
        public float LaserWarning;
        public int LaserShotsMin;
        public int LaserShotsMax;
        public float LaserShotGap;
        public int LaserWeight;
        public int MeteorWeight;
        public int SlowBreathWeight;
        public int BiteWeight;
        public float MeteorWarning;
        public int MeteorRadius;
        public float BiteWarning;
        public int BiteDepth;
        public int BiteHalfWidth;
        public float[] BreathAtHealth;
        public float StunDuration;
        public float BreathPrep;
        public float BreathStepTime;
        public float BreathWarning;
        public int BreathBeamWidth;
        public int BreathWalls;
        public int BreathCoverHops;
        public float SlowBreathPrep;
        public float SlowBreathStepTime;
        public int LaserDamage;
        public int MeteorDamage;
        public int BiteDamage;
        public int BreathDamage;
    }

    /// <summary>Starts boss attacks at intervals and applies the strikes that land on the player.</summary>
    public sealed class BossAttacks
    {
        readonly PawnMovement _pawn;
        readonly Health _boss;
        readonly PlayerVitals _vitals;
        readonly Random _random;
        readonly BossAttackSettings _settings;
        float _idle;
        readonly float[] _breathAtHealth;
        int _nextBreath;
        float _stunLeft;
        int _laserShotsLeft;
        float _nextLaserShot;
        BossAttackKind _lastBasicAttack;
        int _sameBasicAttackInARow;

        public AttackTimeline Timeline { get; } = new AttackTimeline();
        public BossAttackKind CurrentAttack { get; private set; }
        public BossPhase Phase { get; private set; }

        /// <summary>Strikes that reached the player's cell, including those invulnerability then blocked.</summary>
        public int PlayerHits { get; private set; }

        /// <summary>The attack's target while one runs, otherwise the pawn.</summary>
        public int AimSector => Timeline.AimSector ?? _pawn.Position.X;

        public BossAttacks(PawnMovement pawn, Health boss, PlayerVitals vitals, Random random, BossAttackSettings settings)
        {
            _pawn = pawn;
            _boss = boss;
            _vitals = vitals;
            _random = random;
            _settings = settings;
            _breathAtHealth = settings.BreathAtHealth.OrderByDescending(fraction => fraction).ToArray();
            Timeline.Struck += cells =>
            {
                if (!cells.Contains(_pawn.Position)) return;

                PlayerHits++;
                _vitals.Hit(DamageOf(CurrentAttack), CurrentAttack != BossAttackKind.Bite);
            };
        }

        public void Tick(float deltaTime)
        {
            // A dead boss ends the burn too, so the fight can't be lost after it is won.
            if (!_boss.IsDead) _vitals.Tick(deltaTime);

            if (_boss.IsDead || _vitals.Health.IsDead)
            {
                Timeline.Stop();
                _laserShotsLeft = 0;
                Phase = BossPhase.Pressing;
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
            if (Phase == BossPhase.Breathing)
            {
                Phase = BossPhase.Stunned;
                _stunLeft = _settings.StunDuration;
                return;
            }

            if (Phase == BossPhase.Stunned)
            {
                _stunLeft -= deltaTime;
                if (_stunLeft > 0) return;

                Phase = BossPhase.Pressing;
                _idle = 0;
            }

            if (PassedABreathThreshold())
            {
                StartBreath();
                return;
            }

            _idle += deltaTime;
            if (_idle < _settings.Interval) return;

            _idle = 0;
            StartNextAttack();
        }

        int DamageOf(BossAttackKind kind)
        {
            switch (kind)
            {
                case BossAttackKind.Laser: return _settings.LaserDamage;
                case BossAttackKind.Meteor: return _settings.MeteorDamage;
                case BossAttackKind.Bite: return _settings.BiteDamage;
                default: return _settings.BreathDamage;
            }
        }

        // Uses up every threshold passed so far, so a big hit across two of them means one breath, not two in a row.
        bool PassedABreathThreshold()
        {
            var passed = false;
            // Rounded, because 0.7f * 100 comes out just under 70 on Unity's runtime and the exact health would miss it.
            while (_nextBreath < _breathAtHealth.Length && _boss.Current <= Math.Round(_breathAtHealth[_nextBreath] * _boss.Max))
            {
                passed = true;
                _nextBreath++;
            }

            return passed;
        }

        void StartBreath()
        {
            Phase = BossPhase.Breathing;
            CurrentAttack = BossAttackKind.Breath;
            var breath = BossPatterns.Breath(_pawn.Board, _pawn.Position, _random, _settings);
            _pawn.Board.SetWalls(breath.Walls);
            Timeline.Start(breath.Steps);
        }

        void StartNextAttack()
        {
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
                case BossAttackKind.SlowBreath:
                    Timeline.Start(BossPatterns.SlowBreath(board, _random, _settings));
                    break;
                default:
                    Timeline.Start(new[]
                    {
                        BossPatterns.Bite(board, position.X, _settings.BiteDepth, _settings.BiteHalfWidth, _settings.BiteWarning),
                    });
                    break;
            }
        }

        // Near the boss, bite is favored; no attack runs three times in a row, and the walled breath comes from the health thresholds.
        BossAttackKind PickBasicAttack()
        {
            var options = new List<BossAttackKind>();
            options.AddRange(Enumerable.Repeat(BossAttackKind.Laser, _settings.LaserWeight));
            options.AddRange(Enumerable.Repeat(BossAttackKind.Meteor, _settings.MeteorWeight));
            options.AddRange(Enumerable.Repeat(BossAttackKind.SlowBreath, _settings.SlowBreathWeight));
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
