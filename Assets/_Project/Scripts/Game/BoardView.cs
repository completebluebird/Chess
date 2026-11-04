using System.Collections.Generic;
using ChessBoss.Core;
using UnityEngine;

namespace ChessBoss.Game
{
    public sealed class BoardView : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] BattleTuning _tuning;
        [SerializeField] Renderer _boss;

        readonly Dictionary<Cell, Renderer> _tiles = new Dictionary<Cell, Renderer>();
        Board _board;

        public BattleTuning Tuning => _tuning;
        public Board Board => _board ??= new Board(_tuning.boardSize, _tuning.cornerCut);
        public CellMarks Marks { get; } = new CellMarks();

        void Awake()
        {
            foreach (var cell in Board.Cells)
                CreateTile(cell);

            _boss.transform.SetPositionAndRotation(
                CellToWorld(default) + Vector3.up * (_tuning.bossHeight / 2), Quaternion.identity);
            _boss.transform.localScale = new Vector3(_tuning.bossWidth, _tuning.bossHeight, _tuning.bossWidth);
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

        public void RefreshMarks()
        {
            foreach (var tile in _tiles)
                Paint(tile.Value, ColorOf(tile.Key));
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

            var renderer = tile.GetComponent<Renderer>();
            _tiles.Add(cell, renderer);
            Paint(renderer, ColorOf(cell));
        }

        Color ColorOf(Cell cell)
        {
            switch (Marks.MarkOf(cell))
            {
                case CellMark.Warning: return _tuning.warningColor;
                case CellMark.Attackable: return _tuning.attackableColor;
                default: return ((cell.X + cell.Y) & 1) == 0 ? _tuning.tileLight : _tuning.tileDark;
            }
        }
    }
}
