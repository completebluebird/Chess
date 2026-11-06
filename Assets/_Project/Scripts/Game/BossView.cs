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
        bool _stunned;
        Transform _head;
        float _fallenFor;

        Color RestColor => _stunned ? _board.Tuning.bossStunColor : _board.Tuning.bossColor;

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
            if (!hittable && _flashRemaining <= 0) _board.Paint(_body, RestColor);
        }

        public void SetStunned(bool stunned)
        {
            _stunned = stunned;
            if (_flashRemaining <= 0) _board.Paint(_body, RestColor);
        }

        public void PlayHit()
        {
            var tuning = _board.Tuning;
            _board.Paint(_body, tuning.bossFlashColor);
            _flashRemaining = tuning.bossFlashDuration;
            _hitstopRemaining = tuning.hitstopDuration;
            BattleTime.Stopped = true;
        }

        void Update()
        {
            if (_hitstopRemaining > 0)
            {
                _hitstopRemaining -= Time.unscaledDeltaTime;
                if (_hitstopRemaining <= 0) BattleTime.Stopped = false;
            }

            if (Health.IsDead) Fall();

            if (_flashRemaining > 0)
            {
                _flashRemaining -= Time.unscaledDeltaTime;
                if (_flashRemaining <= 0) _board.Paint(_body, RestColor);
            }
            else if (_hittable)
            {
                var tuning = _board.Tuning;
                var blink = Mathf.PingPong(Time.unscaledTime * 2 / tuning.bossBlinkPeriod, 1);
                _board.Paint(_body, Color.Lerp(RestColor, tuning.attackableColor, blink));
            }
        }

        // shortcut: sinks into the floor instead of a defeat animation, the dragon's own replaces it in M9.
        void Fall()
        {
            var tuning = _board.Tuning;
            if (_fallenFor >= tuning.battleResultDelay) return;

            _fallenFor += Time.unscaledDeltaTime;
            var down = Vector3.down * ((tuning.bossHeight + tuning.bossHeadSize.y) * Time.unscaledDeltaTime / tuning.battleResultDelay);
            _body.transform.position += down;
            _head.position += down;
        }

        void OnDisable()
        {
            if (_hitstopRemaining > 0) BattleTime.Stopped = false;
        }
    }
}
