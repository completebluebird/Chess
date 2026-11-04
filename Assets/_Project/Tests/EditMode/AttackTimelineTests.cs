using System;
using System.Collections.Generic;
using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class AttackTimelineTests
    {
        static readonly Cell A = new Cell(0, 1);
        static readonly Cell B = new Cell(1, 1);

        static AttackStep Step(float warningStart, float strikeTime, params Cell[] cells) =>
            new AttackStep(cells, warningStart, strikeTime);

        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        public void Step_WithoutWarningTime_Throws(float warningStart, float strikeTime)
        {
            Assert.Throws<ArgumentException>(() => Step(warningStart, strikeTime, A));
        }

        [Test]
        public void Step_WithoutCells_Throws()
        {
            Assert.Throws<ArgumentException>(() => Step(0, 1));
        }

        [Test]
        public void Warning_ShowsOnlyBetweenWarningStartAndStrike()
        {
            var timeline = new AttackTimeline();
            timeline.Start(new[] { Step(1, 2, A) });

            CollectionAssert.IsEmpty(timeline.WarningCells);
            timeline.Tick(1);
            CollectionAssert.AreEqual(new[] { A }, timeline.WarningCells);
            timeline.Tick(1);
            CollectionAssert.IsEmpty(timeline.WarningCells);
        }

        [Test]
        public void WarningProgress_RisesFromZeroToOne()
        {
            var timeline = new AttackTimeline();
            timeline.Start(new[] { Step(1, 3, A) });

            Assert.AreEqual(0f, timeline.WarningProgress(A));
            timeline.Tick(2);
            Assert.AreEqual(0.5f, timeline.WarningProgress(A), 1e-5f);
            Assert.AreEqual(0f, timeline.WarningProgress(B));
        }

        [Test]
        public void WarningProgress_UsesTheStepClosestToStriking()
        {
            var timeline = new AttackTimeline();
            timeline.Start(new[] { Step(0, 2, A), Step(1, 5, A) });

            timeline.Tick(1);

            Assert.AreEqual(0.5f, timeline.WarningProgress(A), 1e-5f);
        }

        [Test]
        public void Strike_FiresOnceWithTheWarnedCells()
        {
            var timeline = new AttackTimeline();
            var strikes = new List<IReadOnlyList<Cell>>();
            timeline.Struck += strikes.Add;
            timeline.Start(new[] { Step(0, 1, A, B) });

            timeline.Tick(0.5f);
            CollectionAssert.IsEmpty(strikes);
            var warned = timeline.WarningCells.ToArray();

            timeline.Tick(0.5f);
            timeline.Tick(10);

            Assert.AreEqual(1, strikes.Count);
            CollectionAssert.AreEqual(warned, strikes[0]);
            Assert.IsFalse(timeline.IsRunning);
        }

        [Test]
        public void LargeTick_FiresEveryPassedStepInStrikeOrder()
        {
            var timeline = new AttackTimeline();
            var strikes = new List<Cell>();
            timeline.Struck += cells => strikes.Add(cells[0]);
            timeline.Start(new[] { Step(1, 3, B), Step(0, 1, A) });

            timeline.Tick(10);

            CollectionAssert.AreEqual(new[] { A, B }, strikes);
        }

        [Test]
        public void Start_WhileRunning_Throws()
        {
            var timeline = new AttackTimeline();
            timeline.Start(new[] { Step(0, 1, A) });

            Assert.Throws<InvalidOperationException>(() => timeline.Start(new[] { Step(0, 1, B) }));
        }

        [Test]
        public void Stop_ClearsWarningsWithoutStriking()
        {
            var timeline = new AttackTimeline();
            var strikes = 0;
            timeline.Struck += _ => strikes++;
            timeline.Start(new[] { Step(0, 1, A) });
            timeline.Tick(0.5f);

            timeline.Stop();
            timeline.Tick(10);

            CollectionAssert.IsEmpty(timeline.WarningCells);
            Assert.AreEqual(0, strikes);
            Assert.IsFalse(timeline.IsRunning);
        }
    }

    public class BossAttacksTests
    {
        const float Warning = 1f;
        const float Interval = 2f;

        readonly Board _board = new Board(24, 6);

        BossAttacks Attacks(int seed, out PawnMovement pawn, out Health boss)
        {
            pawn = new PawnMovement(_board, new Cell(18, 4));
            boss = new Health(100);
            return new BossAttacks(pawn, boss, new Random(seed), Warning, Interval);
        }

        [Test]
        public void Laser_CoversEveryWalkableRingOfItsSectors()
        {
            var step = BossPatterns.Laser(_board, new[] { 5 }, Warning);

            CollectionAssert.AreEquivalent(
                Enumerable.Range(1, 5).Select(ring => new Cell(5, ring)), step.Cells);
            Assert.AreEqual(Warning, step.StrikeTime - step.WarningStart, 1e-5f);
        }

        [Test]
        public void NoAttack_BeforeTheIntervalPasses()
        {
            var attacks = Attacks(1, out _, out _);

            attacks.Tick(Interval * 0.9f);

            Assert.IsFalse(attacks.Timeline.IsRunning);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Laser_AimsAtPlayersSector_WithOneOrTwoDistinctLines(int seed)
        {
            var attacks = Attacks(seed, out var pawn, out _);

            attacks.Tick(Interval);

            var sectors = attacks.Timeline.WarningCells.Select(c => c.X).Distinct().ToArray();
            CollectionAssert.Contains(sectors, pawn.Position.X);
            Assert.That(sectors.Length, Is.InRange(1, 2));
        }

        [Test]
        public void StayingOnTheLine_CountsOneHit_MovingOffDodges()
        {
            var attacks = Attacks(1, out var pawn, out _);
            attacks.Tick(Interval);
            attacks.Tick(Warning);
            Assert.AreEqual(1, attacks.PlayerHits);

            attacks.Tick(Interval);
            var warned = new HashSet<Cell>(attacks.Timeline.WarningCells);
            Assert.IsTrue(pawn.TryMove(warned.Contains(_board.Step(pawn.Position, 1, 0)) ? PawnMove.Left : PawnMove.Right));
            attacks.Tick(Warning);

            Assert.AreEqual(1, attacks.PlayerHits);
        }

        [Test]
        public void EveryStrike_WasWarnedForTheFullWarningTime_AcrossManySeeds()
        {
            for (var seed = 0; seed < 1000; seed++)
            {
                var attacks = Attacks(seed, out _, out _);
                var random = new Random(seed);
                var warnedFor = new Dictionary<Cell, float>();
                var strikes = 0;
                attacks.Timeline.Struck += cells =>
                {
                    strikes++;
                    foreach (var cell in cells)
                        Assert.GreaterOrEqual(warnedFor[cell], Warning * 0.8f, $"seed {seed} {cell}");
                    warnedFor.Clear();
                };

                for (var i = 0; i < 200; i++)
                {
                    var dt = (float)random.NextDouble() * 0.1f + 0.01f;
                    foreach (var cell in attacks.Timeline.WarningCells)
                    {
                        warnedFor.TryGetValue(cell, out var total);
                        warnedFor[cell] = total + dt;
                    }

                    attacks.Tick(dt);
                }

                Assert.Greater(strikes, 0, $"seed {seed} never struck");
            }
        }

        [Test]
        public void DeadBoss_CancelsAttackAndStopsAttacking()
        {
            var attacks = Attacks(1, out _, out var boss);
            attacks.Tick(Interval);
            boss.TakeDamage(boss.Max);

            attacks.Tick(Warning);
            attacks.Tick(Interval * 3);

            Assert.AreEqual(0, attacks.PlayerHits);
            CollectionAssert.IsEmpty(attacks.Timeline.WarningCells);
            Assert.IsFalse(attacks.Timeline.IsRunning);
        }
    }
}
