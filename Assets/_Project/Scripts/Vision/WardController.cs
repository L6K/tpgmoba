using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Enigma.Combat;
using Enigma.Character;

namespace Enigma.Vision
{
    /// <summary>
    /// 操作プレイヤーのワード(設置型偵察)を司る Humble Object。マップシーンで自動生成し、
    /// G キーでカーソル地点にワードを設置する。寿命/本数は <see cref="WardVisionModel"/>(純ロジック)が管理し、
    /// アクティブワードを <see cref="FogOfWarDirector"/> の外部視界源として登録する。
    /// ビルダー/シーンを改変せず動かすため自動生成方式(FoW/中央オブジェクトと同様)。
    /// </summary>
    public sealed class WardController : MonoBehaviour
    {
        private const Key  WardKey       = Key.G;
        private const float MarkerFootDrop = 1.0f; // カーソル平面(プレイヤー腰高)→足元へ下げる

        public static WardController Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (SceneManager.GetActiveScene().name != "AetherRift_Map") return;
            if (FindObjectOfType<WardController>() != null) return;
            new GameObject("WardController").AddComponent<WardController>();
        }

        private readonly WardVisionModel _model = new WardVisionModel(maxActivePerTeam: 3, defaultLifetime: 90f, defaultVisionRadius: 12f);
        private readonly Dictionary<int, GameObject> _markers = new Dictionary<int, GameObject>();

        private Transform _player;
        private TeamId    _playerTeam = TeamId.Neutral;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>現在アクティブなワード一覧（ミニマップ表示用）。</summary>
        public IReadOnlyList<Ward> ActiveWards() => _model.ActiveWards();

        private void Update()
        {
            _model.Tick(Time.deltaTime);
            SyncMarkers();

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!ResolvePlayer()) return;

            if (keyboard[WardKey].wasPressedThisFrame && TryGetGroundCursor(out var ground))
            {
                PlaceWard(ground);
            }
        }

        private bool ResolvePlayer()
        {
            if (_player != null) return true;
            var pc = FindObjectOfType<PlayerController>();
            if (pc == null) return false;
            _player = pc.transform;
            var tag = pc.GetComponentInParent<TeamTag>();
            _playerTeam = tag != null ? tag.Team : TeamId.Neutral;
            return true;
        }

        // カーソル→地面(プレイヤー腰高の水平面)の交点。SkillCaster と同方式。
        private bool TryGetGroundCursor(out Vector3 point)
        {
            point = Vector3.zero;
            var cam = Camera.main;
            var mouse = Mouse.current;
            if (cam == null || mouse == null || _player == null) return false;

            var ray = cam.ScreenPointToRay(new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, 0f));
            var plane = new Plane(Vector3.up, new Vector3(0f, _player.position.y, 0f));
            if (!plane.Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        private void PlaceWard(Vector3 ground)
        {
            int team = (int)_playerTeam;
            var ward = _model.Place(team, ground.x, ground.z, Time.time);

            // 既存マーカーが上限超過で落ちている場合は SyncMarkers が掃除する。新規マーカー生成。
            float footY = ground.y - MarkerFootDrop;
            var marker = BuildMarker(new Vector3(ground.x, footY, ground.z), _playerTeam);
            marker.name = "Ward_" + ward.Id;
            _markers[ward.Id] = marker;

            // FoW へ味方視界源として登録（位置静止なので設置時に一度）。key はマーカー GO。
            FogOfWarDirector.SetExternalSource(marker, ward.X, ward.Z, ward.VisionRadius, _playerTeam);

            Audio.GameSfx.Play("skill_e_fire", marker.transform.position, 0.6f);
        }

        // モデルのアクティブワードとマーカー辞書を突き合わせ、寿命切れ/破壊分を撤去する。
        private static readonly List<int> _expired = new List<int>();
        private void SyncMarkers()
        {
            if (_markers.Count == 0) return;
            _expired.Clear();
            foreach (var kv in _markers)
            {
                bool alive = false;
                var active = _model.ActiveWards();
                for (int i = 0; i < active.Count; i++)
                    if (active[i].Id == kv.Key) { alive = true; break; }
                if (!alive) _expired.Add(kv.Key);
            }
            for (int i = 0; i < _expired.Count; i++)
            {
                int id = _expired[i];
                if (_markers.TryGetValue(id, out var go) && go != null)
                {
                    FogOfWarDirector.RemoveExternalSource(go);
                    Destroy(go);
                }
                _markers.Remove(id);
            }
        }

        // ポール + 発光オーブのワードマーカーを手続き生成。
        private static GameObject BuildMarker(Vector3 footPos, TeamId team)
        {
            var root = new GameObject("WardMarker");
            root.transform.position = footPos;

            // ポール（細い暗色シリンダー、高さ約1m）
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            var pc = post.GetComponent<Collider>(); if (pc != null) Object.Destroy(pc);
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            post.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
            SetUnlitColor(post, new Color(0.12f, 0.12f, 0.14f));

            // 発光オーブ（明るいチーム色の眼）
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Orb";
            var oc = orb.GetComponent<Collider>(); if (oc != null) Object.Destroy(oc);
            orb.transform.SetParent(root.transform, false);
            orb.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            orb.transform.localScale = Vector3.one * 0.45f;
            var orbColor = team == TeamId.Red ? new Color(1f, 0.45f, 0.4f) : new Color(0.4f, 0.95f, 1f);
            SetUnlitColor(orb, orbColor);

            // 発光感のための小ライト
            var lightGo = new GameObject("WardLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = orbColor; l.range = 5f; l.intensity = 2.5f; l.shadows = LightShadows.None;

            return root;
        }

        // 描画実績のある URP/Unlit 不透明・明色（自前の加算/半透明構築は不可視になりやすいので opaque）。
        private static void SetUnlitColor(GameObject go, Color color)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mr.sharedMaterial = mat;
        }
    }
}
