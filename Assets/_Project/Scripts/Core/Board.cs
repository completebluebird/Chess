using System;
using System.Collections.Generic;

namespace ChessBoss.Core
{
    /// <summary>
    /// Round board around the boss, with a cell's X as its sector (wraps around the circle, counterclockwise)
    /// and its Y as its ring (0 is the boss's ring, growing outward).
    /// </summary>
    public sealed class Board
    {
        public const int BossRing = 0;

        readonly HashSet<Cell> _walls = new HashSet<Cell>();

        public int Sectors { get; }
        public int Rings { get; }

        public Board(int sectors, int rings)
        {
            if (sectors % 2 != 0 || sectors < 4)
                throw new ArgumentOutOfRangeException(nameof(sectors), sectors, "Sectors must be even and at least 4.");
            if (rings < 2)
                throw new ArgumentOutOfRangeException(nameof(rings), rings, "Rings must leave at least one ring outside the boss.");

            Sectors = sectors;
            Rings = rings;
        }

        public IEnumerable<Cell> Cells
        {
            get
            {
                for (var y = 0; y < Rings; y++)
                for (var x = 0; x < Sectors; x++)
                    yield return new Cell(x, y);
            }
        }

        public bool Contains(Cell cell) => cell.X >= 0 && cell.X < Sectors && cell.Y >= 0 && cell.Y < Rings;

        public bool IsBossCell(Cell cell) => Contains(cell) && cell.Y == BossRing;

        public bool IsWalkable(Cell cell) => Contains(cell) && !IsBossCell(cell) && !_walls.Contains(cell);

        public IReadOnlyCollection<Cell> Walls => _walls;

        public bool IsWall(Cell cell) => _walls.Contains(cell);

        public void SetWalls(IEnumerable<Cell> walls)
        {
            _walls.Clear();
            _walls.UnionWith(walls);
        }

        /// <summary>How many one-cell moves each reachable cell is from the start, going around the blocked cells.</summary>
        public Dictionary<Cell, int> HopsFrom(Cell start, ICollection<Cell> blocked)
        {
            var hops = new Dictionary<Cell, int> { [start] = 0 };
            var queue = new Queue<Cell>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var next in new[] { Step(cell, 1, 0), Step(cell, -1, 0), Step(cell, 0, 1), Step(cell, 0, -1) })
                {
                    if (!IsWalkable(next) || blocked.Contains(next) || hops.ContainsKey(next)) continue;
                    hops[next] = hops[cell] + 1;
                    queue.Enqueue(next);
                }
            }

            return hops;
        }

        /// <summary>The cell the given sectors and rings away. Sectors wrap; rings can leave the board.</summary>
        public Cell Step(Cell from, int sectors, int rings) =>
            new Cell(((from.X + sectors) % Sectors + Sectors) % Sectors, from.Y + rings);

        /// <summary>How many sectors apart two sectors are, going the shorter way around.</summary>
        public int SectorDistance(int a, int b)
        {
            var apart = Math.Abs(a - b);
            return Math.Min(apart, Sectors - apart);
        }
    }
}
