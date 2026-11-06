using System;

namespace ChessBoss.Core
{
    public enum AwakenPiece
    {
        Rook,
        Knight,
        Bishop,
    }

    /// <summary>The gauge that charges with game time, and the awakened piece waiting for its one attack.</summary>
    public sealed class Awakening
    {
        readonly float _chargeTime;
        readonly float _inputWindow;
        float _charged;
        float _windowLeft;

        public AwakenPiece? Active { get; private set; }
        public float Gauge => Math.Min(1, _charged / _chargeTime);
        public float WindowLeft => _windowLeft;

        public Awakening(float chargeTime, float inputWindow)
        {
            if (chargeTime <= 0)
                throw new ArgumentOutOfRangeException(nameof(chargeTime), chargeTime, "The gauge needs time to charge.");

            _chargeTime = chargeTime;
            _inputWindow = inputWindow;
        }

        /// <summary>The input window runs on real time, so slow motion doesn't stretch it.</summary>
        public void Tick(float gameDeltaTime, float realDeltaTime)
        {
            if (Active == null)
            {
                _charged = Math.Min(_chargeTime, _charged + gameDeltaTime);
                return;
            }

            _windowLeft -= realDeltaTime;
            if (_windowLeft <= 0) End();
        }

        public bool TryAwaken(AwakenPiece piece)
        {
            if (Active != null || _charged < _chargeTime) return false;

            Active = piece;
            _charged = 0;
            _windowLeft = _inputWindow;
            return true;
        }

        /// <summary>Ends however it ended: used, timed out or cancelled. The gauge is never refunded.</summary>
        public void End() => Active = null;
    }
}
