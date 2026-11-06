using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class BishopTests
    {
        const int Rings = 8;
        const int BaseDamage = 4;
        const float BaseCooldown = 0.4f;
        const float CooldownPerRing = 0.1f;

        Board _board;
        PawnMovement _pawn;
        Health _boss;
        PlayerVitals _vitals;
        PawnActions _actions;
        Awakening _awakening;
        AwakenedAttacks _attacks;

        void Setup(int x, int y, AwakenPiece piece = AwakenPiece.Bishop)
        {
            _board = new Board(24, Rings);
            _pawn = new PawnMovement(_board, new Cell(x, y));
            _boss = new Health(100);
            _vitals = PlayerVitalsTests.Vitals();
            _actions = new PawnActions(_pawn, new PawnCombat(_pawn, _boss, 2), 0.2f, BaseCooldown);
            _awakening = new Awakening(20, 3);
            _attacks = new AwakenedAttacks(_pawn, _boss, _vitals, _actions, _awakening, new AwakenSettings
            {
                BaseDamage = BaseDamage,
                DamagePerRing = 1,
                AttackCooldown = BaseCooldown,
                CooldownPerRing = CooldownPerRing,
                RookRingHitDamage = 1,
                LandingInvulnerable = 0.5f,
            });
            _awakening.Tick(20, 20);
            _awakening.TryAwaken(piece);
        }

        // The examples agreed for the round board, from docs/design/phase1.md.
        [TestCase(BishopMove.ForwardRight, 3, 21, 23)]
        [TestCase(BishopMove.ForwardLeft, 3, 15, 15)]
        [TestCase(BishopMove.BackRight, 9, 3, 5)]
        [TestCase(BishopMove.BackLeft, 9, 9, 9)]
        public void Bishop_FromSector18Ring4_LandsOnTheAgreedCell(BishopMove move, int moved, int arrivalSector, int landingSector)
        {
            Setup(18, 4);

            var strike = _attacks.Bishop(move);

            Assert.IsTrue(strike.Succeeded);
            Assert.AreEqual(moved, strike.RingsMoved);
            Assert.AreEqual(new Cell(arrivalSector, 1), strike.Path[moved]);
            Assert.AreEqual(new Cell(landingSector, 1), _pawn.Position);
            Assert.AreEqual(100 - BaseDamage - moved, _boss.Current);
            _actions.Tick(BaseCooldown + moved * CooldownPerRing - 0.001f);
            Assert.IsFalse(_actions.IsReady);
            _actions.Tick(0.002f);
            Assert.IsTrue(_actions.IsReady);
        }

        [Test]
        public void BishopBack_RunsOutToTheEdge_ThenTurnsBackTheSameWayAround()
        {
            Setup(18, 4);

            var strike = _attacks.Bishop(BishopMove.BackRight);

            CollectionAssert.AreEqual(new[]
            {
                new Cell(18, 4), new Cell(19, 5), new Cell(20, 6), new Cell(21, 7),
                new Cell(22, 6), new Cell(23, 5), new Cell(0, 4), new Cell(1, 3), new Cell(2, 2), new Cell(3, 1),
            }, strike.Path.Take(10));
        }

        [Test]
        public void Bishop_FromTheRingBeforeTheBoss_MovesNoRings()
        {
            Setup(18, 1);

            var strike = _attacks.Bishop(BishopMove.ForwardRight);

            Assert.AreEqual(0, strike.RingsMoved);
            Assert.AreEqual(new Cell(20, 1), _pawn.Position);
            Assert.AreEqual(100 - BaseDamage, _boss.Current);
        }

        [Test]
        public void Bishop_FromTheOuterRing_TurnsAtOnce()
        {
            Setup(18, Rings - 1);

            var strike = _attacks.Bishop(BishopMove.BackRight);

            Assert.AreEqual(Rings - 2, strike.RingsMoved);
            Assert.AreEqual(new Cell(19, Rings - 2), strike.Path[1]);
        }

        [Test]
        public void Bishop_PiercesWalls_AndWrapsTheSeam()
        {
            Setup(22, 4);
            _board.SetWalls(new[] { new Cell(23, 3) });

            var strike = _attacks.Bishop(BishopMove.ForwardRight);

            Assert.AreEqual(new Cell(23, 3), strike.Path[1]);
            Assert.AreEqual(new Cell(3, 1), _pawn.Position);
        }

        [Test]
        public void Bishop_EndsTheAwakening_AndGrantsLandingInvulnerability()
        {
            Setup(18, 4);

            _attacks.Bishop(BishopMove.ForwardLeft);

            Assert.IsNull(_awakening.Active);
            Assert.IsTrue(_vitals.IsInvulnerable);
        }

        [Test]
        public void Bishop_DoesNothing_WithoutABishopAwakening()
        {
            Setup(18, 4, AwakenPiece.Rook);

            Assert.IsNull(_attacks.Bishop(BishopMove.ForwardRight));

            Assert.AreEqual(100, _boss.Current);
            Assert.AreEqual(AwakenPiece.Rook, _awakening.Active);
        }

        [Test]
        public void Bishop_WaitsForTheSharedCooldown_KeepingTheAwakening()
        {
            Setup(18, 4);
            _actions.TryMove(PawnMove.Left);

            Assert.IsNull(_attacks.Bishop(BishopMove.ForwardRight));
            Assert.AreEqual(AwakenPiece.Bishop, _awakening.Active);
        }
    }

    public class BishopInputTests
    {
        const float Buffer = 0.1f;

        [TestCase(PawnMove.Forward, PawnMove.Right, BishopMove.ForwardRight)]
        [TestCase(PawnMove.Right, PawnMove.Forward, BishopMove.ForwardRight)]
        [TestCase(PawnMove.Forward, PawnMove.Left, BishopMove.ForwardLeft)]
        [TestCase(PawnMove.Left, PawnMove.Forward, BishopMove.ForwardLeft)]
        [TestCase(PawnMove.Back, PawnMove.Right, BishopMove.BackRight)]
        [TestCase(PawnMove.Right, PawnMove.Back, BishopMove.BackRight)]
        [TestCase(PawnMove.Back, PawnMove.Left, BishopMove.BackLeft)]
        [TestCase(PawnMove.Left, PawnMove.Back, BishopMove.BackLeft)]
        public void TwoPerpendicularKeys_InEitherOrder_MakeADiagonal(PawnMove first, PawnMove second, BishopMove expected)
        {
            var input = new BishopInput(Buffer);

            Assert.IsNull(input.Press(first));
            input.Tick(Buffer * 0.9f);

            Assert.AreEqual(expected, input.Press(second));
        }

        [Test]
        public void SecondKey_AfterTheBuffer_StartsOver()
        {
            var input = new BishopInput(Buffer);

            input.Press(PawnMove.Forward);
            input.Tick(Buffer + 0.01f);

            Assert.IsNull(input.Press(PawnMove.Right));
            Assert.AreEqual(BishopMove.ForwardRight, input.Press(PawnMove.Forward));
        }

        [TestCase(PawnMove.Forward, PawnMove.Back)]
        [TestCase(PawnMove.Left, PawnMove.Right)]
        [TestCase(PawnMove.Forward, PawnMove.Forward)]
        public void KeysOnTheSameAxis_MakeNoDiagonal_AndTheNewestCounts(PawnMove first, PawnMove second)
        {
            var input = new BishopInput(Buffer);

            input.Press(first);

            Assert.IsNull(input.Press(second));
            var across = second == PawnMove.Left || second == PawnMove.Right ? PawnMove.Forward : PawnMove.Right;
            Assert.IsNotNull(input.Press(across));
        }

        [Test]
        public void ADiagonal_UsesUpBothKeys()
        {
            var input = new BishopInput(Buffer);

            input.Press(PawnMove.Forward);
            input.Press(PawnMove.Right);

            Assert.IsNull(input.Press(PawnMove.Left));
        }
    }
}
