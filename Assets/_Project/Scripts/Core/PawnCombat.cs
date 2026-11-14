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

        public PawnCombat(PawnMovement movement, Health boss, int damage)
        {
            _movement = movement;
            _boss = boss;
            _damage = damage;
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

        public bool CanHitBoss =>
            !_boss.IsDead && !_boss.Untouchable
            && (_movement.Board.IsBossCell(AttackCell(PawnAttackSide.Left))
                || _movement.Board.IsBossCell(AttackCell(PawnAttackSide.Right)));

        public Cell AttackCell(PawnAttackSide side) =>
            _movement.Board.Step(_movement.Position, side == PawnAttackSide.Left ? -1 : 1, -1);

        public PawnAttackResult Attack(PawnAttackSide side)
        {
            if (_boss.IsDead || _boss.Untouchable || !_movement.Board.IsBossCell(AttackCell(side))) return PawnAttackResult.Miss;

            _boss.TakeDamage(_damage);
            return PawnAttackResult.Hit;
        }
    }
}
