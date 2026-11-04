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
        readonly int _hysteresis;

        public Cell Position { get; private set; }
        public Cell Forward { get; private set; }

        public PawnMovement(Board board, Cell start, int hysteresis)
        {
            if (!board.IsWalkable(start))
                throw new ArgumentException($"Start cell {start} is not walkable.", nameof(start));

            _board = board;
            _hysteresis = hysteresis;
            Position = start;
            Forward = SnapForward(start, default, hysteresis);
        }

        public bool TryMove(PawnMove move)
        {
            var target = TargetOf(move);
            if (!_board.IsWalkable(target)) return false;

            Position = target;
            Forward = SnapForward(target, Forward, _hysteresis);
            return true;
        }

        /// <summary>
        /// Picks the grid direction closest to the boss, keeping the previous one until the other axis
        /// leads by the hysteresis margin, so forward doesn't flip back and forth along diagonal edges.
        /// </summary>
        public static Cell SnapForward(Cell position, Cell previous, int hysteresis)
        {
            var toBossX = -position.X;
            var toBossY = -position.Y;
            var absX = Math.Abs(toBossX);
            var absY = Math.Abs(toBossY);

            if (previous.X * toBossX + previous.Y * toBossY > 0)
            {
                var otherAxisLead = previous.X != 0 ? absY - absX : absX - absY;
                if (otherAxisLead < hysteresis) return previous;
            }

            return absX >= absY ? new Cell(Math.Sign(toBossX), 0) : new Cell(0, Math.Sign(toBossY));
        }

        Cell TargetOf(PawnMove move)
        {
            switch (move)
            {
                case PawnMove.Forward: return Position + Forward;
                case PawnMove.Back: return Position - Forward;
                case PawnMove.Left: return _board.Sidestep(Position, clockwise: true);
                case PawnMove.Right: return _board.Sidestep(Position, clockwise: false);
                default: throw new ArgumentOutOfRangeException(nameof(move), move, null);
            }
        }
    }
}
