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
            var vitals = _pawn.Vitals;
            GUILayout.Label($"Player HP: {vitals.Health.Current} / {vitals.Health.Max}{(vitals.IsBurning ? "  BURNING" : "")}{(vitals.IsInvulnerable ? "  INVULNERABLE" : "")}{(vitals.Health.IsDead ? "  DEFEAT" : "")}");
            GUILayout.Label($"Pawn: {_pawn.Movement.Position}");
            GUILayout.Label($"Q: {Reach(board, combat, PawnAttackSide.Left)}  E: {Reach(board, combat, PawnAttackSide.Right)}");
            GUILayout.Label($"Last attack: {_pawn.LastAttack}");
            GUILayout.Label($"Boss attack: {_attacks.Attacks?.CurrentAttack}  Hits on pawn: {_attacks.Attacks?.PlayerHits}");
        }

        static string Reach(Board board, PawnCombat combat, PawnAttackSide side) =>
            board.IsBossCell(combat.AttackCell(side)) ? "can hit" : "-";
    }
}
