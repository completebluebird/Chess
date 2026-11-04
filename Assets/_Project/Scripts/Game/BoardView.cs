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
        readonly Dictionary<Cell, GameObject> _walls = new Dictionary<Cell, GameObject>();
        readonly List<Cell> _removedWalls = new List<Cell>();
        MaterialPropertyBlock _block;
        Board _board;
        AttackTimeline _warnings;

        public BattleTuning Tuning => _tuning;
        public Board Board => _board ??= new Board(_tuning.boardSectors, _tuning.boardRings);
        public CellMarks Marks { get; } = new CellMarks();

        float SectorAngle => 2 * Mathf.PI / Board.Sectors;

        void Awake()
        {
            var ringMeshes = new Mesh[Board.Rings];
            for (var ring = Board.BossRing + 1; ring < ringMeshes.Length; ring++)
                ringMeshes[ring] = BuildTileMesh(ring);

            // The boss's ring has no floor; the boss itself blinks when it can be hit.
            foreach (var cell in Board.Cells)
                if (Board.IsWalkable(cell))
                    CreateTile(cell, ringMeshes[cell.Y]);
            CreateBossBase();

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
            // Unity forbids creating this in a MonoBehaviour field initializer.
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColor, color);
            target.SetPropertyBlock(_block);
        }

        public void ShowWalls()
        {
            _removedWalls.Clear();
            foreach (var wall in _walls)
                if (!Board.IsWall(wall.Key))
                    _removedWalls.Add(wall.Key);

            foreach (var cell in _removedWalls)
            {
                Destroy(_walls[cell]);
                _walls.Remove(cell);
            }

            foreach (var cell in Board.Walls)
                if (!_walls.ContainsKey(cell))
                    _walls.Add(cell, CreateWall(cell));
        }

        GameObject CreateWall(Cell cell)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall {cell}";
            Destroy(wall.GetComponent<Collider>());
            wall.transform.SetParent(transform, false);
            wall.transform.SetPositionAndRotation(
                CellToWorld(cell) + Vector3.up * (_tuning.wallHeight / 2),
                Quaternion.Euler(0, -cell.X * SectorAngle * Mathf.Rad2Deg, 0));
            wall.transform.localScale = new Vector3(
                _tuning.cellSize * _tuning.tileScale,
                _tuning.wallHeight,
                RingRadius(cell.Y + 0.5f) * SectorAngle * _tuning.tileScale);
            Paint(wall.GetComponent<Renderer>(), _tuning.wallColor);
            return wall;
        }

        public void ShowWarnings(AttackTimeline warnings)
        {
            _warnings = warnings;
            Marks.SetWarning(warnings.WarningCells);
            RefreshMarks();
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

        /// <summary>A flat dark polygon filling the hole inside the first walkable ring.</summary>
        void CreateBossBase()
        {
            var radius = RingRadius(Board.BossRing + 1);
            var vertices = new List<Vector3> { Vector3.zero };
            var triangles = new List<int>();
            for (var i = 0; i < Board.Sectors; i++)
            {
                var angle = (i + 0.5f) * SectorAngle;
                vertices.Add(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
                // Corners go counterclockwise seen from above, so each triangle lists the next corner first.
                triangles.AddRange(new[] { 0, (i + 1) % Board.Sectors + 1, i + 1 });
            }

            var mesh = new Mesh { name = "Boss base" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            var bossBase = new GameObject("Boss base", typeof(MeshFilter), typeof(MeshRenderer));
            bossBase.transform.SetParent(transform, false);
            bossBase.GetComponent<MeshFilter>().sharedMesh = mesh;
            Paint(bossBase.GetComponent<Renderer>(), _tuning.bossBaseColor);
        }

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
            var floor = ((cell.X + cell.Y) & 1) == 0 ? _tuning.tileLight : _tuning.tileDark;
            switch (Marks.MarkOf(cell))
            {
                case CellMark.Warning: return Color.Lerp(floor, _tuning.warningColor, WarningStrength(cell));
                case CellMark.Attackable: return _tuning.attackableColor;
                default: return floor;
            }
        }

        // Grows stronger toward the strike and pulses, so the time left reads without relying on hue. Ends at full strength.
        float WarningStrength(Cell cell)
        {
            var progress = _warnings.WarningProgress(cell);
            var pulse = Mathf.PingPong(progress * _tuning.warningBlinks * 2, 1);
            return Mathf.Lerp(_tuning.warningMinStrength, 1, progress) * (1 - _tuning.warningBlinkDepth * pulse);
        }
    }
}
