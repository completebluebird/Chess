using ChessBoss.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ChessBoss.Game
{
    /// <summary>Pauses the fight, and shows how it ended with the way back into another one.</summary>
    public sealed class BattleFlow : MonoBehaviour
    {
        public const string TitleScene = "Boot";
        public const string BattleScene = "Battle";

        [SerializeField] BoardView _board;
        [SerializeField] PawnController _pawn;
        [SerializeField] BossView _boss;

        InputAction _pause;
        BattleSession _session;
        MenuPanel _pausePanel;
        bool _resultShown;

        void Start()
        {
            _pause = InputSystem.actions.FindActionMap("Battle", true).FindAction("Pause", true);
            _session = new BattleSession(_boss.Health, _pawn.Vitals.Health, _board.Tuning.battleResultDelay);
        }

        void Update()
        {
            if (_resultShown) return;

            if (_pause.WasPressedThisFrame() && _session.Outcome == BattleOutcome.Fighting) TogglePause();
            if (_pausePanel != null) return;

            // Real time, so the clear time isn't stretched by slow motion or hitstop.
            _session.Tick(Time.unscaledDeltaTime);
            if (_session.ResultReady) ShowResult();
        }

        void OnDisable() => BattleTime.Paused = false;

        void TogglePause()
        {
            if (_pausePanel != null)
            {
                Resume();
                return;
            }

            BattleTime.Paused = true;
            _pausePanel = new MenuPanel("PausePanel", "일시정지");
            _pausePanel.AddButton("ResumeButton", "계속", Resume);
            AddWayOut(_pausePanel);
        }

        void Resume()
        {
            Destroy(_pausePanel.Root);
            _pausePanel = null;
            BattleTime.Paused = false;
        }

        void ShowResult()
        {
            _resultShown = true;
            BattleTime.Paused = true;
            var won = _session.Outcome == BattleOutcome.Victory;
            var panel = new MenuPanel(won ? "VictoryPanel" : "DefeatPanel", won ? "승리!" : "패배");
            if (won)
            {
                var elapsed = _session.Elapsed;
                panel.AddDetail("ClearTime", $"클리어 시간 {(int)(elapsed / 60)}:{elapsed % 60:00.00}");
            }

            AddWayOut(panel);
        }

        static void AddWayOut(MenuPanel panel)
        {
            panel.AddButton("RetryButton", "다시 하기", () => SceneManager.LoadScene(BattleScene));
            panel.AddButton("TitleButton", "타이틀", () => SceneManager.LoadScene(TitleScene));
        }
    }
}
