using System;
using System.Collections.Generic;
using ChessBoss.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>
    /// Where the fight's one-off effects start. One lives in each Battle scene, added at run time so the scene file
    /// doesn't change, and it is not static so a reloaded scene can't keep hold of destroyed objects.
    /// Particles and streaks run on game time, so a hitstop freezes them with the hit.
    /// </summary>
    public sealed class BattleEffects : MonoBehaviour
    {
        const int SlashPoints = 8;

        BattleTuning _tuning;
        BoardView _board;
        PawnController _pawn;
        Vector3 _center;
        ParticleSystem _bursts;
        LineRenderer _slash;
        TrailRenderer _lance;
        float _slashLeft;

        public ScreenEffects Screen { get; private set; }
        public CameraEffects Camera { get; private set; }
        public PawnEffects Pawn { get; private set; }
        public HeatHaze Haze { get; private set; }
        public bool SlashShown => _slash.enabled;
        public bool LanceShown => _lance.emitting;
        public int LiveParticles => _bursts.particleCount;

        /// <summary>A name from SoundCues, each time its moment happens.</summary>
        public event Action<string> SoundCued;

        public void Init(BoardView board, PawnController pawn, BossView boss)
        {
            _tuning = board.Tuning;
            _board = board;
            _pawn = pawn;
            _center = board.transform.position;
            Screen = new GameObject("ScreenEffects").AddComponent<ScreenEffects>();
            Screen.Init(_tuning);
            Camera = FindAnyObjectByType<CinemachineCamera>().gameObject.AddComponent<CameraEffects>();
            Camera.Init(board, pawn, boss);
            Pawn = pawn.gameObject.AddComponent<PawnEffects>();
            Pawn.Init(board, pawn, this);
            Haze = gameObject.AddComponent<HeatHaze>();
            Haze.Init(board, pawn, boss.GetComponent<BossAttackView>());

            _bursts = BuildBursts();
            _slash = new GameObject("Slash").AddComponent<LineRenderer>();
            _slash.sharedMaterial = _tuning.particleMaterial;
            _slash.positionCount = SlashPoints;
            _slash.enabled = false;

            _lance = new GameObject("Lance").AddComponent<TrailRenderer>();
            _lance.transform.SetParent(pawn.Body.transform, false);
            _lance.sharedMaterial = _tuning.particleMaterial;
            _lance.time = _tuning.lanceTime;
            _lance.startWidth = _tuning.lanceWidth;
            _lance.endWidth = 0;
            _lance.startColor = _tuning.slashColor;
            _lance.endColor = Clear(_tuning.slashColor);
            _lance.emitting = false;
        }

        public void Sound(string cue) => SoundCued?.Invoke(cue);

        /// <summary>The pawn's attack: a white arc toward the cell, thin and short when it hits nothing, with sparks on the boss when it does.</summary>
        public void Slash(Vector3 to, bool hit)
        {
            var from = _pawn.Body.bounds.center;
            to.y = from.y;
            // Swings out to the attack's own side and over the pawn's head: the camera looks along the pawn's line
            // to the boss, and a straight streak would hide behind the pawn.
            var inward = _center - from;
            inward.y = 0;
            var bulge = Vector3.ProjectOnPlane(to - from, inward.normalized).normalized * _tuning.slashBulge + Vector3.up * _tuning.slashLift;
            var reach = hit ? 1 : _tuning.slashMissReach;
            for (var i = 0; i < SlashPoints; i++)
            {
                var along = reach * i / (SlashPoints - 1);
                _slash.SetPosition(i, Vector3.Lerp(from, to, along) + bulge * Mathf.Sin(along * Mathf.PI));
            }

            _slash.startWidth = hit ? _tuning.slashWidth : _tuning.slashMissWidth;
            _slash.endWidth = 0;
            _slash.enabled = true;
            _slashLeft = _tuning.slashTime;
            if (hit) Sparks(to, _tuning.sparkCount);
            Sound(SoundCues.Slash);
        }

        /// <summary>The white trail an awakened piece leaves while it flies.</summary>
        public void ShowLance(bool flying)
        {
            if (flying && !_lance.emitting) _lance.Clear();
            _lance.emitting = flying;
        }

        public void Sparks(Vector3 at, int count) =>
            Burst(at, _tuning.sparkColor, count, _tuning.sparkSpeed, _tuning.sparkSize, _tuning.sparkLifetime, false);

        /// <summary>Dust thrown outward along the floor.</summary>
        public void Dust(Vector3 at) =>
            Burst(at, _tuning.dustColor, _tuning.dustCount, _tuning.dustSpeed, _tuning.dustSize, _tuning.dustLifetime, true);

        /// <summary>A boss strike landing. It shows whether or not the player was on the cells.</summary>
        public void Impact(BossAttackKind kind, IReadOnlyList<Cell> cells)
        {
            switch (kind)
            {
                case BossAttackKind.Laser:
                    Camera.ShakeWeak();
                    Flames(cells);
                    break;
                case BossAttackKind.Meteor:
                    Camera.ShakeStrong();
                    Flames(cells);
                    break;
                case BossAttackKind.Bite:
                    Camera.ShakeStrong();
                    break;
                case BossAttackKind.Slam:
                    // The landing is the heavy one; the wave's later rings rumble.
                    if (cells[0].Y == Board.BossRing + 1) Camera.ShakeStrong();
                    else Camera.ShakeWeak();
                    foreach (var cell in cells)
                        Burst(_board.CellToWorld(cell), _tuning.dustColor, _tuning.impactDustCount, _tuning.dustSpeed, _tuning.dustSize, _tuning.dustLifetime, true);
                    break;
                default:
                    // The breaths don't shake: the haze tells where the beam is.
                    Flames(cells);
                    break;
            }

            Sound(SoundCues.Impact);
        }

        /// <summary>The player was hit. Each attack is felt differently, so the cause reads without looking at the floor.</summary>
        public void PlayerHurt(BossAttackKind kind)
        {
            Pawn.Stagger();
            switch (kind)
            {
                case BossAttackKind.Laser:
                    Screen.Fire(_tuning.screenFireWeak);
                    break;
                case BossAttackKind.Meteor:
                    Camera.Tilt();
                    Screen.Fire(_tuning.screenFireWeak);
                    break;
                case BossAttackKind.Bite:
                    Sound(SoundCues.Bite);
                    Screen.Flash();
                    Camera.Tilt();
                    break;
                case BossAttackKind.Slam:
                    Camera.Tilt();
                    break;
                default:
                    Screen.Fire(_tuning.screenFireStrong);
                    break;
            }

            Sound(SoundCues.Hurt);
        }

        void Flames(IReadOnlyList<Cell> cells)
        {
            foreach (var cell in cells)
                Burst(_board.CellToWorld(cell), _tuning.impactFireColor, _tuning.impactFireCount, _tuning.impactFireSpeed, _tuning.impactFireSize, _tuning.impactFireLifetime, false);
        }

        public void Burst(Vector3 at, Color color, int count, float speed, float size, float lifetime, bool alongFloor)
        {
            var particle = new ParticleSystem.EmitParams
            {
                position = at, startColor = color, startSize = size, startLifetime = lifetime,
            };
            for (var i = 0; i < count; i++)
            {
                var direction = UnityEngine.Random.onUnitSphere;
                direction.y = alongFloor ? 0 : Mathf.Abs(direction.y);
                particle.velocity = direction.normalized * (speed * UnityEngine.Random.Range(0.5f, 1f));
                _bursts.Emit(particle, 1);
            }
        }

        void Update()
        {
            if (!_slash.enabled) return;

            _slashLeft -= Time.deltaTime;
            var color = _tuning.slashColor;
            color.a *= Mathf.Clamp01(_slashLeft / _tuning.slashTime);
            _slash.startColor = color;
            _slash.endColor = color;
            _slash.enabled = _slashLeft > 0;
        }

        // One system for every burst: each particle is given its place, color and velocity as it is emitted.
        ParticleSystem BuildBursts()
        {
            var bursts = new GameObject("Bursts").AddComponent<ParticleSystem>();
            bursts.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = bursts.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = _tuning.burstGravity;
            main.maxParticles = _tuning.burstMaxParticles;
            var emission = bursts.emission;
            emission.enabled = false;
            var shape = bursts.shape;
            shape.enabled = false;
            var fade = bursts.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(Color.white, Clear(Color.white));
            bursts.GetComponent<ParticleSystemRenderer>().sharedMaterial = _tuning.particleMaterial;
            bursts.Play();
            return bursts;
        }

        static Color Clear(Color color)
        {
            color.a = 0;
            return color;
        }
    }
}
