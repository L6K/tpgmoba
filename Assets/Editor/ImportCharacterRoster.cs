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
            character.ModelPrefab = null;
            character.IdleClip    = null;
            character.WalkClip    = null;
            character.AttackClip  = null;
            character.BodyTexture = null;

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
            var attackCandidates = new AnimationClip[attackPriority.Length];
            foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath))
            {
                if (!(sub is AnimationClip clip)) continue;
                if (clip.name.StartsWith("__preview__")) continue;
                if (first == null) first = clip;
                if (idle == null && clip.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    idle = clip;
                if (walk == null && clip.name.IndexOf("Walk", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    walk = clip;

                bool excluded =
                    clip.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
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

            AnimationClip attack = null;
            foreach (var candidate in attackCandidates)
                if (candidate != null) { attack = candidate; break; }
            character.AttackClip = attack;

            Debug.Log($"[Enigma] {character.CharId}: attack={(attack != null ? attack.name : "なし")}");
        }

        private static SkillTargeting ParseTargeting(string value)
        {
            switch (value)
            {
                case "Directional": return SkillTargeting.Directional;
                case "GroundAoe":   return SkillTargeting.GroundAoe;
                case "Targeted":    return SkillTargeting.Targeted;
                default:            return SkillTargeting.Directional;
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
