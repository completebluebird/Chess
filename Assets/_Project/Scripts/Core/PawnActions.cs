using System;

namespace ChessBoss.Core
{
    /// <summary>
    /// Moving and attacking are one kind of action sharing one cooldown, whose length depends on the action.
    /// It runs on the time given to Tick, so it slows with the game, and only Tick shortens it.
    /// </summary>
    public sealed class PawnActions
    {
        readonly PawnMovement _movement;
        readonly PawnCombat _combat;
        readonly float _moveCooldown;
        readonly float _attackCooldown;
        float _remaining;

        public PawnActions(PawnMovement movement, PawnCombat combat, float moveCooldown, float attackCooldown)
        {
            _movement = movement;
            _combat = combat;
            _moveCooldown = moveCooldown;
            _attackCooldown = attackCooldown;
        }

        public bool IsReady => _remaining <= 0;

        public void Tick(float deltaTime) => _remaining = Math.Max(0, _remaining - deltaTime);

        /// <summary>A move the board refuses costs nothing, so holding a key against an edge doesn't lock the pawn.</summary>
        public bool TryMove(PawnMove move)
        {
            if (!IsReady || !_movement.TryMove(move)) return false;

            _remaining = _moveCooldown;
            return true;
        }

        public PawnAttackResult TryAttack(PawnAttackSide side)
        {
            if (!IsReady) return PawnAttackResult.OnCooldown;

            _remaining = _attackCooldown;
            return _combat.Attack(side);
        }
    }
}
