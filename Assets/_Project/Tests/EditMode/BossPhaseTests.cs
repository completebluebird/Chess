using System;
using System.Collections.Generic;
using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class BossPhaseTests
    {
        const float Step = 0.05f;
        const float Stun = 5f;

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
            BreathAtHealth = new[] { 0.4f, 0.7f },
            StunDuration = Stun,
            BreathPrep = 2f,
            BreathStepTime = 0.1f,
            BreathWarning = 0.6f,
            BreathBeamWidth = 2,
            BreathWalls = 2,
            BreathCoverHops = 5,
            SlowBreathPrep = 1f,
            SlowBreathStepTime = 8f / 24,
        };

        Board _board;
        Health _boss;
        PlayerVitals _vitals;
        BossAttacks _attacks;

        [SetUp]
        public void Build()
        {
            _board = new Board(24, 8);
            _boss = new Health(100);
            _vitals = PlayerVitalsTests.Vitals();
            _attacks = new BossAttacks(new PawnMovement(_board, new Cell(18, 4)), _boss, _vitals, new Random(1), Settings);
        }

        void RunUntil(Func<bool> done, float limit = 60)
        {
            for (var elapsed = 0f; !done(); elapsed += Step)
            {
                Assert.Less(elapsed, limit, "never happened");
                _attacks.Tick(Step);
            }
        }

        void RunThroughStun()
        {
            RunUntil(() => _attacks.Phase == BossPhase.Stunned);
            RunUntil(() => _attacks.Phase != BossPhase.Stunned);
        }

        // Counts each attack as it starts, over the given game time.
        List<BossAttackKind> AttacksDuring(float seconds)
        {
            var kinds = new List<BossAttackKind>();
            var wasRunning = _attacks.Timeline.IsRunning;
            for (var elapsed = 0f; elapsed < seconds; elapsed += Step)
            {
                _attacks.Tick(Step);
                if (_attacks.Timeline.IsRunning && !wasRunning) kinds.Add(_attacks.CurrentAttack);
                wasRunning = _attacks.Timeline.IsRunning;
            }

            return kinds;
        }

        [Test]
        public void AboveTheThreshold_TheBossOnlyPresses()
        {
            _boss.TakeDamage(29);

            var kinds = AttacksDuring(60);

            Assert.Greater(kinds.Count, 5);
            CollectionAssert.DoesNotContain(kinds, BossAttackKind.Breath);
            Assert.AreEqual(BossPhase.Pressing, _attacks.Phase);
        }

        [Test]
        public void ReachingTheThreshold_StartsTheBreathAtOnce_WhenIdle()
        {
            _boss.TakeDamage(30);

            _attacks.Tick(Step);

            Assert.AreEqual(BossPhase.Breathing, _attacks.Phase);
            Assert.AreEqual(BossAttackKind.Breath, _attacks.CurrentAttack);
            Assert.Greater(_board.Walls.Count, 0);
        }

        [Test]
        public void ReachingTheThreshold_WaitsForTheRunningAttackToEnd()
        {
            RunUntil(() => _attacks.Timeline.IsRunning);
            var running = _attacks.CurrentAttack;
            _boss.TakeDamage(30);

            _attacks.Tick(Step);
            Assert.AreEqual(BossPhase.Pressing, _attacks.Phase);
            Assert.AreEqual(running, _attacks.CurrentAttack);

            RunUntil(() => _attacks.Phase == BossPhase.Breathing);
        }

        [Test]
        public void BreathEnding_StunsTheBoss_WithNoAttacksOrWalls_ThenPressingResumes()
        {
            _boss.TakeDamage(30);
            RunUntil(() => _attacks.Phase == BossPhase.Stunned);

            Assert.AreEqual(0, _board.Walls.Count);
            Assert.IsFalse(_attacks.Timeline.IsRunning);

            var hits = _attacks.PlayerHits;
            for (var elapsed = Step; elapsed < Stun - Step; elapsed += Step)
            {
                _attacks.Tick(Step);
                Assert.AreEqual(BossPhase.Stunned, _attacks.Phase);
                Assert.IsFalse(_attacks.Timeline.IsRunning);
            }

            Assert.AreEqual(hits, _attacks.PlayerHits);

            RunUntil(() => _attacks.Phase == BossPhase.Pressing, Step * 4);
            Assert.IsFalse(_attacks.Timeline.IsRunning, "the usual gap comes before the next attack");
            RunUntil(() => _attacks.Timeline.IsRunning, Settings.Interval + Step * 2);
            Assert.AreNotEqual(BossAttackKind.Breath, _attacks.CurrentAttack);
        }

        [Test]
        public void TheSameThreshold_NeverBreathesTwice()
        {
            _boss.TakeDamage(30);
            RunThroughStun();

            CollectionAssert.DoesNotContain(AttacksDuring(60), BossAttackKind.Breath);
        }

        [Test]
        public void TheNextThreshold_BreathesAgain()
        {
            _boss.TakeDamage(30);
            RunThroughStun();

            _boss.TakeDamage(30);

            RunUntil(() => _attacks.Phase == BossPhase.Breathing);
        }

        [Test]
        public void CrossingBothThresholdsAtOnce_BreathesOnce()
        {
            _boss.TakeDamage(70);
            RunThroughStun();

            CollectionAssert.DoesNotContain(AttacksDuring(60), BossAttackKind.Breath);
        }

        [Test]
        public void ThresholdReachedDuringTheStun_BreathesRightAfterIt()
        {
            _boss.TakeDamage(30);
            RunUntil(() => _attacks.Phase == BossPhase.Stunned);

            _boss.TakeDamage(30);
            _attacks.Tick(Step);
            Assert.AreEqual(BossPhase.Stunned, _attacks.Phase);

            RunUntil(() => _attacks.Phase != BossPhase.Stunned);
            Assert.AreEqual(BossPhase.Breathing, _attacks.Phase);
        }

        [Test]
        public void DeadBoss_EndsTheStun()
        {
            _boss.TakeDamage(30);
            RunUntil(() => _attacks.Phase == BossPhase.Stunned);

            _boss.TakeDamage(_boss.Max);
            _attacks.Tick(Step);

            Assert.AreEqual(BossPhase.Pressing, _attacks.Phase);
            Assert.IsFalse(_attacks.Timeline.IsRunning);
        }

        [Test]
        public void DeadPlayer_EndsTheBreathAndItsWalls()
        {
            _boss.TakeDamage(30);
            _attacks.Tick(Step);
            Assert.AreEqual(BossPhase.Breathing, _attacks.Phase);

            _vitals.Health.TakeDamage(_vitals.Health.Max);
            _attacks.Tick(Step);

            Assert.AreEqual(BossPhase.Pressing, _attacks.Phase);
            Assert.AreEqual(0, _board.Walls.Count);
        }

        [Test]
        public void GaugeChargePaused_HoldsTheGauge_ThenResumes()
        {
            var awakening = new Awakening(20, 3);
            awakening.Tick(10, 10);

            awakening.ChargePaused = true;
            awakening.Tick(5, 5);
            Assert.AreEqual(0.5f, awakening.Gauge, 1e-5f);

            awakening.ChargePaused = false;
            awakening.Tick(5, 5);
            Assert.AreEqual(0.75f, awakening.Gauge, 1e-5f);
        }

        [Test]
        public void GaugeChargePaused_StillRunsAWaitingPiecesWindow()
        {
            var awakening = new Awakening(20, 3);
            awakening.Tick(20, 20);
            awakening.TryAwaken(AwakenPiece.Rook);

            awakening.ChargePaused = true;
            awakening.Tick(0, 4);

            Assert.IsNull(awakening.Active);
        }
    }
}
