using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessBoss.Game
{
    public sealed class TitleScreen : MonoBehaviour
    {
        void Start()
        {
            var panel = new MenuPanel("TitlePanel", "체스 보스전");
            panel.AddButton("StartButton", "시작", () => SceneManager.LoadScene(BattleFlow.BattleScene));
            panel.AddButton("QuitButton", "종료", Application.Quit);
        }
    }
}
