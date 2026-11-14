using System;

namespace ChessBoss.Core
{
    public sealed class Health
    {
        public int Max { get; }
        public int Current { get; private set; }
        public bool IsDead => Current == 0;

        /// <summary>While set, damage does nothing: the boss in the air during its slam.</summary>
        public bool Untouchable { get; set; }

        public Health(int max)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max), max, "Max health must be positive.");
            Max = max;
            Current = max;
        }

        public void TakeDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "Damage cannot be negative.");
            if (Untouchable) return;

            Current = Math.Max(0, Current - amount);
        }
    }
}
