using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>The one owner of Time.timeScale, so a hitstop ends in slow motion when slow motion is on.</summary>
    public static class BattleTime
    {
        static float _slowMotion = 1;
        static float _stunSlowMotion = 1;
        static bool _stopped;
        static bool _paused;

        public static float SlowMotion
        {
            set
            {
                _slowMotion = value;
                Apply();
            }
        }

        public static float StunSlowMotion
        {
            set
            {
                _stunSlowMotion = value;
                Apply();
            }
        }

        public static bool IsPaused => _paused;

        public static bool Paused
        {
            set
            {
                _paused = value;
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

        static void Apply() => Time.timeScale = _stopped || _paused ? 0 : Mathf.Min(_slowMotion, _stunSlowMotion);
    }
}
