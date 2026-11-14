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
    /// <summary>The whole loop in the real scenes: title, fight, pause, result, and back again.</summary>
    public class FlowPlayTests
    {
        PawnController _pawn;
        BossView _boss;
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

            yield return Load("Battle");
        }

        [TearDown]
        public void Release()
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            BattleTime.Paused = false;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
        }

        IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            Find();
        }

        void Find()
        {
            _pawn = Object.FindAnyObjectByType<PawnController>();
            _boss = Object.FindAnyObjectByType<BossView>();
            _tuning = Object.FindAnyObjectByType<BoardView>()?.Tuning;
        }

        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        static IEnumerator Click(string button)
        {
            GameObject.Find(button).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Escape_PausesAndResumes_AndIgnoresGameKeysMeanwhile()
        {
            var start = _pawn.Movement.Position;

            yield return Press(Key.Escape);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsNotNull(GameObject.Find("PausePanel"));

            yield return Press(Key.W);
            Assert.AreEqual(start, _pawn.Movement.Position);

            yield return Press(Key.Escape);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNull(GameObject.Find("PausePanel"));

            yield return Press(Key.W);
            Assert.AreNotEqual(start, _pawn.Movement.Position);
        }

        [UnityTest]
        public IEnumerator ResumeButton_ContinuesTheFight()
        {
            yield return Press(Key.Escape);

            yield return Click("ResumeButton");

            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNull(GameObject.Find("PausePanel"));
        }

        [UnityTest]
        public IEnumerator Pause_HoldsTheAwakeningWindow_AndSlowMotionComesBack()
        {
            _pawn.Awakening.Tick(_tuning.awakenChargeTime, 0);
            yield return Press(Key.Digit1);
            var window = _pawn.Awakening.WindowLeft;

            yield return Press(Key.Escape);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(window, _pawn.Awakening.WindowLeft, 0.1f);

            yield return Press(Key.Escape);
            Assert.AreEqual(AwakenPiece.Rook, _pawn.Awakening.Active);
            Assert.AreEqual(_tuning.awakenTimeScale, Time.timeScale, 1e-4f);
        }

        [UnityTest]
        public IEnumerator ScreenEffects_RunThroughHitstop_ButStandStillWhilePaused()
        {
            var screen = _pawn.Effects.Screen;
            screen.Flash();
            BattleTime.Stopped = true;
            yield return null;
            var shown = screen.FlashAlpha;
            Assert.Greater(shown, 0f);

            yield return new WaitForSecondsRealtime(0.1f);
            Assert.Less(screen.FlashAlpha, shown, "a hitstop doesn't hold the screen");
            BattleTime.Stopped = false;

            yield return Press(Key.Escape);
            shown = screen.FlashAlpha;
            Assert.Greater(shown, 0f);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(shown, screen.FlashAlpha);

            yield return Press(Key.Escape);
            yield return new WaitForSecondsRealtime(_tuning.hurtFlashTime);
            Assert.AreEqual(0f, screen.FlashAlpha);
        }

        [UnityTest]
        public IEnumerator Retry_LeavesNoScreenEffectBehind()
        {
            var screen = _pawn.Effects.Screen;
            screen.Flash();
            screen.Fire(_tuning.screenFireStrong);
            screen.LowHealth = true;
            yield return null;
            Assert.Greater(screen.FireAlpha, 0f);
            Assert.Greater(screen.LowHealthAlpha, 0f);
            yield return Press(Key.Escape);

            yield return Click("RetryButton");
            Find();
            yield return null;

            Assert.AreEqual(1, Object.FindObjectsByType<ScreenEffects>().Length);
            screen = _pawn.Effects.Screen;
            Assert.AreEqual(0f, screen.FlashAlpha);
            Assert.AreEqual(0f, screen.FireAlpha);
            Assert.AreEqual(0f, screen.LowHealthAlpha);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator RetryFromPause_StartsAFreshFight_AtNormalSpeed()
        {
            _boss.Health.TakeDamage(10);
            yield return Press(Key.Escape);

            yield return Click("RetryButton");
            Find();

            Assert.AreEqual(_tuning.bossHealth, _boss.Health.Current);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNull(GameObject.Find("PausePanel"));
        }

        [UnityTest]
        public IEnumerator Victory_ShowsTheClearTimeAfterTheBossFalls_ThenRetryWorks()
        {
            yield return new WaitForSecondsRealtime(0.1f);
            _boss.Health.TakeDamage(_boss.Health.Max);
            yield return null;
            Assert.IsNull(GameObject.Find("VictoryPanel"), "the boss falls first");

            yield return new WaitForSecondsRealtime(_tuning.battleResultDelay + 0.1f);
            Assert.IsNotNull(GameObject.Find("VictoryPanel"));
            Assert.AreEqual(0f, Time.timeScale);
            StringAssert.Contains("0:00.", GameObject.Find("ClearTime").GetComponent<UnityEngine.UI.Text>().text);

            yield return Press(Key.Escape);
            Assert.IsNotNull(GameObject.Find("VictoryPanel"), "escape can't leave the result");

            yield return Click("RetryButton");
            Find();
            Assert.AreEqual(_tuning.bossHealth, _boss.Health.Current);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Defeat_ShowsTheDefeatScreen_ThenTitleAndStartReturnToAFight()
        {
            _pawn.Vitals.Health.TakeDamage(_pawn.Vitals.Health.Max);

            yield return new WaitForSecondsRealtime(_tuning.battleResultDelay + 0.1f);
            Assert.IsNotNull(GameObject.Find("DefeatPanel"));
            Assert.IsNull(GameObject.Find("ClearTime"));

            yield return Click("TitleButton");
            Assert.AreEqual("Boot", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale);

            yield return Click("StartButton");
            Assert.AreEqual("Battle", SceneManager.GetActiveScene().name);
            Find();
            Assert.AreEqual(_pawn.Vitals.Health.Max, _pawn.Vitals.Health.Current);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator EveryScreen_HasOneEventSystem_SoItsButtonsCanBeClicked()
        {
            yield return Press(Key.Escape);
            Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length);
            Assert.IsNotNull(Object.FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().actionsAsset);

            yield return Click("TitleButton");
            Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length);
        }
    }
}
