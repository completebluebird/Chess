using ChessBoss.Core;
using UnityEngine;

namespace ChessBoss.Game
{
    public sealed class BossView : MonoBehaviour
    {
        [SerializeField] BoardView _board;
        [SerializeField] Renderer _body;

        Health _health;
        float _flashRemaining;
        float _hitstopRemaining;
        bool _hittable;

        public Health Health => _health ??= new Health(_board.Tuning.bossHealth);

        public void SetHittable(bool hittable)
        {
            if (_hittable == hittable) return;

            _hittable = hittable;
            if (!hittable && _flashRemaining <= 0) _board.Paint(_body, _board.Tuning.bossColor);
        }

        // shortcut: hitstop assumes the normal time scale is 1, move to shared time control when M5 adds slow motion.
        public void PlayHit()
        {
            var tuning = _board.Tuning;
            _board.Paint(_body, tuning.bossFlashColor);
            _flashRemaining = tuning.bossFlashDuration;
            _hitstopRemaining = tuning.hitstopDuration;
            Time.timeScale = 0;
        }

        void Update()
        {
            if (_hitstopRemaining > 0)
            {
                _hitstopRemaining -= Time.unscaledDeltaTime;
                if (_hitstopRemaining <= 0) Time.timeScale = 1;
            }

            if (_flashRemaining > 0)
            {
                _flashRemaining -= Time.unscaledDeltaTime;
                if (_flashRemaining <= 0) _board.Paint(_body, _board.Tuning.bossColor);
            }
            else if (_hittable)
            {
                var tuning = _board.Tuning;
                var blink = Mathf.PingPong(Time.unscaledTime * 2 / tuning.bossBlinkPeriod, 1);
                _board.Paint(_body, Color.Lerp(tuning.bossColor, tuning.attackableColor, blink));
            }
        }

        void OnDisable()
        {
            if (_hitstopRemaining > 0) Time.timeScale = 1;
        }
    }
}
