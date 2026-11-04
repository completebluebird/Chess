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
        Tween _hop;
        Vector3 _hopFrom;
        Vector3 _hopTo;
        PawnMove? _buffered;

        public PawnMovement Movement => _movement;
        public PawnCombat Combat => _combat;
        public PawnAttackResult? LastAttack { get; private set; }

        void Awake()
        {
            var tuning = _board.Tuning;
            var start = new Cell(tuning.startCell.x, tuning.startCell.y);
            _movement = new PawnMovement(_board.Board, start, tuning.forwardHysteresis);
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

            _combat = new PawnCombat(_movement, _boss.Health, tuning.attackDamage, tuning.attackCooldown);
        }

        void Start() => RefreshAttackMarks();

        void Update()
        {
            _combat.Tick(Time.deltaTime);
            if (_attackLeft.WasPressedThisFrame()) Attack(PawnAttackSide.Left);
            if (_attackRight.WasPressedThisFrame()) Attack(PawnAttackSide.Right);

            BufferNewPress();

            if (!_hop.Running)
                TryStartHop();

            if (_hop.Running)
                AnimateHop(_hop.Tick(Time.deltaTime));
        }

        void BufferNewPress()
        {
            for (var i = 0; i < _moves.Length; i++)
                if (_moves[i].WasPressedThisFrame())
                    _buffered = (PawnMove)i;
        }

        void TryStartHop()
        {
            var move = _buffered ?? HeldMove();
            _buffered = null;
            if (move == null || !_movement.TryMove(move.Value)) return;

            _hopFrom = transform.position;
            _hopTo = _board.CellToWorld(_movement.Position);
            _hop.Start(_board.Tuning.moveDuration);
            RefreshAttackMarks();
        }

        void Attack(PawnAttackSide side)
        {
            var result = _combat.TryAttack(side);
            if (result == PawnAttackResult.OnCooldown) return;

            LastAttack = result;
            if (result == PawnAttackResult.Hit) _boss.PlayHit();
        }

        void RefreshAttackMarks()
        {
            _board.Marks.SetAttackable(_combat.AttackableCells);
            _board.RefreshMarks();
        }

        PawnMove? HeldMove()
        {
            for (var i = 0; i < _moves.Length; i++)
                if (_moves[i].IsPressed())
                    return (PawnMove)i;
            return null;
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
