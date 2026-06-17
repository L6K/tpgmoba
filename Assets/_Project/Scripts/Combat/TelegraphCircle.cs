using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Ability;
using Enigma.Audio;

namespace Enigma.Combat
{
    // 予兆円: delay 経過後に範囲内の IDamageable へダメージを与えて消滅
    public sealed class TelegraphCircle : MonoBehaviour
    {
        private float      _damage;
        private float      _radius;
        private GameObject _owner;
        private float      _stun, _root, _slowStrength, _slowDuration;

        // 予兆演出の状態
        private MeshRenderer          _bodyRenderer;
        private MaterialPropertyBlock _bodyMpb;
        private Color                 _baseColor;
        private LineRenderer          _edgeRing;
        private bool                  _telegraphing;

        // 外周エッジリングの分割数と回転速度
        private const int   EdgeSegments    = 48;
        private const float EdgeSpinDegrees = 35f;
        // 本体塗りのパルス範囲(アルファ)
        private const float PulseMinAlpha = 0.35f;
        private const float PulseMaxAlpha = 0.6f;
        private const float PulseHz       = 2.2f;

        public void Init(float radius, float delaySeconds, float damage, GameObject owner)
        {
            _radius  = radius;
            _damage  = damage;
            _owner   = owner;

            // 直径をスケールに反映（薄い円柱プレハブ前提）
            transform.localScale = new Vector3(radius * 2f, transform.localScale.y, radius * 2f);

            SetupTelegraphVisual();

            StartCoroutine(ExplodeAfter(delaySeconds));
        }

        public void SetStatusEffects(float stun, float root, float slowStrength, float slowDuration)
        {
            _stun         = stun;
            _root         = root;
            _slowStrength = slowStrength;
            _slowDuration = slowDuration;
        }

        private void ApplyStatusTo(GameObject go)
        {
            if (_stun <= 0f && _root <= 0f && _slowStrength <= 0f) return;
            var sc = StatusEffectController.GetOrAdd(go);
            if (sc == null) return;
            if (_stun > 0f) sc.ApplyStun(_stun);
            if (_root > 0f) sc.ApplyRoot(_root);
            if (_slowStrength > 0f && _slowDuration > 0f) sc.ApplySlow(_slowStrength, _slowDuration);
        }

        // 本体塗り円の MPB と、明るい同色の外周エッジリングを組み立てる。
        // 本体は子(localScale)に依存せず、エッジリングは実半径で world 描画する。
        private void SetupTelegraphVisual()
        {
            _bodyRenderer = GetComponentInChildren<MeshRenderer>();
            if (_bodyRenderer != null)
            {
                _bodyMpb = new MaterialPropertyBlock();
                _bodyRenderer.GetPropertyBlock(_bodyMpb);
                // 本体マテリアルの基準色を取得（無ければ既定の予兆色）
                var mat = _bodyRenderer.sharedMaterial;
                _baseColor = (mat != null && mat.HasProperty("_BaseColor"))
                    ? mat.GetColor("_BaseColor")
                    : new Color(1f, 0.35f, 0.2f, 1f);
            }
            else
            {
                _baseColor = new Color(1f, 0.35f, 0.2f, 1f);
            }

            // 明るい同色エッジリング（細リング1本）。親スケールの影響を避けるため別 GO + world 空間で描く
            var ringGo = new GameObject("TelegraphEdge");
            ringGo.transform.SetParent(transform, worldPositionStays: true);
            ringGo.transform.position      = transform.position;
            // ローカル Z をワールド上向きへ回し、TransformZ 整列のリボンを地面に寝かせる
            ringGo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            // 親(本体)は radius*2 のスケールを持つため、それを打ち消して
            // 1 ローカル単位 = 1 ワールド単位にし、円を実半径で描けるようにする
            var ps = transform.localScale;
            ringGo.transform.localScale = new Vector3(
                ps.x != 0f ? 1f / ps.x : 1f,
                ps.y != 0f ? 1f / ps.y : 1f,
                ps.z != 0f ? 1f / ps.z : 1f);

            _edgeRing = ringGo.AddComponent<LineRenderer>();
            _edgeRing.useWorldSpace     = false;
            _edgeRing.loop              = true;
            _edgeRing.positionCount     = EdgeSegments;
            _edgeRing.numCapVertices    = 0;
            _edgeRing.startWidth        = 0.12f;
            _edgeRing.endWidth          = 0.12f;
            _edgeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _edgeRing.receiveShadows    = false;
            // 地面に水平に寝かせる（円が円柱本体の縁に沿うよう GO の向きへ従わせる）
            _edgeRing.alignment         = LineAlignment.TransformZ;

            // 明るい同色（加算寄り）。SkillVfx と同じ透過マテリアルキャッシュを共用
            var edgeColor = _baseColor * 1.6f;
            edgeColor.a   = 0.9f;
            _edgeRing.sharedMaterial = SkillVfx.GetTelegraphMaterial(edgeColor);
            _edgeRing.startColor     = edgeColor;
            _edgeRing.endColor       = edgeColor;

            // 単位円を実半径で XY 平面に敷く（GO は -90°X 回転済みなので地面に水平になる）。
            // Z をわずかに持ち上げて地面とのZファイトを避ける（回転後にワールド上向き）
            for (int i = 0; i < EdgeSegments; i++)
            {
                float a = (float)i / EdgeSegments * Mathf.PI * 2f;
                _edgeRing.SetPosition(i, new Vector3(Mathf.Cos(a) * _radius, Mathf.Sin(a) * _radius, 0.06f));
            }

            _telegraphing = true;
        }

