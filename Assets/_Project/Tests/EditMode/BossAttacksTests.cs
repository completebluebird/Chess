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

        readonly Board _board = new Board(24, 8);

        [Test]
        public void Laser_CoversEveryWalkableRingOfItsSectors()
        {
            var step = BossPatterns.Laser(_board, new[] { 5 }, Warning);

            CollectionAssert.AreEquivalent(Enumerable.Range(1, 7).Select(ring => new Cell(5, ring)), step.Cells);
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
        // Moves are this far apart, so the warning time buys warning / MoveCooldown moves.
        const float MoveCooldown = 0.2f;

        static readonly BossAttackSettings Settings = new BossAttackSettings
        {
            Interval = 2f,
            LaserWarning = 0.8f,
            LaserShotsMin = 3,
            LaserShotsMax = 4,
            LaserShotGap = 0.4f,
            LaserWeight = 3,
            MeteorWeight = 3,
            SlowBreathWeight = 2,
            BiteWeight = 6,
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
            SlowBreathPrep = 1f,
            SlowBreathStepTime = 8f / 24,
            LaserDamage = 10,
            MeteorDamage = 20,
            BiteDamage = 30,
            BreathDamage = 10,
        };

        readonly Board _board = new Board(24, 8);
        PlayerVitals _vitals;
        Health _boss;

        BossAttacks Attacks(int seed, Cell start, out PawnMovement pawn, out Health boss)
        {
            pawn = new PawnMovement(_board, start);
            boss = new Health(100);
            _boss = boss;
            _vitals = PlayerVitalsTests.Vitals();
            return new BossAttacks(pawn, boss, _vitals, new Random(seed), Settings);
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
                // The slow breath starts at a sector of its own and comes around to the player.
                if (attacks.CurrentAttack == BossAttackKind.SlowBreath) continue;

                CollectionAssert.Contains(attacks.Timeline.WarningCells, start, $"{start} seed {seed}");
            }
        }

        [Test]
        public void FarFromBoss_LaserMeteorAndSlowBreathAreUsed_ButNoBite()
        {
            var kinds = KindsUsedFrom(new Cell(18, Settings.BiteDepth + 1));

            CollectionAssert.AreEquivalent(
                new[] { BossAttackKind.Laser, BossAttackKind.Meteor, BossAttackKind.SlowBreath }, kinds);
        }

        [Test]
        public void NearBoss_BiteJoinsThem()
        {
            var kinds = KindsUsedFrom(new Cell(18, Settings.BiteDepth));

            CollectionAssert.AreEquivalent(
                new[] { BossAttackKind.Laser, BossAttackKind.Meteor, BossAttackKind.SlowBreath, BossAttackKind.Bite }, kinds);
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
        public void StayingInPlace_MeteorHitsOnce_LaserHitsOncePerShot_SlowBreathOncePerBeamSector()
        {
            var laserHits = new HashSet<int>();
            for (var seed = 0; seed < 100; seed++)
            {
                var attacks = Attacks(seed, out _, out _);
                RunFirstAttack(attacks);

                if (attacks.CurrentAttack == BossAttackKind.Meteor)
                    Assert.AreEqual(1, attacks.PlayerHits, $"seed {seed}");
                else if (attacks.CurrentAttack == BossAttackKind.SlowBreath)
                    // The beam's last sector is only struck by its head.
                    Assert.That(attacks.PlayerHits, Is.InRange(1, Settings.BreathBeamWidth), $"seed {seed}");
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
                // Walking right can walk into a beam coming the other way; the slow breath has its own dodge tests.
                var preview = Attacks(seed, out _, out _);
                preview.Tick(Settings.Interval);
                if (preview.CurrentAttack == BossAttackKind.SlowBreath) continue;

                var attacks = Attacks(seed, out var pawn, out _);
                var hopReady = 0f;
                RunFirstAttack(attacks, () =>
                {
                    hopReady -= 0.05f;
                    var warned = new HashSet<Cell>(attacks.Timeline.WarningCells);
                    if (hopReady > 0 || !warned.Contains(pawn.Position)) return;

                    // Around the ring, whichever way leaves the warned cells sooner: two meteors side by side are too wide to cross.
                    var sectors = 1;
                    while (warned.Contains(_board.Step(pawn.Position, sectors, 0)) && warned.Contains(_board.Step(pawn.Position, -sectors, 0)))
                        sectors++;
                    Assert.IsTrue(pawn.TryMove(warned.Contains(_board.Step(pawn.Position, sectors, 0)) ? PawnMove.Left : PawnMove.Right));
                    hopReady = MoveCooldown;
                });

                Assert.AreEqual(0, attacks.PlayerHits, $"seed {seed} {attacks.CurrentAttack}");
            }
        }

        BossAttacks AttacksStartingWith(BossAttackKind kind, out PawnMovement pawn)
        {
            for (var seed = 0; ; seed++)
            {
                var attacks = Attacks(seed, new Cell(18, 2), out pawn, out _);
                attacks.Tick(Settings.Interval);
                if (attacks.CurrentAttack == kind) return attacks;
            }
        }

        [Test]
        public void Aim_FollowsThePawn_BetweenAttacks()
        {
            var attacks = Attacks(1, out var pawn, out _);

            Assert.AreEqual(pawn.Position.X, attacks.AimSector);
            Assert.IsTrue(pawn.TryMove(PawnMove.Right));
            Assert.AreEqual(pawn.Position.X, attacks.AimSector);
        }

        [TestCase(BossAttackKind.Meteor)]
        [TestCase(BossAttackKind.Bite)]
        public void Aim_StaysOnTheAttack_WhenThePawnMovesAway(BossAttackKind kind)
        {
            var attacks = AttacksStartingWith(kind, out var pawn);
            var target = pawn.Position.X;

            Assert.IsTrue(pawn.TryMove(PawnMove.Right));

            Assert.AreEqual(target, attacks.AimSector);
        }

        [Test]
        public void Aim_IsTheLaserLineStrikingNext()
        {
            var attacks = AttacksStartingWith(BossAttackKind.Laser, out var pawn);
            var first = pawn.Position.X;
            Assert.IsTrue(pawn.TryMove(PawnMove.Right));

            attacks.Tick(Settings.LaserShotGap);
            Assert.AreEqual(first, attacks.AimSector);

            attacks.Tick(Settings.LaserWarning - Settings.LaserShotGap + 0.01f);
            Assert.AreEqual(pawn.Position.X, attacks.AimSector);
        }

        [Test]
        public void Aim_FollowsTheBreathBeamOneSectorPerStep()
        {
            var board = new Board(24, 8);
            var boss = new Health(100);
            var attacks = new BossAttacks(new PawnMovement(board, new Cell(18, 4)), boss, PlayerVitalsTests.Vitals(), new Random(1), Settings);
            boss.TakeDamage(30);
            attacks.Tick(0.05f);
            Assert.AreEqual(BossAttackKind.Breath, attacks.CurrentAttack);
            attacks.Tick(Settings.BreathPrep - 0.01f);
            var first = attacks.AimSector;

            attacks.Tick(0.02f);
            var second = attacks.AimSector;
            attacks.Tick(Settings.BreathStepTime);
            var third = attacks.AimSector;

            Assert.AreEqual(1, board.SectorDistance(first, second));
            Assert.AreEqual(1, board.SectorDistance(second, third));
            Assert.AreEqual(2, board.SectorDistance(first, third));
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
                [BossAttackKind.SlowBreath] = 0,
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
        public void SlowBreath_IsPickedLessOftenThanMeteor()
        {
            var counts = new Dictionary<BossAttackKind, int>
            {
                [BossAttackKind.Laser] = 0, [BossAttackKind.Meteor] = 0, [BossAttackKind.SlowBreath] = 0,
            };
            for (var seed = 0; seed < 2000; seed++)
            {
                var attacks = Attacks(seed, out _, out _);
                attacks.Tick(Settings.Interval);
                counts[attacks.CurrentAttack]++;
            }

            Assert.Greater(counts[BossAttackKind.SlowBreath], 0);
            Assert.Less(counts[BossAttackKind.SlowBreath], counts[BossAttackKind.Meteor]);
        }

        [Test]
        public void SlowBreath_SweepsOneFullCircle_SlowerThanThePawnMoves_AndSheltersNothing()
        {
            var steps = BossPatterns.SlowBreath(_board, new Random(1), Settings);

            Assert.AreEqual(_board.Sectors, steps.Length);
            CollectionAssert.AreEquivalent(_board.Cells.Where(_board.IsWalkable), steps.SelectMany(step => step.Cells).Distinct());
            Assert.AreEqual(Settings.SlowBreathPrep, steps[0].StrikeTime, 1e-4f);
            for (var i = 0; i < steps.Length; i++)
            {
                Assert.AreEqual(Settings.BreathWarning, steps[i].StrikeTime - steps[i].WarningStart, 1e-4f);
                Assert.LessOrEqual(steps[i].Cells.Select(cell => cell.X).Distinct().Count(), Settings.BreathBeamWidth);
                if (i > 0)
                    Assert.Greater(steps[i].StrikeTime - steps[i - 1].StrikeTime, MoveCooldown, "the beam must turn slower than the pawn hops");
            }
        }

        [Test]
        public void SlowBreath_IsABasicAttack_WithNoWallsAndNoStun()
        {
            var attacks = AttacksStartingWith(BossAttackKind.SlowBreath, out _);

            while (attacks.Timeline.IsRunning)
            {
                Assert.AreEqual(BossPhase.Pressing, attacks.Phase);
                Assert.AreEqual(0, _board.Walls.Count);
                attacks.Tick(0.05f);
            }

            attacks.Tick(0.05f);
            Assert.AreEqual(BossPhase.Pressing, attacks.Phase);
        }

        // Plays one slow breath over a pawn that starts the given sectors ahead of the beam's first sector, and returns
        // how many strikes landed on it. The callback says which way to hop; hops are one move cooldown apart.
        int SlowBreathHits(int seed, int sectorsAhead, Func<AttackTimeline, Cell, int, int, int> hopSectors)
        {
            var steps = BossPatterns.SlowBreath(_board, new Random(seed), Settings);
            var way = _board.Step(new Cell(steps[0].AimSector, 1), 1, 0).X == steps[1].AimSector ? 1 : -1;
            var pawn = _board.Step(new Cell(steps[0].AimSector, 3), way * sectorsAhead, 0);
            var timeline = new AttackTimeline();
            var hits = 0;
            var strikes = 0;
            timeline.Struck += cells =>
            {
                strikes++;
                if (cells.Contains(pawn)) hits++;
            };
            timeline.Start(steps);

            var hopReady = 0f;
            while (timeline.IsRunning)
            {
                hopReady -= 0.01f;
                var hop = hopReady <= 0 ? hopSectors(timeline, pawn, way, strikes) : 0;
                if (hop != 0)
                {
                    pawn = _board.Step(pawn, hop, 0);
                    hopReady = MoveCooldown;
                }

                timeline.Tick(0.01f);
            }

            return hits;
        }

        [Test]
        public void SlowBreath_IsOutrun_ByWalkingAheadOfTheBeam()
        {
            for (var seed = 0; seed < 50; seed++)
            for (var ahead = 0; ahead < _board.Sectors; ahead += 5)
                Assert.AreEqual(0, SlowBreathHits(seed, ahead,
                    (timeline, pawn, way, strikes) => timeline.WarningCells.Contains(pawn) ? way : 0), $"seed {seed}, {ahead} ahead");
        }

        [Test]
        public void SlowBreath_CanBeCrossed_BetweenTwoStrikes()
        {
            // From 5 sectors ahead, the beam's fifth strike lands on the sector next to the pawn. Two hops against
            // the beam then reach a sector it has finished with, before the next strike.
            for (var seed = 0; seed < 50; seed++)
                Assert.AreEqual(0, SlowBreathHits(seed, 5,
                    (timeline, pawn, way, strikes) => strikes >= 5 && timeline.WarningCells.Contains(pawn) ? -way : 0), $"seed {seed}");
        }

        [Test]
        public void SlowBreath_StandingStill_IsHit()
        {
            Assert.Greater(SlowBreathHits(1, 5, (timeline, pawn, way, strikes) => 0), 0);
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

                Assert.LessOrEqual(HopsToSafety(start, warned) * MoveCooldown, warning, $"{start} seed {seed} {attacks.CurrentAttack}");
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

        [TestCase(BossAttackKind.Laser, 10, true)]
        [TestCase(BossAttackKind.Meteor, 20, true)]
        [TestCase(BossAttackKind.Bite, 30, false)]
        [TestCase(BossAttackKind.SlowBreath, 10, true)]
        public void Strike_DealsItsDamage_AndOnlyFireBurns(BossAttackKind kind, int damage, bool burns)
        {
            var attacks = AttacksStartingWith(kind, out _);
            var before = _vitals.Health.Current;

            while (attacks.PlayerHits == 0) attacks.Tick(0.01f);

            Assert.AreEqual(before - damage, _vitals.Health.Current);
            Assert.AreEqual(burns, _vitals.IsBurning);
        }

        [Test]
        public void Dodged_LeavesHealthUntouched()
        {
            var attacks = AttacksStartingWith(BossAttackKind.Bite, out var pawn);
            var before = _vitals.Health.Current;

            Assert.IsTrue(pawn.TryMove(PawnMove.Back));
            Assert.IsTrue(pawn.TryMove(PawnMove.Back));
            attacks.Tick(Settings.BiteWarning);

            Assert.AreEqual(before, _vitals.Health.Current);
        }

        [Test]
        public void DeadPlayer_CancelsAttackAndStopsAttacking()
        {
            var attacks = Attacks(1, out _, out _);
            attacks.Tick(Settings.Interval);
            _vitals.Health.TakeDamage(_vitals.Health.Max);
            var hits = attacks.PlayerHits;

            attacks.Tick(Settings.MeteorWarning);
            attacks.Tick(Settings.Interval * 3);

            Assert.AreEqual(hits, attacks.PlayerHits);
            CollectionAssert.IsEmpty(attacks.Timeline.WarningCells);
            Assert.IsFalse(attacks.Timeline.IsRunning);
        }

        [Test]
        public void DeadBoss_StopsTheBurn_SoAWonFightCantBeLost()
        {
            var attacks = AttacksStartingWith(BossAttackKind.Laser, out _);
            while (attacks.PlayerHits == 0) attacks.Tick(0.01f);
            var health = _vitals.Health.Current;

            _boss.TakeDamage(_boss.Max);
            attacks.Tick(10);

            Assert.AreEqual(health, _vitals.Health.Current);
        }
    }
}
