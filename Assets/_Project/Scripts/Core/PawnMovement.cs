using System;

namespace ChessBoss.Core
{
    public enum PawnMove
    {
        Forward,
        Back,
        Left,
        Right,
    }

    public sealed class PawnMovement
    {
        readonly Board _board;

        public Board Board => _board;
        public Cell Position { get; private set; }

        public PawnMovement(Board board, Cell start)
        {
            if (!board.IsWalkable(start))
                throw new ArgumentException($"Start cell {start} is not walkable.", nameof(start));

            _board = board;
            Position = start;
        }

        public bool TryMove(PawnMove move)
        {
            var target = TargetOf(move);
            if (!_board.IsWalkable(target)) return false;

            Position = target;
            return true;
        }

        /// <summary>Ends a jump at the cell, or at the nearest free one if it is blocked. Warned cells count as free.</summary>
        public void Land(Cell target) => Position = _board.NearestWalkable(target);

        Cell TargetOf(PawnMove move)
        {
            switch (move)
            {
                case PawnMove.Forward: return _board.Step(Position, 0, -1);
                case PawnMove.Back: return _board.Step(Position, 0, 1);
                case PawnMove.Left: return _board.Step(Position, -1, 0);
                case PawnMove.Right: return _board.Step(Position, 1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(move), move, null);
            }
        }
    }
}
