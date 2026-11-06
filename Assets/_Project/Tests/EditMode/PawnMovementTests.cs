using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class PawnMovementTests
    {
        const int Sectors = 24;
        const int Rings = 8;

        static PawnMovement Pawn(int x, int y) => new PawnMovement(new Board(Sectors, Rings), new Cell(x, y));

        [Test]
        public void ForwardAndBack_ChangeRing_FromEverySector()
        {
            for (var x = 0; x < Sectors; x++)
            {
                var pawn = Pawn(x, 3);

                Assert.IsTrue(pawn.TryMove(PawnMove.Forward));
                Assert.AreEqual(new Cell(x, 2), pawn.Position);
                Assert.IsTrue(pawn.TryMove(PawnMove.Back));
                Assert.AreEqual(new Cell(x, 3), pawn.Position);
            }
        }

        [Test]
        public void Left_LowersSector_RightRaisesSector()
        {
            var pawn = Pawn(18, 4);

            Assert.IsTrue(pawn.TryMove(PawnMove.Left));
            Assert.AreEqual(new Cell(17, 4), pawn.Position);
            Assert.IsTrue(pawn.TryMove(PawnMove.Right));
            Assert.AreEqual(new Cell(18, 4), pawn.Position);
        }

        [Test]
        public void Forward_IntoBoss_IsBlocked()
        {
            var pawn = Pawn(18, 1);

            Assert.IsFalse(pawn.TryMove(PawnMove.Forward));
            Assert.AreEqual(new Cell(18, 1), pawn.Position);
        }

        [Test]
        public void Back_OffBoard_IsBlocked()
        {
            var pawn = Pawn(18, Rings - 1);

            Assert.IsFalse(pawn.TryMove(PawnMove.Back));
            Assert.AreEqual(new Cell(18, Rings - 1), pawn.Position);
        }

        [Test]
        public void Start_OnBossRing_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => Pawn(0, Board.BossRing));
        }

        [TestCase(PawnMove.Left)]
        [TestCase(PawnMove.Right)]
        public void Sidestep_CirclesBossOnce_StayingOnItsRing(PawnMove move)
        {
            for (var y = 1; y < Rings; y++)
            {
                var pawn = Pawn(18, y);

                for (var i = 0; i < Sectors; i++)
                {
                    Assert.IsTrue(pawn.TryMove(move));
                    Assert.AreEqual(y, pawn.Position.Y);
                }

                Assert.AreEqual(new Cell(18, y), pawn.Position);
            }
        }
    }
}
