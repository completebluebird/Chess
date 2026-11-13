namespace ChessBoss.Core
{
    /// <summary>Turns two keys pressed close together into a diagonal. Fed only while a bishop waits, with real time.</summary>
    public sealed class BishopInput
    {
        readonly float _buffer;
        PawnMove? _first;
        float _firstLeft;

        public BishopInput(float buffer) => _buffer = buffer;

        public void Tick(float realDeltaTime)
        {
            _firstLeft -= realDeltaTime;
            if (_firstLeft <= 0) _first = null;
        }

        /// <summary>Forgets a key waiting for a partner, so it can't pair with one pressed in a later awakening.</summary>
        public void Clear() => _first = null;

        /// <summary>The diagonal this key completes, or null while it only waits for a partner.</summary>
        public BishopMove? Press(PawnMove key)
        {
            var diagonal = _first == null ? null : Diagonal(_first.Value, key);
            if (diagonal != null)
            {
                _first = null;
                return diagonal;
            }

            _first = key;
            _firstLeft = _buffer;
            return null;
        }

        static BishopMove? Diagonal(PawnMove a, PawnMove b)
        {
            if (IsSideways(a) == IsSideways(b)) return null;

            var ring = IsSideways(a) ? b : a;
            var side = IsSideways(a) ? a : b;
            if (ring == PawnMove.Forward) return side == PawnMove.Right ? BishopMove.ForwardRight : BishopMove.ForwardLeft;
            return side == PawnMove.Right ? BishopMove.BackRight : BishopMove.BackLeft;
        }

        static bool IsSideways(PawnMove key) => key == PawnMove.Left || key == PawnMove.Right;
    }
}
