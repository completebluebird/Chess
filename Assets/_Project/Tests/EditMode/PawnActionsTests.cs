using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class PawnActionsTests
    {
        const float MoveCooldown = 0.2f;
        const float AttackCooldown = 0.4f;
        const float Epsilon = 0.001f;

        static PawnActions Actions(int x, int y, out PawnMovement movement, out Health boss)
        {
            var board = new Board(24, 8);
            movement = new PawnMovement(board, new Cell(x, y));
            boss = new Health(100);
            return new PawnActions(movement, new PawnCombat(movement, boss, 2), MoveCooldown, AttackCooldown);
        }

        [Test]
        public void Move_BlocksTheNextMoveAndAttack_UntilMoveCooldownEnds()
        {
            var actions = Actions(18, 4, out var movement, out _);

            Assert.IsTrue(actions.TryMove(PawnMove.Left));
            Assert.AreEqual(new Cell(17, 4), movement.Position);

            Assert.IsFalse(actions.TryMove(PawnMove.Left));
            Assert.AreEqual(PawnAttackResult.OnCooldown, actions.TryAttack(PawnAttackSide.Left));
            Assert.AreEqual(new Cell(17, 4), movement.Position);

            actions.Tick(MoveCooldown - Epsilon);
            Assert.IsFalse(actions.TryMove(PawnMove.Left));

            actions.Tick(Epsilon * 2);
            Assert.IsTrue(actions.TryMove(PawnMove.Left));
            Assert.AreEqual(new Cell(16, 4), movement.Position);
        }

        [Test]
        public void Attack_BlocksMoveAndAttack_ForTheLongerAttackCooldown()
        {
            var actions = Actions(18, 1, out var movement, out var boss);

            Assert.AreEqual(PawnAttackResult.Hit, actions.TryAttack(PawnAttackSide.Left));

            actions.Tick(AttackCooldown - Epsilon);
            Assert.IsFalse(actions.TryMove(PawnMove.Back));
            Assert.AreEqual(PawnAttackResult.OnCooldown, actions.TryAttack(PawnAttackSide.Right));
            Assert.AreEqual(new Cell(18, 1), movement.Position);
            Assert.AreEqual(98, boss.Current);

            actions.Tick(Epsilon * 2);
            Assert.IsTrue(actions.TryMove(PawnMove.Back));
        }

        [Test]
        public void MissedAttack_SpendsTheAttackCooldown()
        {
            var actions = Actions(18, 4, out _, out var boss);

            Assert.AreEqual(PawnAttackResult.Miss, actions.TryAttack(PawnAttackSide.Left));
            Assert.AreEqual(100, boss.Current);

            actions.Tick(MoveCooldown + Epsilon);
            Assert.IsFalse(actions.IsReady);

            actions.Tick(AttackCooldown - MoveCooldown);
            Assert.IsTrue(actions.IsReady);
        }

        [Test]
        public void BlockedMove_SpendsNoCooldown()
        {
            var actions = Actions(18, 1, out var movement, out _);

            Assert.IsFalse(actions.TryMove(PawnMove.Forward));

            Assert.AreEqual(new Cell(18, 1), movement.Position);
            Assert.IsTrue(actions.IsReady);
            Assert.IsTrue(actions.TryMove(PawnMove.Left));
        }

        [Test]
        public void NothingRunning_IsReady_AndTickStaysAtReady()
        {
            var actions = Actions(18, 4, out _, out _);

            Assert.IsTrue(actions.IsReady);
            actions.Tick(10);
            Assert.IsTrue(actions.IsReady);
        }
    }
}
