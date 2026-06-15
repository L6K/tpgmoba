using System.IO;
using UnityEngine;
using UnityEditor;
using Enigma.Ability;
using Enigma.Character;
using Enigma.Data;

namespace Enigma.EditorTools
{
    // characters.json を正として CharacterData / SkillDefinition / CharacterDatabase の
    // ScriptableObject アセットを作成・更新するインポータ。
    // 既存アセットは GUID を維持するため LoadAssetAtPath で取得して上書き更新する。
    public static class ImportCharacterRoster
    {
        private const string JsonPath      = "Assets/_Project/Data/characters.json";
        private const string CharactersDir = "Assets/_Project/Data/Characters";
        private const string SkillsDir     = "Assets/_Project/Data/Skills";
        private const string DatabasePath  = "Assets/_Project/Data/Characters/CharacterDatabase.asset";

        // JSON のスキルは [Q, E, R] の順。既存アセットのファイル名サフィックスは Q/W/E のため位置で対応付ける
        private static readonly string[] _slotSuffixes = { "Q", "W", "E" };

        [MenuItem("Enigma/Import Character Roster")]
        public static void Execute()
        {
            string fullJsonPath = Path.Combine(Directory.GetCurrentDirectory(), JsonPath);
            if (!File.Exists(fullJsonPath))
            {
                Debug.LogError($"[Enigma] characters.json が見つかりません: {JsonPath}");
                return;
            }

            CharacterRoster.ParsedCharacter[] parsed;
            try
            {
                parsed = CharacterRoster.Parse(File.ReadAllText(fullJsonPath));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Enigma] characters.json のバリデーションに失敗しました: {e.Message}");
                return;
            }

            EnsureFolder(CharactersDir);
            EnsureFolder(SkillsDir);

            int created = 0;
            int updated = 0;
            var orderedCharacters = new CharacterData[parsed.Length];

            for (int i = 0; i < parsed.Length; i++)
            {
                var pc = parsed[i];

                // 既存キャラが現在参照しているスキルアセットを GUID 維持のまま上書きするため先に解決する。
                // zeph のように Skill_{id}_{slot} 命名でない既存アセットも、参照経由で正しく拾える
                string charPath = $"{CharactersDir}/Char_{pc.id}.asset";
                var existingChar = AssetDatabase.LoadAssetAtPath<CharacterData>(charPath);
                var existingSkills = existingChar != null ? existingChar.Skills : null;

                // --- スキル（位置で Q/W/E にマップ）---
                var skillAssets = new SkillDefinition[4];
                for (int s = 0; s < pc.skills.Length && s < 3; s++)
                {
                    var sk = pc.skills[s];

                    // 1) 既存キャラが当該スロットで参照中のアセットを最優先（GUID 維持）
                    var skill = existingSkills != null && s < existingSkills.Length ? existingSkills[s] : null;
                    string skillPath = skill != null ? AssetDatabase.GetAssetPath(skill) : null;

                    // 2) 参照が無ければ命名規約のパスをロード、それも無ければ新規作成
                    if (skill == null)
                    {
                        skillPath = $"{SkillsDir}/Skill_{pc.id}_{_slotSuffixes[s]}.asset";
                        skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(skillPath);
                    }
                    bool isNewSkill = skill == null;
                    if (isNewSkill)
                        skill = ScriptableObject.CreateInstance<SkillDefinition>();

                    skill.SkillName       = sk.name;
                    skill.Description      = sk.description;
                    skill.Targeting       = ParseTargeting(sk.targeting);
                    skill.Damage          = sk.damage;
                    skill.Range           = sk.range;
                    skill.Radius          = sk.radius;
                    skill.CooldownSeconds  = sk.cooldown;
                    skill.ProjectileSpeed = sk.projectileSpeed;
                    skill.WindupSeconds   = sk.windup;
                    skill.RecoverySeconds = sk.recovery;
                    skill.StunDuration    = sk.stunDuration;
                    skill.RootDuration    = sk.rootDuration;
                    skill.SlowStrength    = sk.slowStrength;
                    skill.SlowDuration    = sk.slowDuration;
                    skill.ShieldAmount    = sk.shieldAmount;
                    skill.ShieldDuration  = sk.shieldDuration;
                    skill.HealAmount      = sk.healAmount;
                    skill.DashDistance    = sk.dashDistance;

                    if (isNewSkill)
                    {
                        AssetDatabase.CreateAsset(skill, skillPath);
                        created++;
                    }
                    else
                    {
                        EditorUtility.SetDirty(skill);
                        updated++;
                    }

                    skillAssets[s] = skill;
                }

                // --- キャラクター本体 ---
                var character = existingChar;
                bool isNewChar = character == null;
                if (isNewChar)
                    character = ScriptableObject.CreateInstance<CharacterData>();

                character.CharId         = pc.id;
                character.DisplayName    = pc.name;
                character.Role           = MapRole(pc.role);
                character.RoleLabelRaw   = pc.role;
                character.Description    = pc.reference;
                character.Theme          = pc.theme;
                character.OwnedByDefault = pc.ownedByDefault;
                character.BaseHp         = pc.baseHp;
                character.HpPerLevel     = pc.hpPerLevel;
                character.MoveSpeed      = pc.moveSpeed;
                character.AttackDamage   = pc.attackDamage;
                character.AttackRange    = pc.attackRange;
                character.AttackCooldown = pc.attackCooldown;
                character.ModelName      = pc.model;
                WireModelAssets(character, pc.model);
                var tint = CharacterRoster.ParseColor(pc.tintColor);
                character.TintColor      = tint;
                character.ThemeColor     = tint;
                character.Skills         = skillAssets;

                if (isNewChar)
                {
                    AssetDatabase.CreateAsset(character, charPath);
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(character);
                    updated++;
                }

                orderedCharacters[i] = character;
            }

