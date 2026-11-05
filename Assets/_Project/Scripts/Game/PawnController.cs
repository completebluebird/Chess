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
        PawnMovement _movement;
        PawnCombat _combat;
        PawnActions _actions;
        Tween _hop;
        Vector3 _hopFrom;
        Vector3 _hopTo;
        PawnMove? _lastPressed;

        public PawnMovement Movement => _movement;
        public PawnCombat Combat => _combat;
        public PawnAttackResult? LastAttack { get; private set; }

        void Awake()
        {
            var tuning = _board.Tuning;
            var start = new Cell(tuning.startCell.x, tuning.startCell.y);
            _movement = new PawnMovement(_board.Board, start);
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

            _combat = new PawnCombat(_movement, _boss.Health, tuning.attackDamage);
            _actions = new PawnActions(_movement, _combat, tuning.moveCooldown, tuning.attackCooldown);
        }

        void Start() => RefreshAttackMarks();

        void Update()
        {
            _actions.Tick(Time.deltaTime);
            if (_attackLeft.WasPressedThisFrame()) Attack(PawnAttackSide.Left);
            if (_attackRight.WasPressedThisFrame()) Attack(PawnAttackSide.Right);
            _boss.SetHittable(_combat.CanHitBoss);

            var move = HeldMove();
            if (move != null) TryStartHop(move.Value);

            if (_hop.Running)
                AnimateHop(_hop.Tick(Time.deltaTime));
        }

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

            _hopFrom = _board.CellToWorld(from);
            _hopTo = _board.CellToWorld(_movement.Position);
            _hop.Start(_board.Tuning.moveDuration);
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
            transform.position = Vector3.Lerp(_hopFrom, _hopTo, progress) + Vector3.up * (arc * tuning.jumpHeight);

            // Stretches on the way up and squashes into the landing, and ends at rest scale.
            var height = 1 + tuning.squash * Mathf.Sin(progress * 2 * Mathf.PI);
            var width = 1 / Mathf.Sqrt(height);
            transform.localScale = new Vector3(width, height, width);
        }
    }
}