        private void Update()
        {
            if (!_telegraphing) return;

            // エッジリングをゆっくり回転（緩やかな自転）。GO は -90°X 済みで
            // ローカル Z がワールド上向きのため、Z 軸回りに回す
            if (_edgeRing != null)
                _edgeRing.transform.Rotate(0f, 0f, EdgeSpinDegrees * Time.deltaTime, Space.Self);

            // 本体塗り円のアルファを 0.35〜0.6 でパルス
            if (_bodyRenderer != null && _bodyMpb != null)
            {
                float pulse = Mathf.Lerp(PulseMinAlpha, PulseMaxAlpha,
                    0.5f + 0.5f * Mathf.Sin(Time.time * PulseHz * Mathf.PI * 2f));
                var c = _baseColor;
                c.a = pulse;
                _bodyRenderer.GetPropertyBlock(_bodyMpb);
                _bodyMpb.SetColor("_BaseColor", c);
                _bodyMpb.SetColor("_Color", c);
                _bodyRenderer.SetPropertyBlock(_bodyMpb);
            }
        }

        private IEnumerator ExplodeAfter(float delay)
        {
            yield return new WaitForSeconds(delay);

            // 起爆演出: バースト + 衝撃波リング + 光柱。予兆の更新は止める
            _telegraphing = false;
            if (_edgeRing != null) Destroy(_edgeRing.gameObject);

            var burstColor = _baseColor;
            burstColor.a = 1f;
            SkillVfx.SpawnBurst(transform.position, burstColor, _radius * 0.5f, _radius * 2.2f, 0.35f);
            SkillVfx.SpawnRing(transform.position, burstColor, _radius * 0.6f, _radius * 1.4f, 0.4f);
            SkillVfx.SpawnPillar(transform.position, burstColor, _radius * 0.55f, _radius * 2.2f, 0.45f);
            GameSfx.Play("skill_e_blast", transform.position, 0.9f);

            // 中心から半径内にある全コライダーを取得してダメージ。
            // CharacterController + CapsuleCollider のような複数コライダー持ちに
            // 多重ヒットしないよう IDamageable 単位で重複排除する
            var hits = Physics.OverlapSphere(transform.position, _radius);
            var damaged = new HashSet<IDamageable>();
            foreach (var col in hits)
            {
                if (_owner != null && col.gameObject == _owner) continue;

                // 味方には地点 AoE のダメージを与えない
                if (!TeamRules.CanDamage(ResolveTeam(_owner), ResolveTeam(col.gameObject)))
                    continue;

                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null && damaged.Add(damageable))
                {
                    float finalDamage = DamageUtility.ApplyTeamBuff(_damage, _owner, col.gameObject);
                    if (damageable is HealthComponent hc)
                        hc.TakeDamage(finalDamage, _owner);
                    else
                        damageable.TakeDamage(finalDamage);

                    if (damageable is HealthComponent hcTarget)
                        ApplyStatusTo(hcTarget.gameObject);
                }
            }

            // 演出のために少し待ってから消滅
            yield return new WaitForSeconds(0.15f);
            Destroy(gameObject);
        }

        // TeamTag が無い側は中立扱い（誰にでも当たる）。
        private static TeamId ResolveTeam(GameObject go)
        {
            if (go == null) return TeamId.Neutral;
            var tag = go.GetComponentInParent<TeamTag>();
            return tag != null ? tag.Team : TeamId.Neutral;
        }
    }
}
