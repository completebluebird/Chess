using System;
using Unity.Cinemachine;
using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>
    /// Where the fight's one-off effects start. One lives in each Battle scene, added at run time so the scene file
    /// doesn't change, and it is not static so a reloaded scene can't keep hold of destroyed objects.
    /// </summary>
    public sealed class BattleEffects : MonoBehaviour
    {
        public ScreenEffects Screen { get; private set; }
        public CameraEffects Camera { get; private set; }

        /// <summary>A name from SoundCues, each time its moment happens.</summary>
        public event Action<string> SoundCued;

        public void Init(BoardView board, PawnController pawn, BossView boss)
        {
            Screen = new GameObject("ScreenEffects").AddComponent<ScreenEffects>();
            Screen.Init(board.Tuning);
            Camera = FindAnyObjectByType<CinemachineCamera>().gameObject.AddComponent<CameraEffects>();
            Camera.Init(board, pawn, boss);
        }

        public void Sound(string cue) => SoundCued?.Invoke(cue);
    }
}
