using System.Collections;
using ChessBoss.Core;
using ChessBoss.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChessBoss.Tests
{
    /// <summary>Plays the real Battle scene with real key events, which the Core tests can't reach. Each test ends before the boss's first attack.</summary>
    public class AwakeningPlayTests
    {
        PawnController _pawn;
        BossView _boss;
        BoardView _board;
        BattleTuning _tuning;
        InputSettings.BackgroundBehavior _backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;

        [UnitySetUp]
        public IEnumerator LoadBattle()
        {
            // Without these, the keys below are dropped whenever the Editor window isn't in front.
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            SceneManager.LoadScene("Battle");
            yield return null;
            yield return null;
            _pawn = Object.FindAnyObjectByType<PawnController>();
            _boss = Object.FindAnyObjectByType<BossView>();
            _board = Object.FindAnyObjectByType<BoardView>();
            _tuning = _board.Tuning;
        }

        [TearDown]
        public void Release()
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            BattleTime.Stopped = false;
            BattleTime.SlowMotion = 1;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
        }

        IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        void FillGauge() => _pawn.Awakening.Tick(_tuning.awakenChargeTime, 0);

        [UnityTest]
        public IEnumerator Key1_WithoutAFullGauge_LeavesAPawnThatStillWalks()
        {
            var start = _pawn.Movement.Position;

            yield return Press(Key.Digit1);
            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(1f, Time.timeScale);

            yield return Press(Key.W);
            Assert.AreEqual(new Cell(start.X, start.Y - 1), _pawn.Movement.Position);
        }

        [UnityTest]
        public IEnumerator RookForward_FromKeysToLanding()
        {
            var start = _pawn.Movement.Position;
            var moved = start.Y - 1;
            FillGauge();

            yield return Press(Key.Digit1);
            Assert.AreEqual(AwakenPiece.Rook, _pawn.Awakening.Active);
            Assert.AreEqual(_tuning.awakenTimeScale, Time.timeScale, 1e-4f);

            yield return Press(Key.W);
            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(new Cell(start.X, 1), _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth - _tuning.awakenBaseDamage - moved * _tuning.awakenDamagePerRing, _boss.Health.Current);
            Assert.IsTrue(_pawn.Vitals.IsInvulnerable);

            yield return new WaitForSecondsRealtime(_tuning.hitstopDuration + _tuning.awakenTravelTime / _tuning.awakenTimeScale + 0.1f);
            Assert.AreEqual(1f, Time.timeScale, "the hitstop must not leave time stopped or slowed");
            Assert.That(Vector3.Distance(_board.CellToWorld(_pawn.Movement.Position), _pawn.transform.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator RookSideways_OffTheBossRing_FailsWithoutMoving_AndSpendsTheGauge()
        {
            var start = _pawn.Movement.Position;
            FillGauge();
            yield return Press(Key.Digit1);

            yield return Press(Key.A);

            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(start, _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth, _boss.Health.Current);
            Assert.AreEqual(0f, _pawn.Awakening.Gauge, 0.05f);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator BishopForwardRight_FromKeysToLanding()
        {
            var start = _pawn.Movement.Position;
            var moved = start.Y - 1;
            FillGauge();

            yield return Press(Key.Digit3);
            Assert.AreEqual(AwakenPiece.Bishop, _pawn.Awakening.Active);

            yield return Press(Key.W, Key.D);
            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(_pawn.Movement.Board.Step(new Cell(start.X, 1), moved + 2, 0), _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth - _tuning.awakenBaseDamage - moved * _tuning.awakenDamagePerRing, _boss.Health.Current);

            yield return new WaitForSecondsRealtime(_tuning.hitstopDuration + _tuning.awakenTravelTime / _tuning.awakenTimeScale + 0.1f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.That(Vector3.Distance(_board.CellToWorld(_pawn.Movement.Position), _pawn.transform.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Bishop_IgnoresASingleKey_AndStaysAwakened()
        {
            var start = _pawn.Movement.Position;
            FillGauge();
            yield return Press(Key.Digit3);

            yield return Press(Key.W);

            Assert.AreEqual(AwakenPiece.Bishop, _pawn.Awakening.Active);
            Assert.AreEqual(start, _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth, _boss.Health.Current);
        }

        [UnityTest]
        public IEnumerator Knight_ShowsCandidates_ThenJumps_FromKeysToLanding()
        {
            var board = _pawn.Movement.Board;
            var start = _pawn.Movement.Position;
            yield return Press(Key.W);
            yield return new WaitForSecondsRealtime(_tuning.moveCooldown + 0.05f);
            Assert.AreEqual(new Cell(start.X, start.Y - 1), _pawn.Movement.Position);
            FillGauge();

            yield return Press(Key.Digit2);
            Assert.AreEqual(AwakenPiece.Knight, _pawn.Awakening.Active);
            Assert.AreEqual(_tuning.awakenTimeScale, Time.timeScale, 1e-4f);
            var landing = board.Step(_pawn.Movement.Position, 1, -2);
            Assert.AreEqual(CellMark.None, _board.Marks.MarkOf(landing));

            yield return Press(Key.W);
            Assert.AreEqual(CellMark.Attackable, _board.Marks.MarkOf(landing));
            Assert.AreEqual(CellMark.Attackable, _board.Marks.MarkOf(board.Step(_pawn.Movement.Position, -1, -2)));

            yield return Press(Key.S);
            Assert.AreEqual(AwakenPiece.Knight, _pawn.Awakening.Active, "the opposite key is ignored");

            yield return Press(Key.D);
            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(landing, _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth - _tuning.knightDamage, _boss.Health.Current);
            Assert.AreEqual(CellMark.None, _board.Marks.MarkOf(board.Step(landing, 2, 0)));

            yield return new WaitForSecondsRealtime(_tuning.hitstopDuration + _tuning.awakenTravelTime / _tuning.awakenTimeScale + 0.1f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.That(Vector3.Distance(_board.CellToWorld(landing), _pawn.transform.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator PawnAttacks_DoNothing_WhileAwakened()
        {
            FillGauge();
            yield return Press(Key.Digit1);

            yield return Press(Key.Q);

            Assert.AreEqual(AwakenPiece.Rook, _pawn.Awakening.Active);
            Assert.IsNull(_pawn.LastAttack);
        }

        [UnityTest]
        public IEnumerator BeingHit_CancelsTheAwakening_AndEndsSlowMotion()
        {
            FillGauge();
            yield return Press(Key.Digit1);

            _pawn.Vitals.Hit(0, false);
            yield return null;

            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Hitstop_DuringSlowMotion_ReturnsToSlowMotion()
        {
            FillGauge();
            yield return Press(Key.Digit1);

            _boss.PlayHit();
            Assert.AreEqual(0f, Time.timeScale);

            yield return new WaitForSecondsRealtime(_tuning.hitstopDuration + 0.05f);
            Assert.AreEqual(_tuning.awakenTimeScale, Time.timeScale, 1e-4f);
        }
    }
}
