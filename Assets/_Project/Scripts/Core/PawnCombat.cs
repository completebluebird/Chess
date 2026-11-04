using System;
using System.Collections.Generic;

namespace ChessBoss.Core
{
    public enum PawnAttackSide
    {
        Left,
        Right,
    }

    public enum PawnAttackResult
    {
        OnCooldown,
        Miss,
        Hit,
    }

    public sealed class PawnCombat
    {
        readonly PawnMovement _movement;
        readonly Health _boss;
        readonly int _damage;
        readonly float _cooldown;
        float _cooldownRemaining;

        public PawnCombat(PawnMovement movement, Health boss, int damage, float cooldown)
        {
            _movement = movement;
            _boss = boss;
            _damage = damage;
            _cooldown = cooldown;
        }

        public IEnumerable<Cell> AttackableCells
        {
            get
            {
                foreach (var side in new[] { PawnAttackSide.Left, PawnAttackSide.Right })
                {
                    var cell = AttackCell(side);
                    if (_movement.Board.Contains(cell)) yield return cell;
                }
            }
        }

        public Cell AttackCell(PawnAttackSide side)
        {
            var forward = _movement.Forward;
            var left = new Cell(-forward.Y, forward.X);
            return _movement.Position + forward + (side == PawnAttackSide.Left ? left : new Cell(-left.X, -left.Y));
        }

        public void Tick(float deltaTime) => _cooldownRemaining = Math.Max(0, _cooldownRemaining - deltaTime);

        public PawnAttackResult TryAttack(PawnAttackSide side)
        {
            if (_cooldownRemaining > 0) return PawnAttackResult.OnCooldown;

            _cooldownRemaining = _cooldown;
            if (_boss.IsDead || !_movement.Board.IsBossCell(AttackCell(side))) return PawnAttackResult.Miss;

            _boss.TakeDamage(_damage);
            return PawnAttackResult.Hit;
        }
    }
}
