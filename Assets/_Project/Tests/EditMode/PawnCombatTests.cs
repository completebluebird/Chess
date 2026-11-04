using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class PawnCombatTests
    {
        const int Damage = 2;
        const float Cooldown = 0.3f;

        readonly Board _board = new Board(13, 3);

        PawnCombat Combat(int x, int y, out Health boss)
        {
            boss = new Health(100);
            return new PawnCombat(new PawnMovement(_board, new Cell(x, y), 2), boss, Damage, Cooldown);
        }

        [Test]
        public void AttackCells_AreFrontDiagonalsRelativeToForward()
        {
            var combat = Combat(0, -5, out _);

            Assert.AreEqual(new Cell(-1, -4), combat.AttackCell(PawnAttackSide.Left));
            Assert.AreEqual(new Cell(1, -4), combat.AttackCell(PawnAttackSide.Right));
        }

        [Test]
        public void AttackCells_FollowForwardAroundTheBoss()
        {
            var combat = Combat(5, 0, out _);

            Assert.AreEqual(new Cell(4, -1), combat.AttackCell(PawnAttackSide.Left));
            Assert.AreEqual(new Cell(4, 1), combat.AttackCell(PawnAttackSide.Right));
        }

        [Test]
        public void Attack_HitsOnlyWhenDiagonalIsBossCell_FromEveryCell()
        {
            foreach (var cell in _board.Cells.Where(_board.IsWalkable))
            foreach (var side in new[] { PawnAttackSide.Left, PawnAttackSide.Right })
            {
                var combat = Combat(cell.X, cell.Y, out var boss);
                var expectHit = _board.IsBossCell(combat.AttackCell(side));

                var result = combat.TryAttack(side);

                Assert.AreEqual(expectHit ? PawnAttackResult.Hit : PawnAttackResult.Miss, result, $"{cell} {side}");
                Assert.AreEqual(expectHit ? 98 : 100, boss.Current, $"{cell} {side}");
            }
        }

        [Test]
        public void FrontOfBoss_BothDiagonalsCanHit()
        {
            var combat = Combat(0, -2, out _);

            Assert.IsTrue(_board.IsBossCell(combat.AttackCell(PawnAttackSide.Left)));
            Assert.IsTrue(_board.IsBossCell(combat.AttackCell(PawnAttackSide.Right)));
        }

        [Test]
        public void Attack_IsBlockedByCooldown_AfterHitOrMiss()
        {
            var hit = Combat(0, -2, out var boss);
            Assert.AreEqual(PawnAttackResult.Hit, hit.TryAttack(PawnAttackSide.Left));
            Assert.AreEqual(PawnAttackResult.OnCooldown, hit.TryAttack(PawnAttackSide.Right));
            Assert.AreEqual(98, boss.Current);

            hit.Tick(Cooldown / 2);
            Assert.AreEqual(PawnAttackResult.OnCooldown, hit.TryAttack(PawnAttackSide.Left));
            hit.Tick(Cooldown / 2);
            Assert.AreEqual(PawnAttackResult.Hit, hit.TryAttack(PawnAttackSide.Left));

            var miss = Combat(0, -5, out _);
            Assert.AreEqual(PawnAttackResult.Miss, miss.TryAttack(PawnAttackSide.Left));
            Assert.AreEqual(PawnAttackResult.OnCooldown, miss.TryAttack(PawnAttackSide.Left));
        }

        [Test]
        public void Attack_DoesNotHitDeadBoss()
        {
            var combat = Combat(0, -2, out var boss);
            boss.TakeDamage(boss.Max);

            Assert.AreEqual(PawnAttackResult.Miss, combat.TryAttack(PawnAttackSide.Left));
        }

        [Test]
        public void RepeatedHits_BringBossToZero()
        {
            var combat = Combat(0, -2, out var boss);

            for (var i = 0; i < boss.Max / Damage; i++)
            {
                Assert.AreEqual(PawnAttackResult.Hit, combat.TryAttack(PawnAttackSide.Left));
                combat.Tick(Cooldown);
            }

            Assert.AreEqual(0, boss.Current);
            Assert.IsTrue(boss.IsDead);
        }
    }

    public class HealthTests
    {
        [Test]
        public void Damage_ReducesAndClampsAtZero()
        {
            var health = new Health(5);

            health.TakeDamage(3);
            Assert.AreEqual(2, health.Current);
            Assert.IsFalse(health.IsDead);

            health.TakeDamage(3);
            Assert.AreEqual(0, health.Current);
            Assert.IsTrue(health.IsDead);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveMax_Throws(int max)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Health(max));
        }

        [Test]
        public void NegativeDamage_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Health(5).TakeDamage(-1));
        }
    }

    public class CellMarksTests
    {
        static readonly Cell A = new Cell(2, 0);
        static readonly Cell B = new Cell(3, 0);

        [Test]
        public void UnmarkedCell_IsNone()
        {
            Assert.AreEqual(CellMark.None, new CellMarks().MarkOf(A));
        }

        [Test]
        public void SetAttackable_ReplacesPreviousCells()
        {
            var marks = new CellMarks();
            marks.SetAttackable(new[] { A });
            marks.SetAttackable(new[] { B });

            Assert.AreEqual(CellMark.None, marks.MarkOf(A));
            Assert.AreEqual(CellMark.Attackable, marks.MarkOf(B));
        }

        [Test]
        public void Warning_TakesPriorityOverAttackable_InEitherOrder()
        {
            var marks = new CellMarks();
            marks.SetAttackable(new[] { A });
            marks.SetWarning(new[] { A, B });
            Assert.AreEqual(CellMark.Warning, marks.MarkOf(A));

            marks.SetAttackable(new[] { B });
            Assert.AreEqual(CellMark.Warning, marks.MarkOf(B));

            marks.SetWarning(new Cell[0]);
            Assert.AreEqual(CellMark.Attackable, marks.MarkOf(B));
        }
    }
}
