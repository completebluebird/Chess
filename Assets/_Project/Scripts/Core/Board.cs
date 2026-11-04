using System;
using System.Collections.Generic;

namespace ChessBoss.Core
{
    /// <summary>Octagonal board centered on (0, 0), with the boss on the middle 3x3 cells.</summary>
    public sealed class Board
    {
        public const int MinRing = 2;
        const int BossHalf = 1;

        readonly int _half;
        readonly int _sumLimit;
        readonly Cell[][] _rings;

        public Board(int size, int cornerCut)
        {
            if (size % 2 == 0 || size < 2 * MinRing + 1)
                throw new ArgumentOutOfRangeException(nameof(size), size, "Board size must be odd and at least 5.");
            _half = size / 2;
            if (cornerCut < 0 || cornerCut >= _half)
                throw new ArgumentOutOfRangeException(nameof(cornerCut), cornerCut, "Corner cut must be in [0, size / 2).");

            _sumLimit = 2 * _half - cornerCut;
            _rings = new Cell[_half + 1][];
            for (var distance = MinRing; distance <= _half; distance++)
                _rings[distance] = BuildRing(distance);
        }

        public int MaxRing => _half;

        public IEnumerable<Cell> Cells
        {
            get
            {
                for (var y = -_half; y <= _half; y++)
                for (var x = -_half; x <= _half; x++)
                {
                    var cell = new Cell(x, y);
                    if (Contains(cell)) yield return cell;
                }
            }
        }

        public bool Contains(Cell cell) =>
            Math.Abs(cell.X) <= _half && Math.Abs(cell.Y) <= _half && Math.Abs(cell.X) + Math.Abs(cell.Y) <= _sumLimit;

        public bool IsBossCell(Cell cell) => Math.Abs(cell.X) <= BossHalf && Math.Abs(cell.Y) <= BossHalf;

        public bool IsWalkable(Cell cell) => Contains(cell) && !IsBossCell(cell);

        /// <summary>The smallest octagon around the boss that contains the cell.</summary>
        public int RingOf(Cell cell)
        {
            var longest = Math.Max(Math.Abs(cell.X), Math.Abs(cell.Y));
            var sum = Math.Abs(cell.X) + Math.Abs(cell.Y);
            var distance = Math.Max(MinRing, longest);
            while (sum > RingSumLimit(distance)) distance++;
            return distance;
        }

        /// <summary>The octagon's outline in counterclockwise order; neighbors are at most one diagonal step apart.</summary>
        public IReadOnlyList<Cell> Ring(int distance) => _rings[distance];

        /// <summary>The next cell around the boss at the same distance.</summary>
        public Cell Sidestep(Cell from, bool clockwise)
        {
            var ring = _rings[RingOf(from)];
            var index = Array.IndexOf(ring, from);
            if (index >= 0)
                return ring[(index + (clockwise ? ring.Length - 1 : 1)) % ring.Length];

            // Octagons grow unevenly, so some cells sit between two outlines and join the outline ahead.
            foreach (var cell in ring)
            {
                var isAdjacent = Math.Abs(cell.X - from.X) <= 1 && Math.Abs(cell.Y - from.Y) <= 1;
                var cross = from.X * cell.Y - from.Y * cell.X;
                if (isAdjacent && (clockwise ? cross < 0 : cross > 0)) return cell;
            }

            throw new InvalidOperationException($"No sidestep from {from}.");
        }

        // At least distance + 1 keeps the smallest ring off the boss's corner cells.
        int RingSumLimit(int distance) => Math.Max(distance + 1, distance * _sumLimit / _half);

        Cell[] BuildRing(int distance)
        {
            var straight = RingSumLimit(distance) - distance;
            var diagonal = distance - straight;
            var legs = new[]
            {
                (new Cell(0, 1), 2 * straight), (new Cell(-1, 1), diagonal),
                (new Cell(-1, 0), 2 * straight), (new Cell(-1, -1), diagonal),
                (new Cell(0, -1), 2 * straight), (new Cell(1, -1), diagonal),
                (new Cell(1, 0), 2 * straight), (new Cell(1, 1), diagonal),
            };

            var ring = new List<Cell>();
            var current = new Cell(distance, -straight);
            foreach (var (step, count) in legs)
            for (var i = 0; i < count; i++)
            {
                ring.Add(current);
                current += step;
            }

            return ring.ToArray();
        }
    }
}