            // --- CharacterDatabase（6体を JSON 順で登録）---
            var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
            bool isNewDb = db == null;
            if (isNewDb)
            {
                db = ScriptableObject.CreateInstance<CharacterDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
                created++;
            }
            else
            {
                updated++;
            }

            db.Characters.Clear();
            db.Characters.AddRange(orderedCharacters);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Enigma] Character Roster インポート完了: 作成 {created} 件 / 更新 {updated} 件（キャラ {parsed.Length} 体）");
        }

        private const string ChampionsDir = "Assets/External/Champions";

        // 試合用 3D モデルの結線。Champ_{model}.fbx のプレハブと、その FBX サブアセットの
        // Idle/Walk AnimationClip を CharacterData に紐付ける。
        // "UnityChan"（特別扱い）や空モデル、FBX 不在時は全て null のまま（フォールバック=UnityChan）。
        private static void WireModelAssets(CharacterData character, string model)
        {
            character.ModelPrefab      = null;
            character.IdleClip         = null;
            character.WalkClip         = null;
            character.RunClip          = null;
            character.AttackClip       = null;
            character.AttackClips      = System.Array.Empty<AnimationClip>();
            character.IdleVariantClips = System.Array.Empty<AnimationClip>();
            character.BodyTexture      = null;

            if (string.IsNullOrEmpty(model) || model == "UnityChan") return;

            // FBX リネームで内部のテクスチャ参照が切れているため、命名規約で明示結線する
            character.BodyTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{ChampionsDir}/Champ_{model}_Texture.png");

            string fbxPath = $"{ChampionsDir}/Champ_{model}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Enigma] モデル FBX が見つかりません（UnityChan フォールバック）: {fbxPath}");
                return;
            }
            character.ModelPrefab = prefab;

            // FBX サブアセットの AnimationClip を名前部分一致で拾う（__preview__ 除外）。
            // 無ければ先頭クリップ（Idle）/ null（Walk）。
            // 攻撃クリップは武器攻撃を最優先（Sword/Dagger/Staff/Bow → 汎用 Attack → Punch/Slash/Shoot/Spell）。
            // "Idle" と被弾リアクション("Recieve"/"Receive"/"Hit")を含むクリップは攻撃候補から除外する
            // （Quaternius の "RecieveHit_Attacking" や "Attack_Idle" の誤検出対策）。
            string[] attackPriority =
            {
                "Sword_Attack", "Dagger_Attack", "Staff_Attack", "Bow_Attack",
                "Attack", "Punch", "Slash", "Shoot", "Spell",
            };
            AnimationClip first  = null;
            AnimationClip idle   = null;
            AnimationClip walk   = null;
            // Run は "Run_Weapon" を最優先、無ければ "Run"。"Gun" 等の誤マッチを避けるため
            // 単語境界（"Run" の直後が英字でない or 末尾）でのみ採用する。
            AnimationClip runWeapon = null;
            AnimationClip run       = null;
            // アイドルバリアント: "Idle" を含むがベース Idle 以外（"Idle_Weapon" 等）を収集
            var idleVariants = new System.Collections.Generic.List<AnimationClip>();
            // 攻撃クリップは優先度順に最大 3 本収集（AttackFast 等を含めて AA コンボ用）
            var attackCandidates = new AnimationClip[attackPriority.Length];
            foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath))
            {
                if (!(sub is AnimationClip clip)) continue;
                if (clip.name.StartsWith("__preview__")) continue;
                if (first == null) first = clip;

                bool isIdle = clip.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isIdle)
                {
                    if (idle == null) idle = clip;            // 最初の Idle をベースに
                    else idleVariants.Add(clip);             // 2 本目以降はバリアント候補
                }

                if (walk == null && clip.name.IndexOf("Walk", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    walk = clip;

                // Run 検出（"Gun"/"Around" 等の部分一致を避ける単語境界判定）
                if (ContainsWord(clip.name, "Run"))
                {
                    if (runWeapon == null &&
                        clip.name.IndexOf("Run_Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        runWeapon = clip;
                    else if (run == null)
                        run = clip;
                }

                bool excluded =
                    isIdle ||
                    clip.name.IndexOf("Recieve", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clip.name.IndexOf("Receive", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clip.name.IndexOf("Hit", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!excluded)
                {
                    for (int p = 0; p < attackPriority.Length; p++)
                    {
                        if (attackCandidates[p] == null &&
                            clip.name.IndexOf(attackPriority[p], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            attackCandidates[p] = clip;
                            break;
                        }
                    }
                }
            }
            character.IdleClip = idle != null ? idle : first;
            character.WalkClip = walk;
            character.RunClip  = runWeapon != null ? runWeapon : run;
            character.IdleVariantClips = idleVariants.ToArray();

            // 優先度順に最大 3 本を AA コンボへ。先頭は従来どおり単発 AttackClip にも入れる。
            var attacks = new System.Collections.Generic.List<AnimationClip>();
            foreach (var candidate in attackCandidates)
            {
                if (candidate == null) continue;
                attacks.Add(candidate);
                if (attacks.Count >= 3) break;
            }
            character.AttackClip  = attacks.Count > 0 ? attacks[0] : null;
            character.AttackClips = attacks.ToArray();

            Debug.Log($"[Enigma] {character.CharId}: attack={(character.AttackClip != null ? character.AttackClip.name : "なし")}" +
                      $" combo={attacks.Count} run={(character.RunClip != null ? character.RunClip.name : "なし")}" +
                      $" idleVariants={idleVariants.Count}");
        }

        // name の中に word が「単語境界付き」で含まれるか（大小無視）。
        // "Gun"/"Around" を "Run" と誤マッチさせないため、直前・直後が英字なら不一致扱いにする。
        private static bool ContainsWord(string name, string word)
        {
            int from = 0;
            while (true)
            {
                int idx = name.IndexOf(word, from, System.StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return false;
                bool leftOk  = idx == 0 || !char.IsLetter(name[idx - 1]);
                int after    = idx + word.Length;
                bool rightOk = after >= name.Length || !char.IsLetter(name[after]);
                if (leftOk && rightOk) return true;
                from = idx + 1;
            }
        }

        private static SkillTargeting ParseTargeting(string value)
        {
            switch (value)
            {
                case "Directional": return SkillTargeting.Directional;
                case "GroundAoe":   return SkillTargeting.GroundAoe;
                case "Targeted":     return SkillTargeting.Targeted;
                case "TargetedAlly": return SkillTargeting.TargetedAlly;
                default:             return SkillTargeting.Directional;
            }
        }

        // 自由記述ロール（"タンク/ファイター" 等）をキーワードで CharacterRole enum に寄せる。原文は RoleLabelRaw に保持
        private static CharacterRole MapRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return CharacterRole.Fighter;
            if (role.Contains("タンク"))                 return CharacterRole.Tank;
            if (role.Contains("マークスマン"))           return CharacterRole.Marksman;
            if (role.Contains("メイジ"))                 return CharacterRole.Mage;
            if (role.Contains("サポート"))               return CharacterRole.Support;
            if (role.Contains("アサシン") || role.Contains("ファイター")
                || role.Contains("ブルーザー"))          return CharacterRole.Fighter;
            return CharacterRole.Fighter;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
