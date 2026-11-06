using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class AwakeningTests
    {
        const float Charge = 20f;
        const float Window = 3f;

        static Awakening Full()
        {
            var awakening = new Awakening(Charge, Window);
            awakening.Tick(Charge, Charge);
            return awakening;
        }

        [Test]
        public void Gauge_FillsOverTheChargeTime_AndOnlyThenAllowsAwakening()
        {
            var awakening = new Awakening(Charge, Window);

            awakening.Tick(Charge / 2, Charge / 2);
            Assert.AreEqual(0.5f, awakening.Gauge, 1e-5f);
            Assert.IsFalse(awakening.TryAwaken(AwakenPiece.Rook));
            Assert.IsNull(awakening.Active);

            awakening.Tick(Charge, Charge);
            Assert.AreEqual(1f, awakening.Gauge, 1e-5f);
            Assert.IsTrue(awakening.TryAwaken(AwakenPiece.Rook));
            Assert.AreEqual(AwakenPiece.Rook, awakening.Active);
        }

        [Test]
        public void Awakening_EmptiesTheGauge_AndItDoesNotChargeUntilTheEnd()
        {
            var awakening = Full();

            awakening.TryAwaken(AwakenPiece.Rook);
            Assert.AreEqual(0f, awakening.Gauge);

            awakening.Tick(1, 1);
            Assert.AreEqual(0f, awakening.Gauge);

            awakening.End();
            awakening.Tick(1, 1);
            Assert.AreEqual(1 / Charge, awakening.Gauge, 1e-5f);
        }

        [Test]
        public void InputWindow_RunsOnRealTime_ThenCancelsWithoutRefund()
        {
            var awakening = Full();
            awakening.TryAwaken(AwakenPiece.Rook);

            awakening.Tick(100, Window - 0.1f);
            Assert.AreEqual(AwakenPiece.Rook, awakening.Active);

            awakening.Tick(0, 0.2f);
            Assert.IsNull(awakening.Active);
            Assert.Less(awakening.Gauge, 0.01f);
        }

        [Test]
        public void GaugeCharges_OnGameTime_SoSlowMotionSlowsIt()
        {
            var awakening = new Awakening(Charge, Window);

            awakening.Tick(0, Charge);

            Assert.AreEqual(0f, awakening.Gauge);
        }
    }

    public class AwakenedAttacksTests
    {
        const int Rings = 8;
        const int Sectors = 24;
        const int BaseDamage = 4;
        const int PerRing = 1;
        const float BaseCooldown = 0.4f;
        const float CooldownPerRing = 0.1f;
        const float Landing = 0.5f;

        Board _board;
        PawnMovement _pawn;
        Health _boss;
        PlayerVitals _vitals;
        PawnActions _actions;
        Awakening _awakening;
        AwakenedAttacks _attacks;

        void Setup(int x, int y, bool awaken = true)
        {
            _board = new Board(Sectors, Rings);
            _pawn = new PawnMovement(_board, new Cell(x, y));
            _boss = new Health(100);
            _vitals = PlayerVitalsTests.Vitals();
            _actions = new PawnActions(_pawn, new PawnCombat(_pawn, _boss, 2), 0.2f, BaseCooldown);
            _awakening = new Awakening(20, 3);
            _attacks = new AwakenedAttacks(_pawn, _boss, _vitals, _actions, _awakening, new AwakenSettings
            {
                BaseDamage = BaseDamage,
                DamagePerRing = PerRing,
                AttackCooldown = BaseCooldown,
                CooldownPerRing = CooldownPerRing,
                RookRingHitDamage = 1,
                LandingInvulnerable = Landing,
            });
            _awakening.Tick(20, 20);
            if (awaken) _awakening.TryAwaken(AwakenPiece.Rook);
        }

        void AssertCooldown(float expected)
        {
            _actions.Tick(expected - 0.001f);
            Assert.IsFalse(_actions.IsReady);
            _actions.Tick(0.002f);
            Assert.IsTrue(_actions.IsReady);
        }

        [TestCase(1, 0)]
        [TestCase(4, 3)]
        [TestCase(7, 6)]
        public void RookForward_StopsBeforeTheBoss_WithDamageAndCooldownByRingsMoved(int ring, int moved)
        {
            Setup(18, ring);

            var strike = _attacks.Rook(PawnMove.Forward);

            Assert.IsTrue(strike.Succeeded);
            Assert.AreEqual(new Cell(18, 1), _pawn.Position);
            Assert.AreEqual(moved, strike.RingsMoved);
            Assert.AreEqual(100 - BaseDamage - moved * PerRing, _boss.Current);
            AssertCooldown(BaseCooldown + moved * CooldownPerRing);
        }

        [TestCase(1, 12)]
        [TestCase(4, 9)]
        [TestCase(7, 6)]
        public void RookBack_TurnsAtTheOuterEdge_AndCountsTheWayBack(int ring, int moved)
        {
            Setup(18, ring);

            var strike = _attacks.Rook(PawnMove.Back);

            Assert.AreEqual(new Cell(18, Rings - 1), strike.Path.First(cell => cell.Y == Rings - 1));
            Assert.AreEqual(new Cell(18, ring), strike.Path[0]);
            Assert.AreEqual(new Cell(18, 1), strike.Path[strike.Path.Count - 1]);
            Assert.AreEqual(new Cell(18, 1), _pawn.Position);
            Assert.AreEqual(moved, strike.RingsMoved);
            Assert.AreEqual(100 - BaseDamage - moved * PerRing, _boss.Current);
            AssertCooldown(BaseCooldown + moved * CooldownPerRing);
        }

        [Test]
        public void RookLine_PiercesWalls()
        {
            Setup(18, 5);
            _board.SetWalls(new[] { new Cell(18, 3) });

            var strike = _attacks.Rook(PawnMove.Forward);

            Assert.IsTrue(strike.Succeeded);
            Assert.AreEqual(new Cell(18, 1), _pawn.Position);
        }

        [TestCase(PawnMove.Left, -1)]
        [TestCase(PawnMove.Right, 1)]
        public void RookSideways_OnTheBossRing_CirclesOnce_HittingPerCell_AndReturns(PawnMove move, int direction)
        {
            Setup(0, 1);

            var strike = _attacks.Rook(move);

            Assert.IsTrue(strike.Succeeded);
            Assert.AreEqual(new Cell(0, 1), _pawn.Position);
            Assert.AreEqual(Sectors + 1, strike.Path.Count);
            Assert.AreEqual(strike.Path[0], strike.Path[Sectors]);
            Assert.AreEqual(_board.Step(new Cell(0, 1), direction, 0), strike.Path[1]);
            Assert.IsTrue(strike.Path.All(cell => cell.Y == 1));
            Assert.AreEqual(Sectors, strike.Path.Distinct().Count());
            Assert.AreEqual(100 - Sectors, _boss.Current);
            AssertCooldown(BaseCooldown);
        }

        [TestCase(PawnMove.Left, 2)]
        [TestCase(PawnMove.Right, 7)]
        public void RookSideways_OffTheBossRing_Fails_AndStillSpendsTheAwakening(PawnMove move, int ring)
        {
            Setup(18, ring);

            var strike = _attacks.Rook(move);

            Assert.IsFalse(strike.Succeeded);
            Assert.AreEqual(100, _boss.Current);
            Assert.AreEqual(new Cell(18, ring), _pawn.Position);
            Assert.IsNull(_awakening.Active);
            Assert.AreEqual(0f, _awakening.Gauge);
            Assert.IsFalse(_vitals.IsInvulnerable);
            AssertCooldown(BaseCooldown);
        }

        [Test]
        public void Strike_EndsTheAwakening_AndGrantsLandingInvulnerability()
        {
            Setup(18, 4);

            _attacks.Rook(PawnMove.Forward);

            Assert.IsNull(_awakening.Active);
            Assert.IsTrue(_vitals.IsInvulnerable);
            _vitals.Tick(Landing - 0.01f);
            Assert.IsTrue(_vitals.IsInvulnerable);
            _vitals.Tick(0.02f);
            Assert.IsFalse(_vitals.IsInvulnerable);
        }

        [Test]
        public void Rook_DoesNothing_WithoutARookAwakening()
        {
            Setup(18, 4, awaken: false);

            Assert.IsNull(_attacks.Rook(PawnMove.Forward));
            Assert.AreEqual(100, _boss.Current);
            Assert.AreEqual(new Cell(18, 4), _pawn.Position);

            _awakening.TryAwaken(AwakenPiece.Knight);
            Assert.IsNull(_attacks.Rook(PawnMove.Forward));
            Assert.AreEqual(AwakenPiece.Knight, _awakening.Active);
        }

        [Test]
        public void Rook_WaitsForTheSharedCooldown_KeepingTheAwakening()
        {
            Setup(18, 4);
            _actions.TryMove(PawnMove.Left);

            Assert.IsNull(_attacks.Rook(PawnMove.Forward));
            Assert.AreEqual(AwakenPiece.Rook, _awakening.Active);

            _actions.Tick(1);
            Assert.IsNotNull(_attacks.Rook(PawnMove.Forward));
        }

        [Test]
        public void HitWhileWaiting_CancelsWithoutRefund_ButABlockedHitOrBurnTickDoesNot()
        {
            Setup(18, 4);

            _vitals.Hit(0, true);
            Assert.IsNull(_awakening.Active);
            Assert.AreEqual(0f, _awakening.Gauge);

            Setup(18, 4, awaken: false);
            _vitals.Hit(0, true);
            _awakening.Tick(20, 20);
            _awakening.TryAwaken(AwakenPiece.Rook);

            Assert.IsFalse(_vitals.Hit(0, false), "still invulnerable");
            Assert.AreEqual(AwakenPiece.Rook, _awakening.Active, "blocked hit");

            _vitals.Tick(1.1f);
            Assert.AreEqual(AwakenPiece.Rook, _awakening.Active, "burn tick");
        }

        [Test]
        public void Landing_OnAWall_IsPushedToTheNearestFreeCell()
        {
            var board = new Board(Sectors, Rings);
            var pawn = new PawnMovement(board, new Cell(18, 4));
            board.SetWalls(new[] { new Cell(5, 3) });

            pawn.Land(new Cell(5, 3));

            Assert.AreEqual(1, board.HopsFrom(new Cell(5, 3), new Cell[0])[pawn.Position]);
            Assert.IsTrue(board.IsWalkable(pawn.Position));
        }

        [Test]
        public void Landing_OnAFreeOrWarnedCell_StaysThere()
        {
            var board = new Board(Sectors, Rings);
            var pawn = new PawnMovement(board, new Cell(18, 4));

            pawn.Land(new Cell(5, 3));

            Assert.AreEqual(new Cell(5, 3), pawn.Position);
        }
    }
}
