using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class KnightTests
    {
        const int Rings = 8;
        const int Damage = 6;
        const float Cooldown = 0.4f;

        Board _board;
        PawnMovement _pawn;
        Health _boss;
        PlayerVitals _vitals;
        PawnActions _actions;
        Awakening _awakening;
        AwakenedAttacks _attacks;

        void Setup(int x, int y, AwakenPiece piece = AwakenPiece.Knight)
        {
            _board = new Board(24, Rings);
            _pawn = new PawnMovement(_board, new Cell(x, y));
            _boss = new Health(100);
            _vitals = PlayerVitalsTests.Vitals();
            _actions = new PawnActions(_pawn, new PawnCombat(_pawn, _boss, 2), 0.2f, Cooldown);
            _awakening = new Awakening(20, 3);
            _attacks = new AwakenedAttacks(_pawn, _boss, _vitals, _actions, _awakening, new AwakenSettings
            {
                BaseDamage = 4,
                DamagePerRing = 1,
                AttackCooldown = Cooldown,
                CooldownPerRing = 0.1f,
                RookRingHitDamage = 1,
                KnightDamage = Damage,
                KnightRadius = 1,
                LandingInvulnerable = 0.5f,
            });
            _awakening.Tick(20, 20);
            _awakening.TryAwaken(piece);
        }

        AwakenedStrike Jump(PawnMove first, PawnMove second)
        {
            Assert.IsNull(_attacks.Knight(first));
            return _attacks.Knight(second);
        }

        [TestCase(PawnMove.Forward, PawnMove.Right, 19, 2)]
        [TestCase(PawnMove.Forward, PawnMove.Left, 17, 2)]
        [TestCase(PawnMove.Back, PawnMove.Right, 19, 6)]
        [TestCase(PawnMove.Back, PawnMove.Left, 17, 6)]
        [TestCase(PawnMove.Right, PawnMove.Forward, 20, 3)]
        [TestCase(PawnMove.Right, PawnMove.Back, 20, 5)]
        [TestCase(PawnMove.Left, PawnMove.Forward, 16, 3)]
        [TestCase(PawnMove.Left, PawnMove.Back, 16, 5)]
        public void EveryDirection_TwoCellsByTheFirstKey_OneByTheSecond(PawnMove first, PawnMove second, int x, int y)
        {
            Setup(18, 4);

            var strike = Jump(first, second);

            Assert.IsTrue(strike.Succeeded);
            Assert.AreEqual(new Cell(x, y), _pawn.Position);
            Assert.IsNull(_awakening.Active);
        }

        [TestCase(PawnMove.Forward, PawnMove.Back)]
        [TestCase(PawnMove.Forward, PawnMove.Forward)]
        [TestCase(PawnMove.Left, PawnMove.Right)]
        [TestCase(PawnMove.Right, PawnMove.Right)]
        [TestCase(PawnMove.Back, PawnMove.Forward)]
        public void SecondKeyOnTheSameAxis_IsIgnored_KeepingTheFirst(PawnMove first, PawnMove second)
        {
            Setup(18, 4);
            var candidates = (first == PawnMove.Left || first == PawnMove.Right)
                ? new[] { PawnMove.Forward }
                : new[] { PawnMove.Right };

            Assert.IsNull(Jump(first, second));
            Assert.AreEqual(AwakenPiece.Knight, _awakening.Active);
            Assert.AreEqual(new Cell(18, 4), _pawn.Position);

            Assert.IsNotNull(_attacks.Knight(candidates[0]));
        }

        [Test]
        public void FirstKey_ShowsItsTwoCandidates()
        {
            Setup(18, 4);
            CollectionAssert.IsEmpty(_attacks.KnightCandidates);

            _attacks.Knight(PawnMove.Forward);

            CollectionAssert.AreEquivalent(new[] { new Cell(17, 2), new Cell(19, 2) }, _attacks.KnightCandidates);
        }

        [Test]
        public void CandidatesWrapTheSeam()
        {
            Setup(0, 4);

            _attacks.Knight(PawnMove.Left);

            CollectionAssert.AreEquivalent(new[] { new Cell(22, 3), new Cell(22, 5) }, _attacks.KnightCandidates);
        }

        [Test]
        public void OffBoardCandidate_IsNotShown_AndCantBeChosen()
        {
            Setup(18, Rings - 1);

            _attacks.Knight(PawnMove.Left);
            CollectionAssert.AreEquivalent(new[] { new Cell(16, Rings - 2) }, _attacks.KnightCandidates);

            Assert.IsNull(_attacks.Knight(PawnMove.Back));
            Assert.AreEqual(AwakenPiece.Knight, _awakening.Active);

            Assert.IsNotNull(_attacks.Knight(PawnMove.Forward));
            Assert.AreEqual(new Cell(16, Rings - 2), _pawn.Position);
        }

        [TestCase(1, PawnMove.Forward)]
        [TestCase(Rings - 2, PawnMove.Back)]
        [TestCase(Rings - 1, PawnMove.Back)]
        public void FirstKey_WithNoCandidateLeft_IsIgnored_SoItCantLockThePiece(int ring, PawnMove first)
        {
            Setup(18, ring);

            _attacks.Knight(first);
            CollectionAssert.IsEmpty(_attacks.KnightCandidates);

            _attacks.Knight(PawnMove.Right);
            CollectionAssert.IsNotEmpty(_attacks.KnightCandidates);
        }

        [TestCase(2, PawnMove.Forward, PawnMove.Right, 19)]
        [TestCase(1, PawnMove.Right, PawnMove.Forward, 20)]
        public void LandingOnTheBoss_HitsIt_AndJumpsBack(int ring, PawnMove first, PawnMove second, int bossSector)
        {
            Setup(18, ring);

            var strike = Jump(first, second);

            Assert.AreEqual(Damage, strike.Damage);
            Assert.AreEqual(100 - Damage, _boss.Current);
            Assert.AreEqual(new Cell(18, ring), _pawn.Position);
            CollectionAssert.AreEqual(new[] { new Cell(18, ring), new Cell(bossSector, 0), new Cell(18, ring) }, strike.Path);
            Assert.IsTrue(_vitals.IsInvulnerable);
        }

        [Test]
        public void LandingNextToTheBoss_HitsIt_AndStaysThere()
        {
            Setup(18, 3);

            var strike = Jump(PawnMove.Forward, PawnMove.Right);

            Assert.AreEqual(Damage, strike.Damage);
            Assert.AreEqual(100 - Damage, _boss.Current);
            Assert.AreEqual(new Cell(19, 1), _pawn.Position);
        }

        [Test]
        public void LandingOutOfReach_JumpsThere_AndMisses()
        {
            Setup(18, 4);

            var strike = Jump(PawnMove.Forward, PawnMove.Right);

            Assert.IsTrue(strike.Succeeded);
            Assert.AreEqual(0, strike.Damage);
            Assert.AreEqual(100, _boss.Current);
            Assert.AreEqual(new Cell(19, 2), _pawn.Position);
            Assert.IsTrue(_vitals.IsInvulnerable);
            _actions.Tick(Cooldown - 0.001f);
            Assert.IsFalse(_actions.IsReady);
            _actions.Tick(0.002f);
            Assert.IsTrue(_actions.IsReady);
        }

        [Test]
        public void LandingOnAWall_IsPushedToTheNearestFreeCell_AndThePathEndsThere()
        {
            Setup(18, 4);
            _board.SetWalls(new[] { new Cell(19, 2) });

            var strike = Jump(PawnMove.Forward, PawnMove.Right);

            Assert.AreEqual(1, _board.HopsFrom(new Cell(19, 2), new Cell[0])[_pawn.Position]);
            Assert.AreEqual(_pawn.Position, strike.Path[strike.Path.Count - 1]);
        }

        [Test]
        public void CooldownBlocksTheJump_KeepingTheFirstKey()
        {
            Setup(18, 4);
            _actions.TryMove(PawnMove.Left);

            _attacks.Knight(PawnMove.Forward);
            Assert.IsNull(_attacks.Knight(PawnMove.Right));
            Assert.AreEqual(AwakenPiece.Knight, _awakening.Active);

            _actions.Tick(1);
            Assert.IsNotNull(_attacks.Knight(PawnMove.Right));
        }

        [Test]
        public void EndedAwakening_ForgetsTheFirstKey()
        {
            Setup(18, 4);
            _attacks.Knight(PawnMove.Forward);

            _vitals.Hit(0, false);
            CollectionAssert.IsEmpty(_attacks.KnightCandidates);

            _awakening.Tick(20, 0);
            _awakening.TryAwaken(AwakenPiece.Knight);
            _attacks.Knight(PawnMove.Right);

            CollectionAssert.AreEquivalent(new[] { new Cell(20, 3), new Cell(20, 5) }, _attacks.KnightCandidates);
        }

        [Test]
        public void Knight_DoesNothing_WithoutAKnightAwakening()
        {
            Setup(18, 4, AwakenPiece.Rook);

            Assert.IsNull(Jump(PawnMove.Forward, PawnMove.Right));

            Assert.AreEqual(new Cell(18, 4), _pawn.Position);
            CollectionAssert.IsEmpty(_attacks.KnightCandidates);
        }
    }
}
