using UnityEngine;

namespace Enigma.Character
{
    /// <summary>
    /// 試合開始時、ピックしたキャラの 3D モデル（CharacterData.ModelPrefab）を
    /// プレイヤーの見た目として差し替えるランタイムユーティリティ。
    ///
    /// ゲームロジック（CharacterController / HealthComponent / SkillCaster /
    /// PlayerController 等）には一切触れず、見た目の子オブジェクトのみを操作する。
    /// UnityChan（ModelName="UnityChan" / 空 / ModelPrefab=null）は既存の
    /// "UnityChanModel" 子をそのまま維持し、何もしない（フォールバック）。
    /// </summary>
    public static class ChampionModelSwapper
    {
        private const string UnityChanModelName = "UnityChanModel";
        private const string ChampionModelName  = "ChampionModel";

        // プレイヤーピボットは地上 +1.05m のカプセル中心にある。
        // モデルのピボット（足元）をそこから 1.05m 下げると接地する。
        private const float GroundOffsetY  = -1.05f;
        private const float TargetHeight   = 1.5f;   // 高さ正規化のターゲット（m）
        private const float RampSmoothing  = 0.18f;

        private static readonly Color ShadeColor = new Color(0.58f, 0.62f, 0.80f, 1f);

        /// <summary>
        /// player の見た目を data のモデルへ差し替える。
        /// 戻り値は生成したモデル GameObject。維持（何もしなかった）場合は null。
        /// </summary>
        public static GameObject Apply(GameObject player, CharacterData data)
        {
            if (player == null || data == null) return null;

            // UnityChan / 空 / プレハブ未結線 → 既存モデル維持（フォールバック）
            if (string.IsNullOrEmpty(data.ModelName) ||
                data.ModelName == "UnityChan" ||
                data.ModelPrefab == null)
                return null;

            // 既存ユニティちゃんを無効化（破棄せず保持）
            var existing = player.transform.Find(UnityChanModelName);
            if (existing != null) existing.gameObject.SetActive(false);

            // モデル生成
            var model = Object.Instantiate(data.ModelPrefab);
            model.name = ChampionModelName;
            model.transform.SetParent(player.transform, false);
            model.transform.localRotation = Quaternion.identity;

            StripGameplayComponents(model);
            NormalizeHeightAndGround(model);
            ApplyToonMaterials(model, data);
            var switcher = SetupLocomotion(model, player, data);
            RewireAttackMotor(player, model.transform, switcher);
            ReparentMuzzleToHand(player, model.transform);

            return model;
        }

        // スワップ後モデルの右手ボーンへ player の "Muzzle" を付け替える（ビーム発射点を手元へ）。
        // 名前候補（大小無視）: "RightHand" / "Hand.R" / "HandR" / "Hand_R"。見つからなければ現状維持。
        private static void ReparentMuzzleToHand(GameObject player, Transform modelRoot)
        {
            var muzzle = player.transform.Find("Muzzle");
            if (muzzle == null) return;

            Transform hand = null;
            Transform fallback = null;
            foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("hand")) continue;
                if (n.Contains("righthand")) { hand = t; break; }
                if (fallback == null &&
                    (n.Contains("hand.r") || n.Contains("handr") || n.Contains("hand_r")))
                    fallback = t;
            }
            hand ??= fallback;
            if (hand == null) return;

