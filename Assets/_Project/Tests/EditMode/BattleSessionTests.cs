using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class BattleSessionTests
    {
        const float Delay = 1.5f;

        Health _boss;
        Health _player;
        BattleSession _session;

        [SetUp]
        public void Build()
        {
            _boss = new Health(100);
            _player = new Health(50);
            _session = new BattleSession(_boss, _player, Delay);
        }

        [Test]
        public void WhileBothLive_TheFightGoesOn_AndTheClockRuns()
        {
            _session.Tick(2);
            _session.Tick(3);

            Assert.AreEqual(BattleOutcome.Fighting, _session.Outcome);
            Assert.IsFalse(_session.ResultReady);
            Assert.AreEqual(5f, _session.Elapsed, 1e-5f);
        }

        [Test]
        public void BossDying_IsVictory_AndStopsTheClock()
        {
            _session.Tick(10);

            _boss.TakeDamage(_boss.Max);
            _session.Tick(1);
            _session.Tick(1);

            Assert.AreEqual(BattleOutcome.Victory, _session.Outcome);
            Assert.AreEqual(10f, _session.Elapsed, 1e-5f);
        }

        [Test]
        public void PlayerDying_IsDefeat()
        {
            _player.TakeDamage(_player.Max);

            _session.Tick(1);

            Assert.AreEqual(BattleOutcome.Defeat, _session.Outcome);
        }

        [Test]
        public void BothDyingTogether_IsVictory()
        {
            _boss.TakeDamage(_boss.Max);
            _player.TakeDamage(_player.Max);

            _session.Tick(1);

            Assert.AreEqual(BattleOutcome.Victory, _session.Outcome);
        }

        [Test]
        public void TheOutcome_NeverChangesOnceDecided()
        {
            _boss.TakeDamage(_boss.Max);
            _session.Tick(1);

            _player.TakeDamage(_player.Max);
            _session.Tick(1);

            Assert.AreEqual(BattleOutcome.Victory, _session.Outcome);
        }

        [Test]
        public void TheResult_WaitsForTheDelayAfterTheEnd()
        {
            _player.TakeDamage(_player.Max);
            _session.Tick(0);

            _session.Tick(Delay - 0.01f);
            Assert.IsFalse(_session.ResultReady);

            _session.Tick(0.02f);
            Assert.IsTrue(_session.ResultReady);
        }
    }
}
