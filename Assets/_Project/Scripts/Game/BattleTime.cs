using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>The one owner of Time.timeScale, so a hitstop ends in slow motion when slow motion is on.</summary>
    public static class BattleTime
    {
        static float _slowMotion = 1;
        static bool _stopped;

        public static float SlowMotion
        {
            set
            {
                _slowMotion = value;
                Apply();
            }
        }

        public static bool Stopped
        {
            set
            {
                _stopped = value;
                Apply();
            }
        }

        static void Apply() => Time.timeScale = _stopped ? 0 : _slowMotion;
    }
}
