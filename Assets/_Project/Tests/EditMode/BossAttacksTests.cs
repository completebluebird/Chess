using System;
using System.Collections.Generic;
using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class BossPatternsTests
    {
        const float Warning = 1f;

        readonly Board _board = new Board(24, 6);

        [Test]
        public void Laser_CoversEveryWalkableRingOfItsSectors()
        {
            var step = BossPatterns.Laser(_board, new[] { 5 }, Warning);

            CollectionAssert.AreEquivalent(Enumerable.Range(1, 5).Select(ring => new Cell(5, ring)), step.Cells);
            Assert.AreEqual(Warning, step.StrikeTime - step.WarningStart, 1e-5f);
        }

        [Test]
        public void Meteor_IsASquareAroundItsCenter()
        {
            var step = BossPatterns.Meteor(_board, new[] { new Cell(10, 3) }, 2, Warning);

            Assert.AreEqual(25, step.Cells.Count);
            CollectionAssert.IsSubsetOf(new[] { new Cell(10, 3), new Cell(12, 5), new Cell(8, 1), new Cell(12, 1), new Cell(8, 5) }, step.Cells);
            CollectionAssert.DoesNotContain(step.Cells, new Cell(13, 3));
            Assert.AreEqual(Warning, step.StrikeTime - step.WarningStart, 1e-5f);
        }

        [Test]
        public void Meteor_WrapsAcrossTheSectorSeam()
        {
            var step = BossPatterns.Meteor(_board, new[] { new Cell(0, 3) }, 2, Warning);

            CollectionAssert.IsSubsetOf(new[] { new Cell(22, 3), new Cell(23, 3), new Cell(2, 3) }, step.Cells);
        }

        [Test]
        public void Meteor_StaysOnWalkableCells_AndOverlapsDoNotRepeat()
        {
            var step = BossPatterns.Meteor(_board, new[] { new Cell(10, 1), new Cell(11, 1) }, 2, Warning);

            Assert.IsTrue(step.Cells.All(_board.IsWalkable));
            Assert.AreEqual(step.Cells.Count, step.Cells.Distinct().Count());
        }

        [Test]
        public void Bite_CoversInnerRingsInFrontOfFacing_AcrossTheSeam()
        {
            var step = BossPatterns.Bite(_board, 0, 2, 2, Warning);

            var expected = new[] { 22, 23, 0, 1, 2 }
                .SelectMany(sector => new[] { new Cell(sector, 1), new Cell(sector, 2) });
            CollectionAssert.AreEquivalent(expected, step.Cells);
            Assert.AreEqual(Warning, step.StrikeTime - step.WarningStart, 1e-5f);
        }
    }

    public class BossAttacksTests
    {
        // A pawn hop takes this long, so the warning time buys warning / HopTime moves.
        const float HopTime = 0.15f;

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
            BreathEvery = 4,
            BreathPrep = 2f,
            BreathStepTime = 0.15f,
            BreathWarning = 0.6f,
            BreathBeamWidth = 2,
            BreathWalls = 2,
            BreathCoverHops = 5,
        };

        readonly Board _board = new Board(24, 6);

        BossAttacks Attacks(int seed, Cell start, out PawnMovement pawn, out Health boss)
        {
            pawn = new PawnMovement(_board, start);
            boss = new Health(100);
            return new BossAttacks(pawn, boss, new Random(seed), Settings);
        }

        BossAttacks Attacks(int seed, out PawnMovement pawn, out Health boss) =>
            Attacks(seed, new Cell(18, 4), out pawn, out boss);

        [Test]
        public void NoAttack_BeforeTheIntervalPasses()
        {
            var attacks = Attacks(1, out _, out _);

            attacks.Tick(Settings.Interval * 0.9f);

            Assert.IsFalse(attacks.Timeline.IsRunning);
        }

        [Test]
        public void EveryAttack_CoversThePlayersCell_FromEveryCell()
        {
            foreach (var start in _board.Cells.Where(_board.IsWalkable))
            for (var seed = 0; seed < 20; seed++)
            {
                var attacks = Attacks(seed, start, out _, out _);

                attacks.Tick(Settings.Interval);

                CollectionAssert.Contains(attacks.Timeline.WarningCells, start, $"{start} seed {seed}");
            }
        }

        [Test]
        public void FarFromBoss_OnlyLaserAndMeteorAreUsed()
        {
            var kinds = KindsUsedFrom(new Cell(18, Settings.BiteDepth + 1));

            CollectionAssert.AreEquivalent(new[] { BossAttackKind.Laser, BossAttackKind.Meteor }, kinds);
        }

        [Test]
        public void NearBoss_AllThreeAttacksAreUsed()
        {
            var kinds = KindsUsedFrom(new Cell(18, Settings.BiteDepth));

            CollectionAssert.AreEquivalent(
                new[] { BossAttackKind.Laser, BossAttackKind.Meteor, BossAttackKind.Bite }, kinds);
        }

        HashSet<BossAttackKind> KindsUsedFrom(Cell start)
        {
            var kinds = new HashSet<BossAttackKind>();
            for (var seed = 0; seed < 200; seed++)
            {
                var attacks = Attacks(seed, start, out _, out _);
                attacks.Tick(Settings.Interval);
                kinds.Add(attacks.CurrentAttack);
            }

            return kinds;
        }

        // Runs the first attack to its end, returning how many laser lines it warned over time.
        static HashSet<int> RunFirstAttack(BossAttacks attacks, Action beforeTick = null)
        {
            var sectors = new HashSet<int>();
            attacks.Tick(Settings.Interval);
            while (attacks.Timeline.IsRunning)
            {
                beforeTick?.Invoke();
                sectors.UnionWith(attacks.Timeline.WarningCells.Select(cell => cell.X));
                attacks.Tick(0.05f);
            }

            return sectors;
        }

        [Test]
        public void StayingInPlace_MeteorHitsOnce_LaserHitsOncePerShot()
        {
            var laserHits = new HashSet<int>();
            for (var seed = 0; seed < 100; seed++)
            {
                var attacks = Attacks(seed, out _, out _);
                RunFirstAttack(attacks);

                if (attacks.CurrentAttack == BossAttackKind.Meteor)
                    Assert.AreEqual(1, attacks.PlayerHits, $"seed {seed}");
                else
                    laserHits.Add(attacks.PlayerHits);
            }

            CollectionAssert.AreEquivalent(new[] { Settings.LaserShotsMin, Settings.LaserShotsMax }, laserHits);
        }

        [Test]
        public void LaserShots_ChaseThePawn_OneLineForEachCellItMovedTo()
        {
            for (var seed = 0; ; seed++)
            {
                var attacks = Attacks(seed, out var pawn, out _);
                attacks.Tick(Settings.Interval);
                if (attacks.CurrentAttack != BossAttackKind.Laser) continue;

                var first = attacks.Timeline.WarningCells.First().X;
                Assert.IsTrue(pawn.TryMove(PawnMove.Right));
                attacks.Tick(Settings.LaserShotGap);

                CollectionAssert.AreEquivalent(
                    new[] { first, pawn.Position.X }, attacks.Timeline.WarningCells.Select(cell => cell.X).Distinct());
                return;
            }
        }

        [Test]
        public void KeepingOnTheMove_DodgesEveryAttack_AtRealHopSpeed()
        {
            for (var seed = 0; seed < 200; seed++)
            {
                var attacks = Attacks(seed, out var pawn, out _);
                var hopReady = 0f;
                RunFirstAttack(attacks, () =>
                {
                    hopReady -= 0.05f;
                    if (hopReady > 0 || !attacks.Timeline.WarningCells.Contains(pawn.Position)) return;
                    Assert.IsTrue(pawn.TryMove(PawnMove.Right));
                    hopReady = HopTime;
                });

                Assert.AreEqual(0, attacks.PlayerHits, $"seed {seed} {attacks.CurrentAttack}");
            }
        }

        // Plays with a still pawn and returns each attack's kind in the order they started.
        List<BossAttackKind> AttackSequence(int seed, Cell start, int count)
        {
            var attacks = Attacks(seed, start, out _, out _);
            var kinds = new List<BossAttackKind>();
            var wasRunning = false;
            while (kinds.Count < count)
            {
                attacks.Tick(0.05f);
                if (attacks.Timeline.IsRunning && !wasRunning) kinds.Add(attacks.CurrentAttack);
                wasRunning = attacks.Timeline.IsRunning;
            }

            return kinds;
        }

        [Test]
        public void SameBasicAttack_NeverRunsThreeTimesInARow()
        {
            foreach (var start in new[] { new Cell(18, 4), new Cell(18, 1) })
            for (var seed = 0; seed < 100; seed++)
            {
                var kinds = AttackSequence(seed, start, 12).Where(kind => kind != BossAttackKind.Breath).ToArray();

                for (var i = 2; i < kinds.Length; i++)
                    Assert.IsFalse(kinds[i] == kinds[i - 1] && kinds[i] == kinds[i - 2], $"{start} seed {seed}: {string.Join(",", kinds)}");
            }
        }

        [Test]
        public void NearBoss_BiteIsPickedMoreOftenThanEachOtherAttack()
        {
            var counts = new Dictionary<BossAttackKind, int>
            {
                [BossAttackKind.Laser] = 0, [BossAttackKind.Meteor] = 0, [BossAttackKind.Bite] = 0,
            };
            for (var seed = 0; seed < 500; seed++)
            {
                var attacks = Attacks(seed, new Cell(18, Settings.BiteDepth), out _, out _);
                attacks.Tick(Settings.Interval);
                counts[attacks.CurrentAttack]++;
            }

            Assert.Greater(counts[BossAttackKind.Bite], counts[BossAttackKind.Laser]);
            Assert.Greater(counts[BossAttackKind.Bite], counts[BossAttackKind.Meteor]);
        }

        [Test]
        public void EveryAttack_LeavesASafeCellReachableInTime_FromEveryCell()
        {
            foreach (var start in _board.Cells.Where(_board.IsWalkable))
            for (var seed = 0; seed < 50; seed++)
            {
                var attacks = Attacks(seed, start, out _, out _);
                attacks.Tick(Settings.Interval);
                var warned = new HashSet<Cell>(attacks.Timeline.WarningCells);
                var warning = WarningTimeOf(attacks.CurrentAttack);

                Assert.LessOrEqual(HopsToSafety(start, warned) * HopTime, warning, $"{start} seed {seed} {attacks.CurrentAttack}");
            }
        }

        static float WarningTimeOf(BossAttackKind kind)
        {
            switch (kind)
            {
                case BossAttackKind.Laser: return Settings.LaserWarning;
                case BossAttackKind.Meteor: return Settings.MeteorWarning;
                default: return Settings.BiteWarning;
            }
        }

        int HopsToSafety(Cell start, HashSet<Cell> warned)
        {
            var hops = new Dictionary<Cell, int> { [start] = 0 };
            var queue = new Queue<Cell>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (!warned.Contains(cell)) return hops[cell];

                foreach (var next in new[]
                {
                    _board.Step(cell, 1, 0), _board.Step(cell, -1, 0), _board.Step(cell, 0, 1), _board.Step(cell, 0, -1),
                })
                {
                    if (!_board.IsWalkable(next) || hops.ContainsKey(next)) continue;
                    hops[next] = hops[cell] + 1;
                    queue.Enqueue(next);
                }
            }

            return int.MaxValue / 1000;
        }

        [Test]
        public void EveryStrike_WasWarnedFirst_AcrossManySeeds()
        {
            var shortest = Settings.BiteWarning;
            foreach (var start in new[] { new Cell(18, 4), new Cell(18, 1) })
            for (var seed = 0; seed < 1000; seed++)
            {
                var attacks = Attacks(seed, start, out _, out _);
                var random = new Random(seed);
                var warnedFor = new Dictionary<Cell, float>();
                var strikes = 0;
                attacks.Timeline.Struck += cells =>
                {
                    strikes++;
                    foreach (var cell in cells)
                        Assert.GreaterOrEqual(warnedFor[cell], shortest * 0.8f, $"seed {seed} {cell}");
                };

                for (var i = 0; i < 200; i++)
                {
                    // A cell can be struck twice in one attack, so its warning time only resets between attacks.
                    if (!attacks.Timeline.IsRunning) warnedFor.Clear();
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
            attacks.Tick(Settings.Interval);
            boss.TakeDamage(boss.Max);

            attacks.Tick(Settings.MeteorWarning);
            attacks.Tick(Settings.Interval * 3);

            Assert.AreEqual(0, attacks.PlayerHits);
            CollectionAssert.IsEmpty(attacks.Timeline.WarningCells);
            Assert.IsFalse(attacks.Timeline.IsRunning);
        }
    }
}
