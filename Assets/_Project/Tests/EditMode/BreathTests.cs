using System;
using System.Collections.Generic;
using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class BreathTests
    {
        const float MoveCooldown = 0.2f;
        const float AttackCooldown = 0.4f;
        const int Rings = 8;

        static readonly BossAttackSettings Settings = new BossAttackSettings
        {
            Interval = 2f,
            LaserWarning = 0.8f,
            LaserShotsMin = 3,
            LaserShotsMax = 4,
            LaserShotGap = 0.4f,
            BiteWeight = 2,
            MeteorWarning = 1.5f,
            MeteorRadius = 2,
            BiteWarning = 0.6f,
            BiteDepth = 2,
            BiteHalfWidth = 2,
            BreathAtHealth = new[] { 0.7f, 0.4f },
            StunDuration = 5f,
            BreathPrep = 2f,
            BreathStepTime = 0.1f,
            BreathWarning = 0.6f,
            BreathBeamWidth = 2,
            BreathWalls = 2,
            BreathCoverHops = 5,
        };

        readonly Board _board = new Board(24, Rings);

        BreathPlan Plan(Cell pawn, int seed) => BossPatterns.Breath(_board, pawn, new Random(seed), Settings);

        static HashSet<Cell> Struck(BreathPlan plan) => new HashSet<Cell>(plan.Steps.SelectMany(step => step.Cells));

        static IEnumerable<Cell> SafeCells(Board board, BreathPlan plan)
        {
            var struck = Struck(plan);
            return board.Cells.Where(cell =>
                board.IsWalkable(cell) && !plan.Walls.Contains(cell) && !struck.Contains(cell));
        }

        [Test]
        public void Beam_SweepsOneFullCircle_OneSectorPerStep()
        {
            var plan = Plan(new Cell(18, 4), 1);

            Assert.AreEqual(_board.Sectors, plan.Steps.Count);
            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, _board.Sectors), plan.Steps.SelectMany(step => step.Cells).Select(c => c.X).Distinct());
            for (var i = 1; i < plan.Steps.Count; i++)
                Assert.AreEqual(Settings.BreathStepTime, plan.Steps[i].StrikeTime - plan.Steps[i - 1].StrikeTime, 1e-4f);
        }

        [Test]
        public void EveryStep_IsWarnedAheadOfItsStrike_AndTheFirstStrikeWaitsForPrep()
        {
            var plan = Plan(new Cell(18, 4), 1);

            Assert.AreEqual(Settings.BreathPrep, plan.Steps[0].StrikeTime, 1e-4f);
            foreach (var step in plan.Steps)
                Assert.AreEqual(Settings.BreathWarning, step.StrikeTime - step.WarningStart, 1e-4f);
        }

        [Test]
        public void WallsAndTheCellsBehindThem_AreNeverStruck_CellsInFrontAre()
        {
            for (var seed = 0; seed < 200; seed++)
            {
                var plan = Plan(new Cell(18, 4), seed);
                var struck = Struck(plan);

                foreach (var wall in plan.Walls)
                {
                    for (var ring = wall.Y; ring < Rings; ring++)
                        CollectionAssert.DoesNotContain(struck, new Cell(wall.X, ring), $"seed {seed} wall {wall}");

                    var frontRing = plan.Walls.Where(w => w.X == wall.X).Min(w => w.Y);
                    for (var ring = 1; ring < frontRing; ring++)
                        CollectionAssert.Contains(struck, new Cell(wall.X, ring), $"seed {seed} wall {wall}");
                }
            }
        }

        [Test]
        public void Walls_LeaveCoverBehindThem_AndNeverLandOnThePawn_FromEveryCell()
        {
            foreach (var pawn in _board.Cells.Where(_board.IsWalkable))
            for (var seed = 0; seed < 20; seed++)
            {
                var plan = Plan(pawn, seed);

                Assert.That(plan.Walls.Count, Is.InRange(1, Settings.BreathWalls), $"{pawn} seed {seed}");
                Assert.AreEqual(plan.Walls.Count, plan.Walls.Distinct().Count(), $"{pawn} seed {seed}");
                CollectionAssert.DoesNotContain(plan.Walls, pawn, $"seed {seed}");
                foreach (var wall in plan.Walls)
                    Assert.That(wall.Y, Is.InRange(2, Rings - 2), $"{pawn} seed {seed} wall {wall}");
            }
        }

        [Test]
        public void SafeCell_IsReachableInTime_From1000RandomStarts()
        {
            var starts = _board.Cells.Where(_board.IsWalkable).ToArray();
            var random = new Random(12345);
            for (var seed = 0; seed < 1000; seed++)
                AssertSafeCellInReach(starts[random.Next(starts.Length)], seed);
        }

        [Test]
        public void SafeCell_IsReachableInTime_FromEveryCell()
        {
            foreach (var pawn in _board.Cells.Where(_board.IsWalkable))
            for (var seed = 0; seed < 20; seed++)
                AssertSafeCellInReach(pawn, seed);
        }

        void AssertSafeCellInReach(Cell pawn, int seed)
        {
            var plan = Plan(pawn, seed);
            var path = PathToSafety(pawn, plan);

            Assert.IsNotNull(path, $"{pawn} seed {seed}: no path to cover");
            Assert.LessOrEqual(path.Count, Settings.BreathCoverHops, $"{pawn} seed {seed}");
            // Worst case: the pawn attacked just as the breath began, so it waits out that cooldown before the first hop.
            Assert.LessOrEqual(AttackCooldown + (path.Count - 1) * MoveCooldown, Settings.BreathPrep, $"{pawn} seed {seed}");
        }

        // A separate search from the one the rule uses, so the test can't inherit its mistakes.
        List<Cell> PathToSafety(Cell start, BreathPlan plan)
        {
            var safe = new HashSet<Cell>(SafeCells(_board, plan));
            var cameFrom = new Dictionary<Cell, Cell> { [start] = start };
            var queue = new Queue<Cell>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (safe.Contains(cell))
                {
                    var path = new List<Cell>();
                    for (var at = cell; at != start; at = cameFrom[at]) path.Insert(0, at);
                    return path;
                }

                foreach (var next in new[]
                {
                    _board.Step(cell, 1, 0), _board.Step(cell, -1, 0), _board.Step(cell, 0, 1), _board.Step(cell, 0, -1),
                })
                {
                    if (!_board.IsWalkable(next) || plan.Walls.Contains(next) || cameFrom.ContainsKey(next)) continue;
                    cameFrom[next] = cell;
                    queue.Enqueue(next);
                }
            }

            return null;
        }

        BossAttacks AttacksAtBreath(int seed, out PawnMovement pawn, out Health boss, out Board board)
        {
            board = new Board(24, Rings);
            pawn = new PawnMovement(board, new Cell(18, 4));
            boss = new Health(100);
            var attacks = new BossAttacks(pawn, boss, PlayerVitalsTests.Vitals(), new Random(seed), Settings);
            boss.TakeDamage(30);
            attacks.Tick(0.05f);
            Assert.AreEqual(BossAttackKind.Breath, attacks.CurrentAttack);
            return attacks;
        }

        [Test]
        public void Walls_BlockMovement_WhileBreathRuns_AndVanishAfter()
        {
            var attacks = AttacksAtBreath(1, out var pawn, out _, out var board);
            Assert.Greater(board.Walls.Count, 0);
            var wall = board.Walls.First();
            Assert.IsTrue(board.IsWall(wall));
            Assert.IsFalse(board.IsWalkable(wall));

            // Two ticks, so the second stays shorter than the interval and no new attack starts.
            attacks.Tick(Settings.BreathPrep + Settings.BreathStepTime * 24 - 1);
            attacks.Tick(1);

            Assert.IsFalse(attacks.Timeline.IsRunning);
            Assert.AreEqual(0, board.Walls.Count);
            Assert.IsTrue(board.IsWalkable(wall));
        }

        [Test]
        public void HidingBehindAWall_TakesNoHits_StayingInTheOpen_TakesMultipleHits()
        {
            var multiHit = 0;
            for (var seed = 0; seed < 100; seed++)
            {
                var attacks = AttacksAtBreath(seed, out var pawn, out _, out var board);
                var before = attacks.PlayerHits;
                var position = pawn.Position;
                var open = !board.Walls.Any(wall => wall.X == position.X && wall.Y < position.Y);

                attacks.Tick(Settings.BreathPrep + Settings.BreathStepTime * 24 + 1);

                var hits = attacks.PlayerHits - before;
                if (open)
                {
                    Assert.That(hits, Is.InRange(1, Settings.BreathBeamWidth), $"seed {seed}");
                    if (hits > 1) multiHit++;
                }
                else
                {
                    Assert.AreEqual(0, hits, $"seed {seed}");
                }
            }

            Assert.Greater(multiHit, 0);
        }

        [Test]
        public void WalkingToCoverWithRealMoves_AvoidsEveryHit()
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var attacks = AttacksAtBreath(seed, out var pawn, out _, out var board);
                var before = attacks.PlayerHits;

                foreach (var next in PathBehindWall(board, pawn.Position))
                    Assert.IsTrue(pawn.TryMove(MoveBetween(board, pawn.Position, next)), $"seed {seed} {pawn.Position}->{next}");
                attacks.Tick(Settings.BreathPrep + Settings.BreathStepTime * 24 + 1);

                Assert.AreEqual(before, attacks.PlayerHits, $"seed {seed}");
            }
        }

        [Test]
        public void BreathHeadOn_DealsOneHitAndOneBurn_ThoughTheBeamStrikesTwice()
        {
            const int BreathDamage = 10;
            const int FullBurn = 6;
            var settings = new BossAttackSettings
            {
                Interval = Settings.Interval,
                BreathAtHealth = new[] { 0.7f },
                StunDuration = 5f,
                BreathPrep = Settings.BreathPrep,
                BreathStepTime = Settings.BreathStepTime,
                BreathWarning = Settings.BreathWarning,
                BreathBeamWidth = Settings.BreathBeamWidth,
                BreathWalls = Settings.BreathWalls,
                BreathCoverHops = Settings.BreathCoverHops,
                BreathDamage = BreathDamage,
            };

            var multiHit = 0;
            for (var seed = 0; seed < 100; seed++)
            {
                var board = new Board(24, Rings);
                var pawn = new PawnMovement(board, new Cell(18, 4));
                var vitals = PlayerVitalsTests.Vitals(100);
                var boss = new Health(100);
                var attacks = new BossAttacks(pawn, boss, vitals, new Random(seed), settings);

                boss.TakeDamage(30);
                attacks.Tick(0.01f);
                var open = !board.Walls.Any(wall => wall.X == pawn.Position.X && wall.Y < pawn.Position.Y);
                while (attacks.Timeline.IsRunning) attacks.Tick(0.02f);
                vitals.Tick(10);

                Assert.AreEqual(open ? 100 - BreathDamage - FullBurn : 100, vitals.Health.Current, $"seed {seed}");
                if (attacks.PlayerHits > 1) multiHit++;
            }

            Assert.Greater(multiHit, 0);
        }

        static List<Cell> PathBehindWall(Board board, Cell start)
        {
            var cameFrom = new Dictionary<Cell, Cell> { [start] = start };
            var queue = new Queue<Cell>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (board.Walls.Any(wall => wall.X == cell.X && wall.Y < cell.Y))
                {
                    var path = new List<Cell>();
                    for (var at = cell; at != start; at = cameFrom[at]) path.Insert(0, at);
                    return path;
                }

                foreach (var next in new[]
                {
                    board.Step(cell, 1, 0), board.Step(cell, -1, 0), board.Step(cell, 0, 1), board.Step(cell, 0, -1),
                })
                {
                    if (!board.IsWalkable(next) || cameFrom.ContainsKey(next)) continue;
                    cameFrom[next] = cell;
                    queue.Enqueue(next);
                }
            }

            throw new InvalidOperationException($"No cover from {start}.");
        }

        static PawnMove MoveBetween(Board board, Cell from, Cell to)
        {
            if (to == board.Step(from, 0, -1)) return PawnMove.Forward;
            if (to == board.Step(from, 0, 1)) return PawnMove.Back;
            return to == board.Step(from, -1, 0) ? PawnMove.Left : PawnMove.Right;
        }

        [Test]
        public void DeadBoss_RemovesWalls()
        {
            var attacks = AttacksAtBreath(1, out _, out var boss, out var board);
            boss.TakeDamage(boss.Max);

            attacks.Tick(0.01f);

            Assert.AreEqual(0, board.Walls.Count);
            Assert.IsFalse(attacks.Timeline.IsRunning);
        }
    }
}
