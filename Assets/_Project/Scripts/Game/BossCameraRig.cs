using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>
    /// The camera's tracking target: follows the pawn along the floor, ignoring hops so the view
    /// doesn't bob, and faces the boss so the camera orbits smoothly behind the pawn.
    /// </summary>
    public sealed class BossCameraRig : MonoBehaviour
    {
        [SerializeField] Transform _pawn;
        [SerializeField] Transform _boss;

        void LateUpdate()
        {
            var position = _pawn.position;
            position.y = transform.position.y;
            var toBoss = _boss.position - position;
            toBoss.y = 0;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(toBoss));
        }
    }
}
