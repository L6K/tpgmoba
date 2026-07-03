using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Enigma.Character;
using Enigma.Combat;
using Enigma.UI;
using Enigma.Vision;

namespace Enigma.GameModes
{
    /// <summary>
    /// 次元リフト（中立オブジェクティブ）の実体。RiftEventModel の状態機械を駆動し、
    /// ゾーン占拠で得た効果（Shortcut=転送 / TeamVision=チーム視界 / TeamHaste=チーム加速）を
    /// 制圧チームへ付与する。CentralObjectiveDirector と同様 RuntimeInitializeOnLoadMethod で
    /// マップシーンに自動生成し、シーン編集を不要にする。
    /// </summary>
    public sealed class RiftDirector : MonoBehaviour
    {
        public static RiftDirector Instance { get; private set; }

        // 出現は試合中盤から。テンポ重視で短めに設定。
        private readonly RiftEventModel _model = new RiftEventModel(
            firstOpenAt: 60f, warningLead: 8f, openWindow: 25f,
            captureSeconds: 5f, effectDuration: 40f, cooldown: 70f);

        // 配置（上側リバー付近に入口、下側へショートカット出口）。M-0(平面1.4倍拡張)で位置・半径を更新。
        // M-A(立体化)で川底(-1.2)に接地するよう y を更新(z=±36 は川のトレンチ範囲内=22<=r<=54)。
        private static readonly Vector3 RiftPos      = new Vector3(0f, -0.1f, 36f);
        private static readonly Vector3 ShortcutExit = new Vector3(0f, -0.1f, -36f);
        private const float ZoneRadius     = 7f;
        private const float VisionRadius   = 22f;
        private const float HasteStrength  = 0.25f;
        private const float ShortcutPerUnitCd = 5f;

        private GameObject _portal;
        private GameObject _exitPortal;
        private Light _portalLight;
        private Material _portalMat;
        private Material _exitMat;

        private RiftState  _lastState  = RiftState.Dormant;
        private GameHudController _hud;

        // ショートカットの連続転送防止（ユニット instanceID 毎の次回可能時刻）。
        private readonly Dictionary<int, float> _shortcutReadyAt = new Dictionary<int, float>();

        public RiftState State { get; private set; } = RiftState.Dormant;
        public float SecondsToNextChange { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (SceneManager.GetActiveScene().name != "AetherRift_Map") return;
            if (Instance != null) return;
            var go = new GameObject("RiftDirector");
            go.AddComponent<RiftDirector>();
        }

        private void Awake()
        {
            Instance = this;
            BuildVisuals();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            FogOfWarDirector.RemoveExternalSource(this);
        }

        private void Update()
        {
            float now = Time.time;
            int presentTeam = ResolvePresentTeam();
            var status = _model.Tick(now, Time.deltaTime, presentTeam);

            State = status.State;
            SecondsToNextChange = status.SecondsToNextChange;

            ApplyVisuals(status);
            ApplyEffect(status);
            AnnounceTransitions(status);
            _lastState = status.State;
        }

        // ゾーン内に居るチャンピオンのチームを判定。単一チームのみなら 0/1、無人/競合は -1。
        private int ResolvePresentTeam()
        {
            bool blue = false, red = false;

            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                Mark(pc.gameObject, ref blue, ref red);
            foreach (var ai in FindObjectsByType<EnemyChampionAI>(FindObjectsSortMode.None))
                Mark(ai.gameObject, ref blue, ref red);

            if (blue == red) return -1; // 両方 or どちらも居ない
            return blue ? 0 : 1;
        }

        private void Mark(GameObject go, ref bool blue, ref bool red)
        {
            var hc = go.GetComponent<HealthComponent>();
            if (hc != null && hc.Model.IsDead) return;
            if (!InZone(go.transform.position)) return;

            var tag = go.GetComponentInParent<TeamTag>();
            if (tag == null) return;
            if (tag.Team == TeamId.Blue) blue = true;
            else if (tag.Team == TeamId.Red) red = true;
        }

        private static bool InZone(Vector3 p)
        {
            float dx = p.x - RiftPos.x;
            float dz = p.z - RiftPos.z;
            return dx * dx + dz * dz <= ZoneRadius * ZoneRadius;
        }

        private void ApplyEffect(in RiftStatus status)
        {
            bool captured = status.State == RiftState.Captured;
            bool visionOn = captured && status.ActiveEffect == RiftEffect.TeamVision
                            && status.OwnerTeam >= 0;

            // チーム視界: 制圧中のみ外部視界源を登録、それ以外は解除。
            if (visionOn)
                FogOfWarDirector.SetExternalSource(this, RiftPos.x, RiftPos.z, VisionRadius,
                    IndexToTeam(status.OwnerTeam));
            else
                FogOfWarDirector.RemoveExternalSource(this);

            if (!captured || status.OwnerTeam < 0) return;
            TeamId owner = IndexToTeam(status.OwnerTeam);

            if (status.ActiveEffect == RiftEffect.TeamHaste)
                ApplyTeamHaste(owner);
            else if (status.ActiveEffect == RiftEffect.Shortcut)
                ApplyShortcut(owner);
        }

        // 制圧チーム全チャンピオンへ短時間の加速を毎フレーム上書き付与（実質、制圧中ずっと加速）。
        private void ApplyTeamHaste(TeamId owner)
        {
            foreach (var go in OwnerChampions(owner))
                StatusEffectController.GetOrAdd(go).ApplyHaste(HasteStrength, 0.5f);
        }