            muzzle.SetParent(hand, false);
            muzzle.localPosition = Vector3.zero;
        }

        // モデル付属の物理・当たり判定・制御スクリプトを除去（Animator は残す）。
        // RequireComponent(Rigidbody) を持つ MonoBehaviour を先に消さないと Rigidbody を除去できない。
        private static void StripGameplayComponents(GameObject model)
        {
            foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Object.Destroy(mb);
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true))
                Object.Destroy(rb);
            foreach (var col in model.GetComponentsInChildren<Collider>(true))
                Object.Destroy(col);

            // Quaternius 等の FBX プレハブは Animator コンポーネントを持たないことがある。
            // Playables 再生(LocomotionClipSwitcher)には Animator が必須のため、無ければ付与する
            // (Generic クリップはトランスフォームパスでバインドされるため Avatar なしでも再生できる)
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false; // 移動は CharacterController が担う
        }

        // FBX ルートの単位変換スケールは上書きせず、bounds 計測 → 相対乗算で高さ正規化。
        // 接地は bounds 最下端をプレイヤーローカル GroundOffsetY に合わせる。
        private static void NormalizeHeightAndGround(GameObject model)
        {
            // 一旦原点へ置いてからワールド bounds を計測（親は player、localPosition=0 起点）
            model.transform.localPosition = Vector3.zero;

            if (!TryGetWorldBounds(model, out var bounds)) return;

            // --- 高さ正規化（相対乗算: スケールを上書きしない）---
            float height = bounds.size.y;
            if (height > 0.0001f)
            {
                float factor = TargetHeight / height;
                model.transform.localScale *= factor;
                // スケール変更後に bounds を取り直す（接地計算のため）
                if (!TryGetWorldBounds(model, out bounds)) return;
            }

            // --- 接地補正 ---
            // 現在のローカル基準（GroundOffsetY）に、モデル最下端のローカル相当ズレを足し込む。
            // bounds.min.y はワールド値。親（player）のワールド Y を引いてローカルへ写す。
            float parentWorldY = model.transform.parent != null
                ? model.transform.parent.position.y
                : 0f;
            float footLocalY = bounds.min.y - parentWorldY;
            // footLocalY を GroundOffsetY に合わせるよう y をオフセット
            float deltaY = GroundOffsetY - footLocalY;
            var lp = model.transform.localPosition;
            lp.y += deltaY;
            model.transform.localPosition = lp;
        }

        private static bool TryGetWorldBounds(GameObject model, out Bounds bounds)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { bounds = default; return false; }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        // 全 Renderer を Enigma/Toon 化。元テクスチャを _BaseMap へ引き継ぐ。
        // ランタイム生成マテリアルはモデルごとに1回だけ生成して共有（リーク抑制）。
        // 試合終了でシーンごと破棄されるため Destroy は不要。
        private static void ApplyToonMaterials(GameObject model, CharacterData data)
        {
            var toon = Shader.Find("Enigma/Toon");
            if (toon == null) return;

            // (元マテリアル → 生成済みトゥーンマテリアル) のローカルキャッシュ。
            // 同一モデル内で同じソースを共有するため、ここで1回だけ生成する。
            var cache = new System.Collections.Generic.Dictionary<Material, Material>();

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;

                    if (!cache.TryGetValue(src, out var dst))
                    {
                        dst = new Material(toon) { name = $"Toon_{src.name}_Runtime" };
                        // FBX リネームで内部参照が切れるため、CharacterData の明示テクスチャを優先
                        var tex = data.BodyTexture != null ? (Texture)data.BodyTexture : src.mainTexture;
                        if (tex != null) dst.SetTexture("_BaseMap", tex);
                        dst.SetColor("_BaseColor", Color.white);
                        dst.SetColor("_ShadeColor", ShadeColor);
                        dst.SetFloat("_RampSmoothing", RampSmoothing);
                        cache[src] = dst;
                    }
                    mats[i] = dst;
                }
                r.sharedMaterials = mats;
            }
        }

        // LocomotionClipSwitcher を付与して Idle/Walk を結線。
        // CharacterController はプレイヤー側のものを渡す（velocity で歩行判定）。
        private static LocomotionClipSwitcher SetupLocomotion(GameObject model, GameObject player, CharacterData data)
        {
            var switcher = model.AddComponent<LocomotionClipSwitcher>();
            var controller = player.GetComponent<CharacterController>();
            // IdleClip 未結線時は何も再生されないため、最低限 Idle が無いと静止する点に留意
            switcher.Configure(data.IdleClip, data.WalkClip, data.AttackClip, controller);
            return switcher;
        }

        // PlayerAttackMotor の _modelRoot を新モデルへ差し替え、攻撃アニメ用 switcher も結線する（存在する場合のみ）。
        private static void RewireAttackMotor(GameObject player, Transform modelRoot, LocomotionClipSwitcher switcher)
        {
            var motor = player.GetComponent<PlayerAttackMotor>();
            if (motor == null) return;
            motor.SetModelRoot(modelRoot);
            motor.SetClipSwitcher(switcher);
        }
    }
}
