using UnityEngine;
using UnityEngine.UI;

namespace ChessBoss.Game
{
    /// <summary>
    /// Effects along the screen's edge. Each has a uGUI layer of its own, so they overlap without one overwriting another.
    /// They run on real time, so slow motion and hitstop don't hold them, and stand still while the game is paused.
    /// </summary>
    public sealed class ScreenEffects : MonoBehaviour
    {
        const int EdgeTextureSize = 128;

        BattleTuning _tuning;
        Texture2D _edge;
        RawImage _flash;
        RawImage _fire;
        RawImage _lowHealth;
        float _flashLeft;
        float _fireLeft;
        float _fireStrength;
        float _clock;

        public bool LowHealth { get; set; }
        public float FlashAlpha => _flash.color.a;
        public float FireAlpha => _fire.color.a;
        public float LowHealthAlpha => _lowHealth.color.a;

        public void Init(BattleTuning tuning)
        {
            _tuning = tuning;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Under the menus, which are overlay canvases at the default order.
            canvas.sortingOrder = -1;

            _edge = BuildEdgeTexture();
            _lowHealth = AddLayer("LowHealth", tuning.lowHealthColor);
            _fire = AddLayer("ScreenFire", tuning.screenFireColor);
            _flash = AddLayer("HurtFlash", tuning.hurtFlashColor);
        }

        /// <summary>The red flash of a bite.</summary>
        public void Flash() => _flashLeft = _tuning.hurtFlashTime;

        /// <summary>Flames along the edge, at a strength from 0 to 1.</summary>
        public void Fire(float strength)
        {
            _fireStrength = strength;
            _fireLeft = _tuning.screenFireTime;
        }

        void Update()
        {
            if (BattleTime.IsPaused) return;

            _clock += Time.unscaledDeltaTime;
            _flashLeft = Mathf.Max(0, _flashLeft - Time.unscaledDeltaTime);
            _fireLeft = Mathf.Max(0, _fireLeft - Time.unscaledDeltaTime);

            SetAlpha(_flash, _tuning.hurtFlashAlpha * _flashLeft / _tuning.hurtFlashTime);
            var flicker = Mathf.Lerp(1 - _tuning.screenFireFlicker, 1, Mathf.PerlinNoise(_clock * _tuning.screenFireFlickerSpeed, 0));
            SetAlpha(_fire, _fireStrength * flicker * _fireLeft / _tuning.screenFireTime);
            var pulse = Mathf.PingPong(_clock * 2 / _tuning.lowHealthPulsePeriod, 1);
            SetAlpha(_lowHealth, LowHealth ? Mathf.Lerp(_tuning.lowHealthAlphaMin, _tuning.lowHealthAlphaMax, pulse) : 0);
        }

        void OnDestroy() => Destroy(_edge);

        static void SetAlpha(RawImage layer, float alpha)
        {
            var color = layer.color;
            if (color.a == alpha) return;

            color.a = alpha;
            layer.color = color;
        }

        RawImage AddLayer(string layerName, Color color)
        {
            var layer = new GameObject(layerName, typeof(RawImage)).GetComponent<RawImage>();
            layer.transform.SetParent(transform, false);
            var rect = layer.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            layer.texture = _edge;
            layer.raycastTarget = false;
            color.a = 0;
            layer.color = color;
            return layer;
        }

        // White, and opaque toward the edges, ragged like flames. Clear in the middle and around the bottom center, where the pawn stands.
        Texture2D BuildEdgeTexture()
        {
            var texture = new Texture2D(EdgeTextureSize, EdgeTextureSize, TextureFormat.RGBA32, false) { name = "Screen edge" };
            var pixels = new Color32[EdgeTextureSize * EdgeTextureSize];
            for (var y = 0; y < EdgeTextureSize; y++)
            for (var x = 0; x < EdgeTextureSize; x++)
            {
                var point = new Vector2(x, y) / (EdgeTextureSize - 1);
                var fromCenter = Mathf.Max(Mathf.Abs(point.x - 0.5f), Mathf.Abs(point.y - 0.5f)) * 2;
                var edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(_tuning.screenEdgeInner, 1, fromCenter));
                var aroundPawn = Mathf.SmoothStep(0, 1, Vector2.Distance(point, new Vector2(0.5f, 0)) / _tuning.screenEdgeClearRadius);
                var ragged = Mathf.Lerp(1 - _tuning.screenEdgeRaggedness, 1,
                    Mathf.PerlinNoise(point.x * _tuning.screenEdgeNoiseScale, point.y * _tuning.screenEdgeNoiseScale));
                pixels[y * EdgeTextureSize + x] = new Color32(255, 255, 255, (byte)(255 * edge * aroundPawn * ragged));
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