        // 入口ゾーンに入った制圧チームのユニットを出口へ転送（ユニット毎クールダウン）。
        private void ApplyShortcut(TeamId owner)
        {
            float now = Time.time;
            foreach (var go in OwnerChampions(owner))
            {
                if (!InZone(go.transform.position)) continue;
                int id = go.GetInstanceID();
                if (_shortcutReadyAt.TryGetValue(id, out float ready) && now < ready) continue;
                _shortcutReadyAt[id] = now + ShortcutPerUnitCd;
                Teleport(go, ShortcutExit);
            }
        }

        private static void Teleport(GameObject go, Vector3 dest)
        {
            var cc = go.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                go.transform.position = dest;
                cc.enabled = true;
            }
            else
            {
                go.transform.position = dest;
            }
        }

        private IEnumerable<GameObject> OwnerChampions(TeamId owner)
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                var tag = pc.GetComponentInParent<TeamTag>();
                if (tag != null && tag.Team == owner) yield return pc.gameObject;
            }
            foreach (var ai in FindObjectsByType<EnemyChampionAI>(FindObjectsSortMode.None))
            {
                var tag = ai.GetComponentInParent<TeamTag>();
                if (tag != null && tag.Team == owner) yield return ai.gameObject;
            }
        }

        // ── 見た目 ──────────────────────────────────────────────

        private void BuildVisuals()
        {
            _portalMat = MakeUnlit(ColorForState(RiftState.Dormant));
            _exitMat   = MakeUnlit(new Color(0.6f, 0.9f, 1f));

            _portal = BuildPortal("RiftPortal", RiftPos, _portalMat, out _portalLight);
            _exitPortal = BuildPortal("RiftExit", ShortcutExit, _exitMat, out _);
            _exitPortal.SetActive(false);
        }

        private static GameObject BuildPortal(string name, Vector3 pos, Material mat, out Light light)
        {
            var root = new GameObject(name);
            root.transform.position = pos;

            // 地面の円盤
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, -1.0f, 0f);
            disc.transform.localScale = new Vector3(ZoneRadius * 2f, 0.05f, ZoneRadius * 2f);
            StripCollider(disc);
            disc.GetComponent<Renderer>().sharedMaterial = mat;

            // 立ち上がる細いビーム
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Beam";
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            beam.transform.localScale = new Vector3(0.6f, 2.5f, 0.6f);
            StripCollider(beam);
            beam.GetComponent<Renderer>().sharedMaterial = mat;

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 2.5f;

            return root;
        }

        private static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        private static Material MakeUnlit(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh);
            m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }

        private void ApplyVisuals(in RiftStatus status)
        {
            bool dormant = status.State == RiftState.Dormant;
            if (_portal.activeSelf == dormant) _portal.SetActive(!dormant);

            Color c = ColorForState(status.State, status.OwnerTeam);
            if (_portalMat != null) { _portalMat.SetColor("_BaseColor", c); _portalMat.color = c; }
            if (_portalLight != null)
            {
                _portalLight.color = c;
                // Warning と Open は脈動させる。
                float pulse = (status.State == RiftState.Warning || status.State == RiftState.Open)
                    ? 1.5f + Mathf.PingPong(Time.time * 2f, 1.5f) : 2.5f;
                _portalLight.intensity = pulse;
            }

            // ショートカット出口は Shortcut 制圧中のみ表示。
            bool exitOn = status.State == RiftState.Captured && status.ActiveEffect == RiftEffect.Shortcut;
            if (_exitPortal.activeSelf != exitOn) _exitPortal.SetActive(exitOn);
        }

        private static Color ColorForState(RiftState state, int ownerTeam = -1)
        {
            switch (state)
            {
                case RiftState.Warning:  return new Color(1f, 0.8f, 0.2f);   // 橙
                case RiftState.Open:     return new Color(0.4f, 0.9f, 1f);   // シアン
                case RiftState.Captured: return ownerTeam == 1
                    ? new Color(1f, 0.4f, 0.35f)                              // 赤
                    : new Color(0.45f, 0.6f, 1f);                            // 青
                case RiftState.Cooldown: return new Color(0.4f, 0.4f, 0.45f);// 灰
                default:                 return new Color(0.5f, 0.5f, 0.6f);
            }
        }

        private void AnnounceTransitions(in RiftStatus status)
        {
            if (status.State == _lastState) return;
            if (_hud == null) _hud = FindFirstObjectByType<GameHudController>();
            if (_hud == null) return;

            if (status.State == RiftState.Open)
            {
                _hud.AnnounceSpecial("次元リフト出現！", new Color(0.4f, 0.9f, 1f));
            }
            else if (status.State == RiftState.Captured)
            {
                string team = status.OwnerTeam == 1 ? "赤" : "青";
                _hud.AnnounceSpecial($"{team}チームが次元リフトを制圧！（{EffectName(status.ActiveEffect)}）",
                    status.OwnerTeam == 1 ? new Color(1f, 0.4f, 0.35f) : new Color(0.45f, 0.6f, 1f));
            }
        }

        private static string EffectName(RiftEffect e)
        {
            switch (e)
            {
                case RiftEffect.Shortcut:   return "ショートカット";
                case RiftEffect.TeamVision: return "チーム視界";
                case RiftEffect.TeamHaste:  return "チーム加速";
                default:                    return "";
            }
        }

        private static TeamId IndexToTeam(int idx) => idx == 1 ? TeamId.Red : TeamId.Blue;
    }
}
