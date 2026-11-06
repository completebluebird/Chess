using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class PlayerVitalsTests
    {
        const float Invulnerable = 0.4f;
        const float BurnDuration = 3f;
        const float BurnTick = 0.5f;
        const int BurnDamage = 1;
        const int BurnTicks = 6;
        const int Damage = 10;
        const int MaxHealth = 50;

        /// <summary>Plenty of health by default, so tests about other rules never end in defeat.</summary>
        public static PlayerVitals Vitals(int maxHealth = 1000000) =>
            new PlayerVitals(maxHealth, Invulnerable, BurnDuration, BurnTick, BurnDamage);

        static void Run(PlayerVitals vitals, float seconds)
        {
            var steps = (int)System.Math.Round(seconds / 0.01f);
            for (var i = 0; i < steps; i++) vitals.Tick(0.01f);
        }

        [Test]
        public void Hit_DealsDamage_AndBlocksTheNextHitUntilInvulnerabilityEnds()
        {
            var vitals = Vitals(MaxHealth);

            Assert.IsTrue(vitals.Hit(Damage, false));
            Assert.AreEqual(MaxHealth - Damage, vitals.Health.Current);
            Assert.IsTrue(vitals.IsInvulnerable);

            vitals.Tick(Invulnerable - 0.01f);
            Assert.IsFalse(vitals.Hit(Damage, false));
            Assert.AreEqual(MaxHealth - Damage, vitals.Health.Current);

            vitals.Tick(0.02f);
            Assert.IsFalse(vitals.IsInvulnerable);
            Assert.IsTrue(vitals.Hit(Damage, false));
            Assert.AreEqual(MaxHealth - Damage * 2, vitals.Health.Current);
        }

        [Test]
        public void HitWithoutBurn_LeavesNoBurn()
        {
            var vitals = Vitals(MaxHealth);

            vitals.Hit(Damage, false);
            Run(vitals, BurnDuration + 1);

            Assert.IsFalse(vitals.IsBurning);
            Assert.AreEqual(MaxHealth - Damage, vitals.Health.Current);
        }

        [Test]
        public void Burn_DealsItsTicksOverItsDuration_ThenEnds()
        {
            var vitals = Vitals(MaxHealth);
            vitals.Hit(Damage, true);
            Assert.IsTrue(vitals.IsBurning);

            Run(vitals, BurnTick - 0.02f);
            Assert.AreEqual(MaxHealth - Damage, vitals.Health.Current);

            Run(vitals, BurnDuration + 1);
            Assert.IsFalse(vitals.IsBurning);
            Assert.AreEqual(MaxHealth - Damage - BurnTicks * BurnDamage, vitals.Health.Current);
        }

        [Test]
        public void Burn_KeepsTicking_WhileInvulnerable()
        {
            var vitals = new PlayerVitals(MaxHealth, BurnDuration, BurnDuration, BurnTick, BurnDamage);
            vitals.Hit(Damage, true);

            Run(vitals, BurnTick * 2 + 0.02f);

            Assert.IsTrue(vitals.IsInvulnerable);
            Assert.AreEqual(MaxHealth - Damage - 2 * BurnDamage, vitals.Health.Current);
        }

        [Test]
        public void SecondBurn_RefreshesDurationOnly_WithoutFasterTicks()
        {
            var vitals = Vitals(MaxHealth);
            vitals.Hit(0, true);
            Run(vitals, 1.25f);
            Assert.AreEqual(MaxHealth - 2 * BurnDamage, vitals.Health.Current);

            vitals.Hit(0, true);

            // Stacked burns would deal two ticks here.
            Run(vitals, BurnTick);
            Assert.AreEqual(MaxHealth - 3 * BurnDamage, vitals.Health.Current);

            // The refresh gives a full duration: 1.25 + 3 = 4.25s, so ticks fall on 0.5 ... 4.0.
            Run(vitals, 2.0f);
            Assert.IsTrue(vitals.IsBurning);
            Run(vitals, 1.0f);
            Assert.IsFalse(vitals.IsBurning);
            Assert.AreEqual(MaxHealth - 8 * BurnDamage, vitals.Health.Current);
        }

        [Test]
        public void BlockedHit_DoesNotRefreshBurn()
        {
            var vitals = new PlayerVitals(MaxHealth, BurnDuration + 1, BurnDuration, BurnTick, BurnDamage);
            vitals.Hit(0, true);
            Run(vitals, BurnDuration - 0.1f);

            Assert.IsFalse(vitals.Hit(0, true));

            Run(vitals, 0.2f);
            Assert.IsFalse(vitals.IsBurning);
        }

        [Test]
        public void BurnCanKill_AndTheDeadTakeNoMoreHits()
        {
            var vitals = Vitals(2);
            vitals.Hit(0, true);

            Run(vitals, BurnDuration);

            Assert.IsTrue(vitals.Health.IsDead);
            Assert.AreEqual(0, vitals.Health.Current);
            Assert.IsFalse(vitals.Hit(Damage, false));
        }

        [TestCase(0.2f, 0.5f, 0.5f)]
        [TestCase(0.45f, 0.1f, 0.45f)]
        public void Invulnerability_Overlapping_KeepsOnlyTheLonger_WithoutAdding(float running, float granted, float expected)
        {
            var vitals = new PlayerVitals(MaxHealth, 0, BurnDuration, BurnTick, BurnDamage);
            vitals.GrantInvulnerability(running);

            vitals.GrantInvulnerability(granted);

            vitals.Tick(expected - 0.01f);
            Assert.IsTrue(vitals.IsInvulnerable);
            vitals.Tick(0.02f);
            Assert.IsFalse(vitals.IsInvulnerable);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonPositiveBurnTick_Throws(float tick)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new PlayerVitals(MaxHealth, Invulnerable, BurnDuration, tick, BurnDamage));
        }
    }
}
