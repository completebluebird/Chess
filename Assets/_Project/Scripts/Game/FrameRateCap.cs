using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>
    /// Caps the frame rate at the display's refresh rate before the first scene loads.
    /// Every quality level has vSync off, so without a cap the editor's Game view renders thousands of frames a second
    /// and runs out of graphics ring buffer space.
    /// </summary>
    public static class FrameRateCap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            var refreshRate = Screen.currentResolution.refreshRateRatio.value;
            if (refreshRate <= 0) return;

            Application.targetFrameRate = Mathf.RoundToInt((float)refreshRate);
        }
    }
}
