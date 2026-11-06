using System;

namespace ChessBoss.Core
{
    /// <summary>The player's health with the rules around it: invulnerability after a hit, and a burn that never stacks.</summary>
    public sealed class PlayerVitals
    {
        readonly float _invulnerableTime;
        readonly float _burnTickTime;
        readonly int _burnTickDamage;
        readonly int _burnTicks;
        float _invulnerableLeft;
        float _untilBurnTick;
        int _burnTicksLeft;

        public Health Health { get; }
        public bool IsInvulnerable => _invulnerableLeft > 0;
        public bool IsBurning => _burnTicksLeft > 0;

        /// <summary>A hit landed on the player. Blocked hits and burn ticks don't count.</summary>
        public event Action Hurt;

        public PlayerVitals(int maxHealth, float invulnerableTime, float burnDuration, float burnTickTime, int burnTickDamage)
        {
            if (burnTickTime <= 0)
                throw new ArgumentOutOfRangeException(nameof(burnTickTime), burnTickTime, "Burn ticks need time between them.");

            Health = new Health(maxHealth);
            _invulnerableTime = invulnerableTime;
            _burnTickTime = burnTickTime;
            _burnTickDamage = burnTickDamage;
            _burnTicks = (int)Math.Round(burnDuration / burnTickTime);
        }

        /// <summary>A blocked hit changes nothing, so it can't refresh the burn either. Returns whether the hit landed.</summary>
        public bool Hit(int damage, bool burns)
        {
            if (Health.IsDead || IsInvulnerable) return false;

            Health.TakeDamage(damage);
            _invulnerableLeft = _invulnerableTime;
            if (burns)
            {
                // Keeping the running tick timer is what stops repeated hits from speeding the burn up.
                if (!IsBurning) _untilBurnTick = _burnTickTime;
                _burnTicksLeft = _burnTicks;
            }

            Hurt?.Invoke();
            return true;
        }

        /// <summary>Overlapping invulnerability keeps only the longer one, so it can't chain into something endless.</summary>
        public void GrantInvulnerability(float seconds) => _invulnerableLeft = Math.Max(_invulnerableLeft, seconds);

        public void Tick(float deltaTime)
        {
            _invulnerableLeft = Math.Max(0, _invulnerableLeft - deltaTime);
            if (!IsBurning) return;

            _untilBurnTick -= deltaTime;
            while (IsBurning && _untilBurnTick <= 0)
            {
                Health.TakeDamage(_burnTickDamage);
                _burnTicksLeft--;
                _untilBurnTick += _burnTickTime;
            }
        }
    }
}
