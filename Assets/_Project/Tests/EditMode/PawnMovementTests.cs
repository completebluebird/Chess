using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class PawnMovementTests
    {
        const int Margin = 2;

        static PawnMovement Pawn(int x, int y) => new PawnMovement(new Board(13, 3), new Cell(x, y), Margin);

        [TestCase(5, 0, -1, 0)]
        [TestCase(-5, 1, 1, 0)]
        [TestCase(1, 5, 0, -1)]
        [TestCase(-1, -5, 0, 1)]
        public void Forward_SnapsToNearestGridDirectionTowardBoss(int x, int y, int fx, int fy)
        {
            Assert.AreEqual(new Cell(fx, fy), Pawn(x, y).Forward);
        }

        [TestCase(4, 4, -1, 0, -1, 0, Description = "exact diagonal keeps x")]
        [TestCase(4, 4, 0, -1, 0, -1, Description = "exact diagonal keeps y")]
        [TestCase(4, 5, -1, 0, -1, 0, Description = "one cell past the diagonal is not decisive")]
        [TestCase(3, 5, -1, 0, 0, -1, Description = "two cells past the diagonal switches")]
        [TestCase(5, 4, 0, -1, 0, -1, Description = "one cell past the other way is not decisive")]
        [TestCase(5, 3, 0, -1, -1, 0, Description = "two cells past the other way switches")]
        [TestCase(-3, 5, -1, 0, 0, -1, Description = "a direction pointing away from the boss is dropped")]
        public void Forward_KeepsPreviousDirectionUntilDecisivelyPastDiagonal(
            int x, int y, int prevX, int prevY, int fx, int fy)
        {
            var result = PawnMovement.SnapForward(new Cell(x, y), new Cell(prevX, prevY), Margin);

            Assert.AreEqual(new Cell(fx, fy), result);
        }

        [Test]
        public void ForwardAndBack_MoveAlongSnappedDirection()
        {
            var pawn = Pawn(0, -5);

            Assert.IsTrue(pawn.TryMove(PawnMove.Forward));
            Assert.AreEqual(new Cell(0, -4), pawn.Position);
            Assert.IsTrue(pawn.TryMove(PawnMove.Back));
            Assert.AreEqual(new Cell(0, -5), pawn.Position);
        }

        [Test]
        public void Left_OrbitsClockwise_RightOrbitsCounterclockwise()
        {
            var pawn = Pawn(0, -5);

            Assert.IsTrue(pawn.TryMove(PawnMove.Left));
            Assert.AreEqual(new Cell(-1, -5), pawn.Position);
            Assert.IsTrue(pawn.TryMove(PawnMove.Right));
            Assert.AreEqual(new Cell(0, -5), pawn.Position);
        }

        [Test]
        public void Forward_IntoBoss_IsBlocked()
        {
            var pawn = Pawn(0, -2);

            Assert.IsFalse(pawn.TryMove(PawnMove.Forward));
            Assert.AreEqual(new Cell(0, -2), pawn.Position);
        }

        [Test]
        public void Back_OffBoard_IsBlocked()
        {
            var pawn = Pawn(0, -6);

            Assert.IsFalse(pawn.TryMove(PawnMove.Back));
            Assert.AreEqual(new Cell(0, -6), pawn.Position);
        }

        [TestCase(0, -2)]
        [TestCase(0, -6)]
        [TestCase(3, 6)]
        [TestCase(2, 2)]
        public void LeftOnly_CirclesBossOnce_WithoutLeavingBoard(int x, int y)
        {
            var board = new Board(13, 3);
            var pawn = new PawnMovement(board, new Cell(x, y), Margin);
            var ringLength = board.Ring(board.RingOf(pawn.Position)).Count;
            var start = pawn.Position;
            var turned = 0.0;

            for (var i = 0; i < ringLength; i++)
            {
                var from = pawn.Position;
                Assert.IsTrue(pawn.TryMove(PawnMove.Left));
                Assert.IsTrue(board.IsWalkable(pawn.Position));
                turned += System.Math.Atan2(
                    from.X * pawn.Position.Y - from.Y * pawn.Position.X,
                    from.X * pawn.Position.X + from.Y * pawn.Position.Y);
            }

            Assert.AreEqual(start, pawn.Position);
            Assert.AreEqual(-2 * System.Math.PI, turned, 1e-9);
        }
    }
}
