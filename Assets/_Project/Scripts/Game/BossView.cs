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
        Transform _head;

        public Health Health => _health ??= new Health(_board.Tuning.bossHealth);

        void Start()
        {
            var tuning = _board.Tuning;
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Boss head";
            Destroy(head.GetComponent<Collider>());
            _head = head.transform;
            _head.position = _body.transform.position + Vector3.up * ((tuning.bossHeight + tuning.bossHeadSize.y) / 2);
            _head.localScale = tuning.bossHeadSize;
            _board.Paint(head.GetComponent<Renderer>(), tuning.bossHeadColor);
        }

        /// <summary>Turns the body toward the pawn and, faster, the head toward what the boss is attacking.</summary>
        public void Face(int bodySector, int headSector)
        {
            if (Health.IsDead) return;

            var tuning = _board.Tuning;
            Turn(_body.transform, bodySector, tuning.bossTurnSpeed);
            Turn(_head, headSector, tuning.bossHeadTurnSpeed);
        }

        void Turn(Transform part, int sector, float degreesPerSecond)
        {
            part.rotation = Quaternion.RotateTowards(
                part.rotation, Quaternion.LookRotation(_board.SectorDirection(sector)), degreesPerSecond * Time.deltaTime);
        }

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
