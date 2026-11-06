using System.Linq;
using ChessBoss.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChessBoss.Game
{
    /// <summary>Feeds input to the Core movement rules and animates the result as a hop between cells.</summary>
    public sealed class PawnController : MonoBehaviour
    {
        [SerializeField] BoardView _board;
        [SerializeField] Renderer _body;
        [SerializeField] BossView _boss;

        readonly InputAction[] _moves = new InputAction[4];
        InputAction _attackLeft;
        InputAction _attackRight;
        InputAction _awakenRook;
        PawnMovement _movement;
        PawnCombat _combat;
        PawnActions _actions;
        Tween _hop;
        Vector3[] _hopPath;
        AwakenPiece? _shownPiece;
        PawnMove? _lastPressed;

        public PawnMovement Movement => _movement;
        public PawnCombat Combat => _combat;
        public PlayerVitals Vitals { get; private set; }
        public Awakening Awakening { get; private set; }
        public AwakenedAttacks AwakenedAttacks { get; private set; }
        public PawnAttackResult? LastAttack { get; private set; }
        public AwakenedStrike LastStrike { get; private set; }

        void Awake()
        {
            var tuning = _board.Tuning;
            var start = new Cell(tuning.startCell.x, tuning.startCell.y);
            _movement = new PawnMovement(_board.Board, start);
            Vitals = new PlayerVitals(tuning.playerHealth, tuning.invulnerableTime, tuning.burnDuration, tuning.burnTickTime, tuning.burnTickDamage);
            transform.position = _board.CellToWorld(start);
            _board.Paint(_body, tuning.pawnColor);

            var actions = InputSystem.actions.FindActionMap("Battle", true);
            actions.Enable();
            _moves[(int)PawnMove.Forward] = actions.FindAction("Forward", true);
            _moves[(int)PawnMove.Back] = actions.FindAction("Back", true);
            _moves[(int)PawnMove.Left] = actions.FindAction("Left", true);
            _moves[(int)PawnMove.Right] = actions.FindAction("Right", true);
            _attackLeft = actions.FindAction("AttackLeft", true);
            _attackRight = actions.FindAction("AttackRight", true);
            _awakenRook = actions.FindAction("AwakenRook", true);

            _combat = new PawnCombat(_movement, _boss.Health, tuning.attackDamage);
            _actions = new PawnActions(_movement, _combat, tuning.moveCooldown, tuning.attackCooldown);
            Awakening = new Awakening(tuning.awakenChargeTime, tuning.awakenInputWindow);
            AwakenedAttacks = new AwakenedAttacks(_movement, _boss.Health, Vitals, _actions, Awakening, new AwakenSettings
            {
                BaseDamage = tuning.awakenBaseDamage,
                DamagePerRing = tuning.awakenDamagePerRing,
                AttackCooldown = tuning.attackCooldown,
                CooldownPerRing = tuning.awakenCooldownPerRing,
                RookRingHitDamage = tuning.rookRingHitDamage,
                LandingInvulnerable = tuning.landingInvulnerable,
            });
        }

        void Start() => RefreshAttackMarks();

        void Update()
        {
            _actions.Tick(Time.deltaTime);
            Awakening.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            var alive = !Vitals.Health.IsDead;
            var fighting = alive && !_boss.Health.IsDead;
            if (!fighting) Awakening.End();
            else if (_awakenRook.WasPressedThisFrame()) Awakening.TryAwaken(AwakenPiece.Rook);

            if (Awakening.Active != null) ReadAwakenedAttack();
            else if (alive) ReadPawnInput();

            ShowAwakening();
            _boss.SetHittable(alive && Awakening.Active == null && _combat.CanHitBoss);

            if (_hop.Running)
                AnimateHop(_hop.Tick(Time.deltaTime));
        }

        void ReadPawnInput()
        {
            if (_attackLeft.WasPressedThisFrame()) Attack(PawnAttackSide.Left);
            if (_attackRight.WasPressedThisFrame()) Attack(PawnAttackSide.Right);

            var move = HeldMove();
            if (move != null) TryStartHop(move.Value);
        }

        // A fresh press only, so a key still held from walking doesn't fire the one attack by accident.
        void ReadAwakenedAttack()
        {
            for (var i = 0; i < _moves.Length; i++)
            {
                if (!_moves[i].WasPressedThisFrame()) continue;

                var strike = AwakenedAttacks.Rook((PawnMove)i);
                if (strike == null) return;

                LastStrike = strike;
                if (strike.Succeeded)
                {
                    StartHop(strike.Path.Select(_board.CellToWorld).ToArray(), _board.Tuning.awakenTravelTime);
                    _boss.PlayHit();
                }

                return;
            }
        }

        void ShowAwakening()
        {
            if (_shownPiece == Awakening.Active) return;

            _shownPiece = Awakening.Active;
            var tuning = _board.Tuning;
            _board.Paint(_body, _shownPiece == null ? tuning.pawnColor : tuning.awakenedColor);
            BattleTime.SlowMotion = _shownPiece == null ? 1 : tuning.awakenTimeScale;
        }

        void OnDisable() => BattleTime.SlowMotion = 1;

        // The key pressed last wins while it is down. Nothing is remembered past its release, so there is no buffer.
        PawnMove? HeldMove()
        {
            for (var i = 0; i < _moves.Length; i++)
                if (_moves[i].WasPressedThisFrame())
                    _lastPressed = (PawnMove)i;

            if (_lastPressed != null)
            {
                var last = _moves[(int)_lastPressed.Value];
                if (last.IsPressed() || last.WasPressedThisFrame()) return _lastPressed;
            }

            _lastPressed = null;
            for (var i = 0; i < _moves.Length; i++)
                if (_moves[i].IsPressed())
                    return _lastPressed = (PawnMove)i;

            return null;
        }

        void TryStartHop(PawnMove move)
        {
            var from = _movement.Position;
            if (!_actions.TryMove(move)) return;

            StartHop(new[] { _board.CellToWorld(from), _board.CellToWorld(_movement.Position) }, _board.Tuning.moveDuration);
        }

        void StartHop(Vector3[] path, float duration)
        {
            _hopPath = path;
            _hop.Start(duration);
            RefreshAttackMarks();
        }

        void Attack(PawnAttackSide side)
        {
            var result = _actions.TryAttack(side);
            if (result == PawnAttackResult.OnCooldown) return;

            LastAttack = result;
            if (result == PawnAttackResult.Hit) _boss.PlayHit();
        }

        void RefreshAttackMarks()
        {
            _board.Marks.SetAttackable(_combat.AttackableCells);
            _board.RefreshMarks();
        }

        void AnimateHop(float progress)
        {
            var tuning = _board.Tuning;
            var arc = Mathf.Sin(progress * Mathf.PI);
            var along = progress * (_hopPath.Length - 1);
            var segment = Mathf.Min((int)along, _hopPath.Length - 2);
            transform.position = Vector3.Lerp(_hopPath[segment], _hopPath[segment + 1], along - segment) + Vector3.up * (arc * tuning.jumpHeight);

            // Stretches on the way up and squashes into the landing, and ends at rest scale.
            var height = 1 + tuning.squash * Mathf.Sin(progress * 2 * Mathf.PI);
            var width = 1 / Mathf.Sqrt(height);
            transform.localScale = new Vector3(width, height, width);
        }
    }
}
