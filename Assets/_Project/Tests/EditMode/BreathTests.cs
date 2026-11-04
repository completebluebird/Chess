using System;
using System.Collections.Generic;
using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class BreathTests
    {
        const float HopTime = 0.15f;
        const int Rings = 6;

        static readonly BossAttackSettings Settings = new BossAttackSettings
        {
            Interval = 2f,
            LaserWarning = 1.2f,
            MeteorWarning = 1.5f,
            MeteorRadius = 2,
            BiteWarning = 0.6f,
            BiteDepth = 2,
            BiteHalfWidth = 2,
            BreathEvery = 4,
            BreathPrep = 2f,
            BreathStepTime = 0.15f,
            BreathWarning = 0.6f,
            BreathBeamWidth = 2,
            BreathWalls = 4,
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
            Assert.LessOrEqual(path.Count * HopTime, Settings.BreathPrep, $"{pawn} seed {seed}");
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
            var attacks = new BossAttacks(pawn, boss, new Random(seed), Settings);
            for (var i = 0; i < Settings.BreathEvery; i++)
                attacks.Tick(10);
            return attacks;
        }

        [Test]
        public void Breath_IsEveryFourthAttack_AndTheOthersAreNot()
        {
            var board = new Board(24, Rings);
            var attacks = new BossAttacks(new PawnMovement(board, new Cell(18, 4)), new Health(100), new Random(1), Settings);

            var kinds = new List<BossAttackKind>();
            for (var i = 0; i < 8; i++)
            {
                attacks.Tick(10);
                kinds.Add(attacks.CurrentAttack);
            }

            CollectionAssert.AreEqual(new[] { 3, 7 },
                kinds.Select((kind, i) => (kind, i)).Where(k => k.kind == BossAttackKind.Breath).Select(k => k.i));
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
