using System.Collections;
using System.Linq;
using ChessBoss.Core;
using ChessBoss.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChessBoss.Tests
{
    /// <summary>Plays the real Battle scene with real key events, which the Core tests can't reach. Each test ends before the boss's first attack.</summary>
    public class AwakeningPlayTests
    {
        PawnController _pawn;
        BossView _boss;
        BoardView _board;
        BattleTuning _tuning;
        InputSettings.BackgroundBehavior _backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;

        [UnitySetUp]
        public IEnumerator LoadBattle()
        {
            // Without these, the keys below are dropped whenever the Editor window isn't in front.
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            SceneManager.LoadScene("Battle");
            yield return null;
            yield return null;
            _pawn = Object.FindAnyObjectByType<PawnController>();
            _boss = Object.FindAnyObjectByType<BossView>();
            _board = Object.FindAnyObjectByType<BoardView>();
            _tuning = _board.Tuning;
        }

        [TearDown]
        public void Release()
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            BattleTime.Stopped = false;
            BattleTime.Paused = false;
            BattleTime.SlowMotion = 1;
            BattleTime.StunSlowMotion = 1;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
        }

        IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        void FillGauge() => _pawn.Awakening.Tick(_tuning.awakenChargeTime, 0);

        [UnityTest]
        public IEnumerator Key1_WithoutAFullGauge_LeavesAPawnThatStillWalks()
        {
            var start = _pawn.Movement.Position;

            yield return Press(Key.Digit1);
            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(1f, Time.timeScale);

            yield return Press(Key.W);
            Assert.AreEqual(new Cell(start.X, start.Y - 1), _pawn.Movement.Position);
        }

        [UnityTest]
        public IEnumerator RookForward_FromKeysToLanding()
        {
            var start = _pawn.Movement.Position;
            var moved = start.Y - 1;
            FillGauge();

            yield return Press(Key.Digit1);
            Assert.AreEqual(AwakenPiece.Rook, _pawn.Awakening.Active);
            Assert.AreEqual(_tuning.awakenTimeScale, Time.timeScale, 1e-4f);

            yield return Press(Key.W);
            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(new Cell(start.X, 1), _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth - _tuning.awakenBaseDamage - moved * _tuning.awakenDamagePerRing, _boss.Health.Current);
            Assert.IsTrue(_pawn.Vitals.IsInvulnerable);

            yield return new WaitForSecondsRealtime(_tuning.hitstopDuration + _tuning.awakenTravelTime / _tuning.awakenTimeScale + 0.1f);
            Assert.AreEqual(1f, Time.timeScale, "the hitstop must not leave time stopped or slowed");
            Assert.That(Vector3.Distance(_board.CellToWorld(_pawn.Movement.Position), _pawn.transform.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator RookSideways_OffTheBossRing_FailsWithoutMoving_AndSpendsTheGauge()
        {
            var start = _pawn.Movement.Position;
            FillGauge();
            yield return Press(Key.Digit1);

            yield return Press(Key.A);

            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(start, _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth, _boss.Health.Current);
            Assert.AreEqual(0f, _pawn.Awakening.Gauge, 0.05f);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator BishopForwardRight_FromKeysToLanding()
        {
            var start = _pawn.Movement.Position;
            var moved = start.Y - 1;
            FillGauge();

            yield return Press(Key.Digit3);
            Assert.AreEqual(AwakenPiece.Bishop, _pawn.Awakening.Active);

            yield return Press(Key.W, Key.D);
            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(_pawn.Movement.Board.Step(new Cell(start.X, 1), moved + 2, 0), _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth - _tuning.awakenBaseDamage - moved * _tuning.awakenDamagePerRing, _boss.Health.Current);

            yield return new WaitForSecondsRealtime(_tuning.hitstopDuration + _tuning.awakenTravelTime / _tuning.awakenTimeScale + 0.1f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.That(Vector3.Distance(_board.CellToWorld(_pawn.Movement.Position), _pawn.transform.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Bishop_IgnoresASingleKey_AndStaysAwakened()
        {
            var start = _pawn.Movement.Position;
            FillGauge();
            yield return Press(Key.Digit3);

            yield return Press(Key.W);

            Assert.AreEqual(AwakenPiece.Bishop, _pawn.Awakening.Active);
            Assert.AreEqual(start, _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth, _boss.Health.Current);
        }

        [UnityTest]
        public IEnumerator Knight_ShowsCandidates_ThenJumps_FromKeysToLanding()
        {
            var board = _pawn.Movement.Board;
            var start = _pawn.Movement.Position;
            yield return Press(Key.W);
            yield return new WaitForSecondsRealtime(_tuning.moveCooldown + 0.05f);
            Assert.AreEqual(new Cell(start.X, start.Y - 1), _pawn.Movement.Position);
            FillGauge();

            yield return Press(Key.Digit2);
            Assert.AreEqual(AwakenPiece.Knight, _pawn.Awakening.Active);
            Assert.AreEqual(_tuning.awakenTimeScale, Time.timeScale, 1e-4f);
            var landing = board.Step(_pawn.Movement.Position, 1, -2);
            Assert.AreEqual(CellMark.None, _board.Marks.MarkOf(landing));

            yield return Press(Key.W);
            Assert.AreEqual(CellMark.Attackable, _board.Marks.MarkOf(landing));
            Assert.AreEqual(CellMark.Attackable, _board.Marks.MarkOf(board.Step(_pawn.Movement.Position, -1, -2)));

            yield return Press(Key.S);
            Assert.AreEqual(AwakenPiece.Knight, _pawn.Awakening.Active, "the opposite key is ignored");

            yield return Press(Key.D);
            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(landing, _pawn.Movement.Position);
            Assert.AreEqual(_tuning.bossHealth - _tuning.knightDamage, _boss.Health.Current);
            Assert.AreEqual(CellMark.None, _board.Marks.MarkOf(board.Step(landing, 2, 0)));

            yield return new WaitForSecondsRealtime(_tuning.hitstopDuration + _tuning.awakenTravelTime / _tuning.awakenTimeScale + 0.1f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.That(Vector3.Distance(_board.CellToWorld(landing), _pawn.transform.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator BreathThenStun_SlowsOnlyTheEntry_AndHoldsTheGauge_ThenTheBossJumpsAndSlams()
        {
            var attacks = Object.FindAnyObjectByType<BossAttackView>().Attacks;
            _boss.Health.TakeDamage(_tuning.bossHealth - (int)(_tuning.bossHealth * _tuning.stunHealthFractions[0]));
            yield return null;
            yield return null;
            Assert.AreEqual(BossPhase.Breathing, attacks.Phase);

            var limit = Time.realtimeSinceStartup + 15;
            yield return new WaitUntil(() => attacks.Phase == BossPhase.Stunned || Time.realtimeSinceStartup > limit);
            Assert.AreEqual(BossPhase.Stunned, attacks.Phase);
            yield return null;
            Assert.AreEqual(_tuning.stunSlowTimeScale, Time.timeScale, 1e-4f);
            var gauge = _pawn.Awakening.Gauge;

            yield return new WaitForSecondsRealtime(_tuning.stunSlowTime + 0.1f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(BossPhase.Stunned, attacks.Phase);
            Assert.AreEqual(gauge, _pawn.Awakening.Gauge, 1e-4f);

            var body = _boss.GetComponentInChildren<Renderer>().transform;
            var ground = body.position.y;
            limit = Time.realtimeSinceStartup + _tuning.stunDuration + 2;
            yield return new WaitUntil(() => attacks.Phase != BossPhase.Stunned || Time.realtimeSinceStartup > limit);
            Assert.AreEqual(BossPhase.Slamming, attacks.Phase);

            yield return new WaitForSeconds(_tuning.slamAirTime / 2);
            Assert.IsTrue(_boss.Health.Untouchable);
            Assert.Greater(body.position.y, ground + _tuning.slamJumpHeight / 2);
            Assert.Greater(_pawn.Awakening.Gauge, gauge, "only the stun holds the gauge");

            limit = Time.realtimeSinceStartup + _tuning.slamAirTime + _tuning.boardRings * _tuning.slamRingGap + 2;
            yield return new WaitUntil(() => attacks.Phase != BossPhase.Slamming || Time.realtimeSinceStartup > limit);
            Assert.AreEqual(BossPhase.Pressing, attacks.Phase);
            yield return null;
            Assert.IsFalse(_boss.Health.Untouchable);
            Assert.AreEqual(ground, body.position.y, 1e-3f);
        }

        [UnityTest]
        public IEnumerator Awakening_LightsTheFlameAndPillar_InThePiecesColor_AndACancelPutsTheFlameOut()
        {
            var effects = _pawn.Effects.Pawn;
            string cue = null;
            _pawn.Effects.SoundCued += name => cue = name;
            Assert.IsFalse(effects.FlameShown);
            FillGauge();

            yield return Press(Key.Digit3);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.IsTrue(effects.FlameShown);
            Assert.Greater(effects.FlameParticles, 0, "the flame runs on real time through the slow motion");
            Assert.IsTrue(effects.PillarShown);
            Assert.AreEqual(_tuning.bishopFlameColor, effects.PillarColor);
            Assert.AreEqual(SoundCues.Awaken, cue);

            yield return new WaitForSecondsRealtime(_tuning.pillarTime);
            Assert.IsFalse(effects.PillarShown);
            Assert.IsTrue(effects.FlameShown);

            _pawn.Vitals.Hit(0, false);
            yield return null;
            yield return null;
            Assert.IsFalse(effects.FlameShown);
            Assert.AreEqual(0, effects.FlameParticles);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Awakening_UsedForAStrike_LetsTheFlameScatter()
        {
            var effects = _pawn.Effects.Pawn;
            FillGauge();
            yield return Press(Key.Digit1);
            yield return new WaitForSecondsRealtime(0.2f);

            yield return Press(Key.W);
            Assert.IsFalse(effects.FlameShown);
            Assert.Greater(effects.FlameParticles, 0);

            yield return new WaitForSecondsRealtime(_tuning.flameLifetime + _tuning.awakenHitstopDuration + 0.5f);
            Assert.AreEqual(0, effects.FlameParticles);
        }

        [UnityTest]
        public IEnumerator AwakeningZoom_GoesInThenWide_AndComesBackWhenItEnds()
        {
            var camera = _pawn.Effects.Camera;
            Assert.AreEqual(1f, camera.Zoom);
            FillGauge();

            yield return Press(Key.Digit2);
            yield return new WaitForSecondsRealtime(_tuning.awakenZoomInTime * 0.6f);
            Assert.Less(camera.Zoom, 0.9f);

            yield return new WaitForSecondsRealtime(_tuning.awakenZoomInTime + _tuning.awakenZoomOutTime);
            Assert.AreEqual(_tuning.awakenZoomWide, camera.Zoom, 0.01f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(_tuning.awakenZoomWide, camera.Zoom, 0.01f, "it stays wide while the piece waits");

            _pawn.Vitals.Hit(0, false);
            yield return new WaitForSecondsRealtime(_tuning.awakenZoomReturnTime + 0.1f);
            Assert.AreEqual(1f, camera.Zoom, 0.01f);
        }

        [UnityTest]
        public IEnumerator StrikeEnteredDuringTheZoom_GoesOutAtOnce()
        {
            var start = _pawn.Movement.Position;
            FillGauge();
            yield return Press(Key.Digit1);
            Assert.Less(_pawn.Effects.Camera.Zoom, 1f, "the zoom has begun");

            yield return Press(Key.W);

            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(new Cell(start.X, 1), _pawn.Movement.Position);
            yield return new WaitForSecondsRealtime(_tuning.awakenZoomReturnTime + _tuning.awakenTravelTime + _tuning.awakenHitstopDuration + 0.2f);
            Assert.AreEqual(1f, _pawn.Effects.Camera.Zoom, 0.01f);
        }

        [UnityTest]
        public IEnumerator FullGauge_MakesThePawnPulse_UntilItAwakens()
        {
            var block = new MaterialPropertyBlock();
            Color Shown()
            {
                _pawn.Body.GetPropertyBlock(block);
                return block.GetColor("_BaseColor");
            }

            yield return null;
            Assert.Less(Vector4.Distance(_tuning.pawnColor, Shown()), 0.01f);
            FillGauge();

            var brightest = 0f;
            for (var until = Time.realtimeSinceStartup + _tuning.gaugeFullPulsePeriod; Time.realtimeSinceStartup < until;)
            {
                yield return null;
                brightest = Mathf.Max(brightest, Shown().g - _tuning.pawnColor.g);
            }

            Assert.Greater(brightest, 0.2f);

            yield return Press(Key.Digit1);
            Assert.Less(Vector4.Distance(_tuning.awakenedColor, Shown()), 0.01f);
        }

        [UnityTest]
        public IEnumerator AwakenedHit_ShowsWhenThePieceArrives_WithALongerHitstop()
        {
            FillGauge();
            yield return Press(Key.Digit1);

            yield return Press(Key.W);
            Assert.AreEqual(1f, Time.timeScale, "nothing shows the hit while the rook is still flying");
            Assert.IsTrue(_pawn.Effects.LanceShown);

            var stoppedAt = 0f;
            var limit = Time.realtimeSinceStartup + 2;
            yield return new WaitUntil(() => Time.timeScale == 0 || Time.realtimeSinceStartup > limit);
            stoppedAt = Time.realtimeSinceStartup;
            Assert.AreEqual(0f, Time.timeScale);
            Assert.That(Vector3.Distance(_board.CellToWorld(_pawn.Movement.Position), _pawn.transform.position), Is.LessThan(0.01f));
            Assert.Greater(_pawn.Effects.LiveParticles, 0);
            Assert.IsFalse(_pawn.Effects.LanceShown);

            yield return new WaitUntil(() => Time.timeScale == 1 || Time.realtimeSinceStartup > limit);
            Assert.AreEqual(_tuning.awakenHitstopDuration, Time.realtimeSinceStartup - stoppedAt, 0.05f);
        }

        [UnityTest]
        public IEnumerator PawnAttack_DrawsItsSlash_WithSparksAndHitstopOnlyOnAHit()
        {
            yield return Press(Key.Q);
            Assert.AreEqual(PawnAttackResult.Miss, _pawn.LastAttack);
            Assert.IsTrue(_pawn.Effects.SlashShown);
            Assert.AreEqual(0, _pawn.Effects.LiveParticles);
            Assert.AreEqual(1f, Time.timeScale);
            yield return new WaitForSecondsRealtime(_tuning.attackCooldown + 0.05f);
            Assert.IsFalse(_pawn.Effects.SlashShown);

            _pawn.Movement.Land(new Cell(18, 1));
            yield return Press(Key.E);
            Assert.AreEqual(PawnAttackResult.Hit, _pawn.LastAttack);
            Assert.IsTrue(_pawn.Effects.SlashShown);
            Assert.Greater(_pawn.Effects.LiveParticles, 0);
        }

        [UnityTest]
        public IEnumerator BossAttackLanding_ShowsItsImpact_AndTheHitOnThePawn()
        {
            var attacks = Object.FindAnyObjectByType<BossAttackView>().Attacks;
            var cues = new System.Collections.Generic.List<string>();
            _pawn.Effects.SoundCued += cues.Add;

            var limit = Time.realtimeSinceStartup + 15;
            yield return new WaitUntil(() => attacks.PlayerHits > 0 || Time.realtimeSinceStartup > limit);
            Assert.Greater(attacks.PlayerHits, 0, "standing still, the first attack lands");
            yield return null;

            // Far from the boss the first attack is a laser, a meteor or the slow breath: all fire.
            Assert.Greater(_pawn.Effects.LiveParticles, 0);
            Assert.Greater(_pawn.Effects.Screen.FireAlpha, 0f);
            Assert.AreEqual(0f, _pawn.Effects.Screen.FlashAlpha);
            Assert.IsTrue(_pawn.Effects.Pawn.Staggering);
            CollectionAssert.Contains(cues, SoundCues.Impact);
            CollectionAssert.Contains(cues, SoundCues.Hurt);
            CollectionAssert.DoesNotContain(cues, SoundCues.Bite);
        }

        [UnityTest]
        public IEnumerator EachHit_FeelsDifferent_BiteFlashesAndTilts_LaserOnlyBurns()
        {
            var screen = _pawn.Effects.Screen;
            var camera = _pawn.Effects.Camera;
            var cues = new System.Collections.Generic.List<string>();
            _pawn.Effects.SoundCued += cues.Add;

            _pawn.Effects.PlayerHurt(BossAttackKind.Laser);
            yield return null;
            yield return null;
            Assert.Greater(screen.FireAlpha, 0f);
            Assert.AreEqual(0f, screen.FlashAlpha);
            Assert.AreEqual(0f, camera.Roll);
            yield return new WaitForSecondsRealtime(_tuning.screenFireTime + 0.05f);
            Assert.AreEqual(0f, screen.FireAlpha);

            _pawn.Effects.PlayerHurt(BossAttackKind.Bite);
            yield return null;
            yield return null;
            Assert.Greater(screen.FlashAlpha, 0f);
            Assert.AreEqual(0f, screen.FireAlpha);
            Assert.That(Mathf.Abs(camera.Roll), Is.InRange(1f, _tuning.hurtTiltDegrees));
            CollectionAssert.Contains(cues, SoundCues.Bite);
            Assert.IsTrue(_pawn.Effects.Pawn.Staggering);

            yield return new WaitForSecondsRealtime(_tuning.hurtTiltReturnTime + 0.05f);
            Assert.AreEqual(0f, camera.Roll);

            // All of this ends long before the boss's first real attack could land and rock the pawn again.
            yield return new WaitForSeconds(_tuning.staggerTime);
            Assert.IsFalse(_pawn.Effects.Pawn.Staggering);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, _pawn.Body.transform.localRotation), 0.01f);
        }

        [UnityTest]
        public IEnumerator DodgedMeteor_StillShowsItsImpact()
        {
            var away = _board.Board.Step(_pawn.Movement.Position, _board.Board.Sectors / 2, 0);

            _pawn.Effects.Impact(BossAttackKind.Meteor, new[] { away });
            yield return null;

            Assert.Greater(_pawn.Effects.LiveParticles, 0);
            Assert.AreEqual(0f, _pawn.Effects.Screen.FireAlpha);
            Assert.IsFalse(_pawn.Effects.Pawn.Staggering);
        }

        [UnityTest]
        public IEnumerator Burning_ShowsEmbersAtThePawnsFeet_OnlyWhileItLasts()
        {
            var effects = _pawn.Effects.Pawn;
            yield return null;
            Assert.IsFalse(effects.EmbersShown);

            _pawn.Vitals.Hit(0, true);
            yield return null;
            yield return null;
            Assert.IsTrue(effects.EmbersShown);
            Assert.IsFalse(effects.FlameShown, "the burn is not the awakening's flame");

            _pawn.Vitals.Tick(_tuning.burnDuration);
            Assert.IsFalse(_pawn.Vitals.IsBurning);
            yield return null;
            yield return null;
            Assert.IsFalse(effects.EmbersShown);
        }

        [UnityTest]
        public IEnumerator LowHealth_ReddensTheScreenEdge_FromTheThresholdDown()
        {
            var health = _pawn.Vitals.Health;
            var threshold = Mathf.RoundToInt(_tuning.lowHealthFraction * health.Max);
            var screen = _pawn.Effects.Screen;

            health.TakeDamage(health.Max - threshold - 1);
            yield return null;
            yield return null;
            Assert.AreEqual(0f, screen.LowHealthAlpha);

            health.TakeDamage(1);
            yield return null;
            yield return null;
            Assert.GreaterOrEqual(screen.LowHealthAlpha, _tuning.lowHealthAlphaMin);
        }

        [UnityTest]
        public IEnumerator Camera_FramesFromTheTuning_AndShakesHarderForAStrongHit()
        {
            var view = Camera.main.transform;
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(_tuning.cameraFieldOfView, Camera.main.fieldOfView, 0.01f);
            Assert.AreEqual(_tuning.cameraOffset.magnitude, Vector3.Distance(view.position, _board.CellToWorld(_pawn.Movement.Position)), 0.2f);

            IEnumerator Travel(System.Action<float> report)
            {
                var rest = view.position;
                var farthest = 0f;
                for (var until = Time.realtimeSinceStartup + _tuning.shakeDuration; Time.realtimeSinceStartup < until;)
                {
                    yield return null;
                    farthest = Mathf.Max(farthest, Vector3.Distance(rest, view.position));
                }

                report(farthest);
            }

            var still = 0f;
            var weak = 0f;
            var strong = 0f;
            yield return Travel(travel => still = travel);
            _pawn.Effects.Camera.ShakeWeak();
            yield return Travel(travel => weak = travel);
            yield return new WaitForSecondsRealtime(_tuning.shakeDuration);
            _pawn.Effects.Camera.ShakeStrong();
            yield return Travel(travel => strong = travel);

            Assert.Less(still, 0.005f);
            Assert.Greater(weak, still + 0.01f);
            Assert.Greater(strong, weak * 1.5f);
        }

        [UnityTest]
        public IEnumerator Wall_BetweenTheCameraAndThePawn_FadesAndComesBack()
        {
            _boss.Health.TakeDamage(_tuning.bossHealth - (int)(_tuning.bossHealth * _tuning.stunHealthFractions[0]));
            yield return null;
            yield return null;
            var wallCell = _board.Board.Walls.First();
            var wall = _board.Walls.OrderBy(shown => Vector3.Distance(shown.transform.position, _board.CellToWorld(wallCell))).First();

            // In front of the wall, so the camera behind the pawn looks through it.
            var inFront = new Cell(wallCell.X, wallCell.Y - 1);
            _pawn.Movement.Land(inFront);
            _pawn.transform.position = _board.CellToWorld(inFront);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(_tuning.occluderAlpha, _board.AlphaOf(wall), 0.01f);
            Assert.AreEqual(_tuning.fadeMaterial, wall.sharedMaterial);

            var away = _board.Board.Step(inFront, _board.Board.Sectors / 2, 0);
            _pawn.Movement.Land(away);
            _pawn.transform.position = _board.CellToWorld(away);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(1f, _board.AlphaOf(wall));
            Assert.AreEqual(_tuning.prototypeMaterial, wall.sharedMaterial);
        }

        [UnityTest]
        public IEnumerator PawnAttacks_DoNothing_WhileAwakened()
        {
            FillGauge();
            yield return Press(Key.Digit1);

            yield return Press(Key.Q);

            Assert.AreEqual(AwakenPiece.Rook, _pawn.Awakening.Active);
            Assert.IsNull(_pawn.LastAttack);
        }

        [UnityTest]
        public IEnumerator BeingHit_CancelsTheAwakening_AndEndsSlowMotion()
        {
            FillGauge();
            yield return Press(Key.Digit1);

            _pawn.Vitals.Hit(0, false);
            yield return null;

            Assert.IsNull(_pawn.Awakening.Active);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Hitstop_DuringSlowMotion_ReturnsToSlowMotion()
        {
            FillGauge();
            yield return Press(Key.Digit1);

            _boss.PlayHit();
            Assert.AreEqual(0f, Time.timeScale);

            yield return new WaitForSecondsRealtime(_tuning.hitstopDuration + 0.05f);
            Assert.AreEqual(_tuning.awakenTimeScale, Time.timeScale, 1e-4f);
        }
    }
}
