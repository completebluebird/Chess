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
        public Board Board => _board ??= new Board(_tuning.boardSectors, _tuning.boardRings);
        public CellMarks Marks { get; } = new CellMarks();

        float SectorAngle => 2 * Mathf.PI / Board.Sectors;

        void Awake()
        {
            var ringMeshes = new Mesh[Board.Rings];
            for (var ring = 0; ring < ringMeshes.Length; ring++)
                ringMeshes[ring] = BuildTileMesh(ring);

            foreach (var cell in Board.Cells)
                CreateTile(cell, ringMeshes[cell.Y]);

            _boss.transform.SetPositionAndRotation(
                transform.position + Vector3.up * (_tuning.bossHeight / 2), Quaternion.identity);
            _boss.transform.localScale = new Vector3(_tuning.bossWidth, _tuning.bossHeight, _tuning.bossWidth);
            Paint(_boss, _tuning.bossColor);
        }

        public Vector3 CellToWorld(Cell cell)
        {
            var angle = cell.X * SectorAngle;
            return transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * RingRadius(cell.Y + 0.5f);
        }

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

        void CreateTile(Cell cell, Mesh mesh)
        {
            var tile = new GameObject($"Tile {cell}", typeof(MeshFilter), typeof(MeshRenderer));
            tile.transform.SetParent(transform, false);
            tile.transform.localRotation = Quaternion.Euler(0, -cell.X * SectorAngle * Mathf.Rad2Deg, 0);
            tile.GetComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = tile.GetComponent<Renderer>();
            _tiles.Add(cell, renderer);
            Paint(renderer, ColorOf(cell));
        }

        float RingRadius(float ring) => _tuning.boardInnerRadius + ring * _tuning.cellSize;

        /// <summary>A slab shaped like the ring's sector-0 cell, whose inner edge is shorter than its outer edge.</summary>
        Mesh BuildTileMesh(int ring)
        {
            var halfAngle = SectorAngle / 2;
            var center = new Vector3(RingRadius(ring + 0.5f), 0, 0);
            Vector3 Corner(float radius, float side) =>
                center + (new Vector3(radius * Mathf.Cos(halfAngle), 0, side * radius * Mathf.Sin(halfAngle)) - center) * _tuning.tileScale;

            // Clockwise seen from above.
            var top = new[]
            {
                Corner(RingRadius(ring), -1), Corner(RingRadius(ring), 1),
                Corner(RingRadius(ring + 1), 1), Corner(RingRadius(ring + 1), -1),
            };
            var down = Vector3.down * _tuning.tileThickness;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            AddQuad(top[0], top[1], top[2], top[3]);
            for (var i = 0; i < top.Length; i++)
            {
                var from = top[i];
                var to = top[(i + 1) % top.Length];
                AddQuad(to, from, from + down, to + down);
            }

            var mesh = new Mesh { name = $"Tile ring {ring}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
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
