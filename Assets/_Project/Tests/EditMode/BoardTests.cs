using System;
using System.Collections.Generic;
using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class BoardTests
    {
        static readonly object[] Sizes =
        {
            new object[] { 13, 3 }, new object[] { 13, 4 }, new object[] { 13, 0 },
            new object[] { 9, 2 }, new object[] { 5, 1 }, new object[] { 7, 0 }, new object[] { 15, 5 },
        };

        [TestCase(13, 3, 145)]
        [TestCase(13, 0, 169)]
        [TestCase(9, 2, 69)]
        [TestCase(5, 1, 21)]
        public void CellCount_IsSquareMinusFourCorners(int size, int cut, int expected)
        {
            var board = new Board(size, cut);

            Assert.AreEqual(expected, board.Cells.Count());
            Assert.AreEqual(expected - 9, board.Cells.Count(board.IsWalkable));
        }

        [Test]
        public void Corners_AreCutAt45Degrees()
        {
            var board = new Board(13, 3);
            var rowWidths = Enumerable.Range(-6, 13)
                .Select(y => board.Cells.Count(c => c.Y == y))
                .ToArray();

            CollectionAssert.AreEqual(new[] { 7, 9, 11, 13, 13, 13, 13, 13, 13, 13, 11, 9, 7 }, rowWidths);
        }

        [Test]
        public void BossArea_IsOnBoardButNotWalkable()
        {
            var board = new Board(13, 3);

            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            {
                var cell = new Cell(x, y);
                Assert.IsTrue(board.Contains(cell));
                Assert.IsTrue(board.IsBossCell(cell));
                Assert.IsFalse(board.IsWalkable(cell));
            }

            Assert.IsFalse(board.IsBossCell(new Cell(2, 0)));
            Assert.IsTrue(board.IsWalkable(new Cell(2, 0)));
        }

        [TestCase(12, 3)]
        [TestCase(3, 0)]
        [TestCase(13, -1)]
        [TestCase(13, 6)]
        public void InvalidDimensions_Throw(int size, int cut)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Board(size, cut));
        }

        [Test]
        public void Ring_AtDistanceThree_MatchesOctagon()
        {
            var ring = new Board(13, 3).Ring(3);

            Assert.AreEqual(16, ring.Count);
            CollectionAssert.IsSubsetOf(new[] { new Cell(3, 1), new Cell(2, 2), new Cell(1, 3) }, ring);
            CollectionAssert.DoesNotContain(ring, new Cell(3, 2));
        }

        [TestCaseSource(nameof(Sizes))]
        public void EveryRing_IsAClosedLoopOfWalkableCells(int size, int cut)
        {
            var board = new Board(size, cut);

            for (var distance = Board.MinRing; distance <= board.MaxRing; distance++)
            {
                var ring = board.Ring(distance);

                Assert.AreEqual(ring.Count, new HashSet<Cell>(ring).Count, $"ring {distance} repeats a cell");
                for (var i = 0; i < ring.Count; i++)
                {
                    var from = ring[i];
                    var to = ring[(i + 1) % ring.Count];
                    Assert.IsTrue(board.IsWalkable(from), $"{from} is not walkable");
                    Assert.AreEqual(1, Math.Max(Math.Abs(from.X - to.X), Math.Abs(from.Y - to.Y)), $"{from} -> {to}");
                    Assert.Greater(from.X * to.Y - from.Y * to.X, 0, $"{from} -> {to} is not counterclockwise");
                }
            }
        }

        [TestCaseSource(nameof(Sizes))]
        public void Sidestep_ReachesRingFromEveryCell_AndLoopsAroundBoss(int size, int cut)
        {
            var board = new Board(size, cut);

            foreach (var start in board.Cells.Where(board.IsWalkable))
            foreach (var clockwise in new[] { true, false })
            {
                var distance = board.RingOf(start);
                var ring = board.Ring(distance);
                var first = board.Sidestep(start, clockwise);
                Assert.Contains(first, (System.Collections.ICollection)ring, $"{start} did not join ring {distance}");

                var current = first;
                for (var i = 0; i < ring.Count; i++)
                {
                    var next = board.Sidestep(current, clockwise);
                    Assert.IsTrue(board.IsWalkable(next));
                    Assert.AreEqual(distance, board.RingOf(next));
                    var cross = current.X * next.Y - current.Y * next.X;
                    Assert.AreEqual(clockwise, cross < 0, $"{current} -> {next} turned the wrong way");
                    current = next;
                }

                Assert.AreEqual(first, current, $"loop from {start} did not close");
            }
        }
    }
}
