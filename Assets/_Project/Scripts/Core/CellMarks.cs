using System.Collections.Generic;

namespace ChessBoss.Core
{
    public enum CellMark
    {
        None,
        Attackable,
        Warning,
    }

    /// <summary>What each floor cell tells the player. A warning always wins over an attackable mark.</summary>
    public sealed class CellMarks
    {
        readonly HashSet<Cell> _attackable = new HashSet<Cell>();
        readonly HashSet<Cell> _warning = new HashSet<Cell>();

        public void SetAttackable(IEnumerable<Cell> cells) => Replace(_attackable, cells);

        public void SetWarning(IEnumerable<Cell> cells) => Replace(_warning, cells);

        public CellMark MarkOf(Cell cell)
        {
            if (_warning.Contains(cell)) return CellMark.Warning;
            return _attackable.Contains(cell) ? CellMark.Attackable : CellMark.None;
        }

        static void Replace(HashSet<Cell> target, IEnumerable<Cell> cells)
        {
            target.Clear();
            target.UnionWith(cells);
        }
    }
}
