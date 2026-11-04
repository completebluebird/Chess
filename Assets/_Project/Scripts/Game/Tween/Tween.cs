using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>A timer that reports progress from 0 to 1. Callers decide what the progress drives.</summary>
    public struct Tween
    {
        float _duration;
        float _elapsed;

        public bool Running { get; private set; }

        public void Start(float duration)
        {
            _duration = duration;
            _elapsed = 0;
            Running = true;
        }

        public float Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            var progress = _duration > 0 ? Mathf.Clamp01(_elapsed / _duration) : 1;
            Running = progress < 1;
            return progress;
        }
    }
}
