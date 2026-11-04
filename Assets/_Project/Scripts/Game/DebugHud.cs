using ChessBoss.Core;
using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>Prototype readout drawn with IMGUI, so it needs no canvas or font asset.</summary>
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] PawnController _pawn;
        [SerializeField] BossView _boss;
        [SerializeField] BossAttackView _attacks;

        void OnGUI()
        {
            var board = _pawn.Movement.Board;
            var combat = _pawn.Combat;
            GUILayout.Label($"Boss HP: {_boss.Health.Current} / {_boss.Health.Max}");
            GUILayout.Label($"Pawn: {_pawn.Movement.Position}");
            GUILayout.Label($"Q: {Reach(board, combat, PawnAttackSide.Left)}  E: {Reach(board, combat, PawnAttackSide.Right)}");
            GUILayout.Label($"Last attack: {_pawn.LastAttack}");
            GUILayout.Label($"Boss hits on pawn: {_attacks.Attacks?.PlayerHits}");
        }

        static string Reach(Board board, PawnCombat combat, PawnAttackSide side) =>
            board.IsBossCell(combat.AttackCell(side)) ? "can hit" : "-";
    }
}
