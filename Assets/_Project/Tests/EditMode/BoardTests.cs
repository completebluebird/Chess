using System;
using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class BoardTests
    {
        [TestCase(24, 6, 144)]
        [TestCase(8, 2, 16)]
        public void CellCount_IsSectorsTimesRings(int sectors, int rings, int expected)
        {
            var board = new Board(sectors, rings);

            Assert.AreEqual(expected, board.Cells.Count());
            Assert.AreEqual(expected - sectors, board.Cells.Count(board.IsWalkable));
        }

        [Test]
        public void BossRing_IsOnBoardButNotWalkable()
        {
            var board = new Board(24, 8);

            for (var x = 0; x < 24; x++)
            {
                var cell = new Cell(x, Board.BossRing);
                Assert.IsTrue(board.Contains(cell));
                Assert.IsTrue(board.IsBossCell(cell));
                Assert.IsFalse(board.IsWalkable(cell));
            }

            Assert.IsFalse(board.IsBossCell(new Cell(0, 1)));
            Assert.IsTrue(board.IsWalkable(new Cell(0, 1)));
        }

        [TestCase(-1, 3)]
        [TestCase(24, 3)]
        [TestCase(0, -1)]
        [TestCase(0, 8)]
        public void CellsOutsideTheGrid_AreNotOnBoard(int x, int y)
        {
            var board = new Board(24, 8);

            Assert.IsFalse(board.Contains(new Cell(x, y)));
            Assert.IsFalse(board.IsBossCell(new Cell(x, y)));
        }

        [TestCase(23, 6, Description = "odd sectors break the checker pattern at the seam")]
        [TestCase(2, 6)]
        [TestCase(24, 1, Description = "no walkable ring")]
        public void InvalidDimensions_Throw(int sectors, int rings)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Board(sectors, rings));
        }

        [TestCase(5, 3, 1, -1, 6, 2)]
        [TestCase(23, 3, 1, 0, 0, 3)]
        [TestCase(0, 3, -1, 0, 23, 3)]
        [TestCase(0, 1, -1, -1, 23, 0)]
        public void Step_WrapsAroundTheSectorSeam(int x, int y, int dx, int dy, int ex, int ey)
        {
            var board = new Board(24, 8);

            Assert.AreEqual(new Cell(ex, ey), board.Step(new Cell(x, y), dx, dy));
        }

        [Test]
        public void CheckerPattern_AlternatesAcrossEveryEdge_IncludingTheSeam()
        {
            var board = new Board(24, 8);

            foreach (var cell in board.Cells)
            {
                var around = board.Step(cell, 1, 0);
                Assert.AreNotEqual((cell.X + cell.Y) & 1, (around.X + around.Y) & 1, $"{cell} -> {around}");
            }
        }
    }
}
