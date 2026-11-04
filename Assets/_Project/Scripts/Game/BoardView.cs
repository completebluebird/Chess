using ChessBoss.Core;
using UnityEngine;

namespace ChessBoss.Game
{
    public sealed class BoardView : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] BattleTuning _tuning;
        [SerializeField] Renderer _boss;

        Board _board;

        public BattleTuning Tuning => _tuning;
        public Board Board => _board ??= new Board(_tuning.boardSize, _tuning.cornerCut);

        void Awake()
        {
            foreach (var cell in Board.Cells)
                CreateTile(cell);

            var cellSpan = 3 * _tuning.cellSize;
            _boss.transform.SetPositionAndRotation(
                CellToWorld(default) + Vector3.up * (_tuning.bossHeight / 2), Quaternion.identity);
            _boss.transform.localScale = new Vector3(cellSpan, _tuning.bossHeight, cellSpan);
            Paint(_boss, _tuning.bossColor);
        }

        public Vector3 CellToWorld(Cell cell) =>
            transform.position + new Vector3(cell.X, 0, cell.Y) * _tuning.cellSize;

        public void Paint(Renderer target, Color color)
        {
            target.sharedMaterial = _tuning.prototypeMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            target.SetPropertyBlock(block);
        }

        void CreateTile(Cell cell)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"Tile {cell}";
            Destroy(tile.GetComponent<Collider>());
            tile.transform.SetParent(transform, false);
            tile.transform.position = CellToWorld(cell) + Vector3.down * (_tuning.tileThickness / 2);
            var width = _tuning.cellSize * _tuning.tileScale;
            tile.transform.localScale = new Vector3(width, _tuning.tileThickness, width);

            var isLight = ((cell.X + cell.Y) & 1) == 0;
            Paint(tile.GetComponent<Renderer>(), isLight ? _tuning.tileLight : _tuning.tileDark);
        }
    }
}
