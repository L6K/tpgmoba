using System;
using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Data
{
    // characters.json を正とするキャラクターパラメータのパース＋バリデーション層。
    // ファイル I/O・AssetDatabase に非依存（plain C# + JsonUtility）にすることで EditMode テストから直接検証できる。
    public static class CharacterRoster
    {
        // JsonUtility はトップレベル配列を解せないため characters[] をラップする
        [Serializable]
        public sealed class RosterDto
        {
            public int version;
            public ParsedCharacter[] characters;
        }

        [Serializable]
        public sealed class ParsedCharacter
        {
            public string id;
            public string name;
            public string role;
            public string reference;
            public string theme;
            public bool ownedByDefault;
            public float baseHp;
            public float hpPerLevel;
            public float moveSpeed;
            public float attackDamage;
            public float attackRange;
            public float attackCooldown;
            public string model;
            public string tintColor;
            public ParsedSkill[] skills;
        }

        [Serializable]
        public sealed class ParsedSkill
        {
            public string slot;
            public string name;
            public string targeting;
            public float damage;
            public float range;
            public float radius;
            public float cooldown;
            public float projectileSpeed;
            public float windup;
            public float recovery;
            public string description;
            public float stunDuration;
            public float rootDuration;
            public float slowStrength;
            public float slowDuration;
            public float shieldAmount;
            public float shieldDuration;
            public float healAmount;
            public float dashDistance;
            public float pullDistance;
            public float healPerChampionHit;
            public float healPerMinionHit;
        }

        private static readonly string[] _validSlots = { "Q", "E", "R" };
        private static readonly string[] _validTargetings = { "Directional", "GroundAoe", "Targeted", "TargetedAlly", "SelfAoe", "TeamAlly" };

        /// <summary>
        /// JSON 文字列を検証済みの ParsedCharacter[] へ変換する。
        /// 失敗時は問題箇所を特定できる具体的なメッセージで例外を投げる。
        /// </summary>
        public static ParsedCharacter[] Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("characters.json の中身が空です。");

            RosterDto dto;
            try
            {
                dto = JsonUtility.FromJson<RosterDto>(json);
            }
            catch (Exception e)
            {
                throw new FormatException($"characters.json のパースに失敗しました: {e.Message}");
            }

            if (dto == null || dto.characters == null || dto.characters.Length == 0)
                throw new FormatException("characters 配列が見つからない、または空です。");

            var seenIds = new HashSet<string>();

            for (int i = 0; i < dto.characters.Length; i++)
            {
                var c = dto.characters[i];
                string where = $"characters[{i}]";

                if (c == null || string.IsNullOrWhiteSpace(c.id))
                    throw new FormatException($"{where}: id が空です。");

                where = $"キャラ '{c.id}'";

                if (!seenIds.Add(c.id))
                    throw new FormatException($"id '{c.id}' が重複しています。");

                if (c.baseHp <= 0f)
                    throw new FormatException($"{where}: baseHp は正の値である必要があります（実値 {c.baseHp}）。");

                ValidateColor(c.tintColor, where);

                if (c.skills == null || c.skills.Length < 3)
                    throw new FormatException($"{where}: skills は3つ必要です（実際 {(c.skills?.Length ?? 0)}個）。");

                for (int s = 0; s < c.skills.Length; s++)
                {
                    var sk = c.skills[s];
                    string skWhere = $"{where} のスキル[{s}]";

                    if (sk == null)
                        throw new FormatException($"{skWhere}: スキル定義が null です。");

                    if (Array.IndexOf(_validSlots, sk.slot) < 0)
                        throw new FormatException($"{skWhere}: slot '{sk.slot}' は不正です（許可: Q/E/R）。");

                    if (Array.IndexOf(_validTargetings, sk.targeting) < 0)
                        throw new FormatException(
                            $"{skWhere}: targeting '{sk.targeting}' は不正です（許可: Directional/GroundAoe/Targeted/TargetedAlly/SelfAoe/TeamAlly）。");
                }
            }

            return dto.characters;
        }

        /// <summary>"#RRGGBB" を Color へ変換する。ColorUtility 非依存（自前16進パース）。</summary>
        public static Color ParseColor(string hex)
        {
            ValidateColor(hex, "tintColor");

            int r = HexPair(hex, 1);
            int g = HexPair(hex, 3);
            int b = HexPair(hex, 5);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private static void ValidateColor(string hex, string where)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 7 || hex[0] != '#')
                throw new FormatException($"{where}: tintColor '{hex}' は \"#RRGGBB\" 形式である必要があります。");

            for (int i = 1; i < 7; i++)
            {
                if (HexDigit(hex[i]) < 0)
                    throw new FormatException($"{where}: tintColor '{hex}' に不正な16進文字 '{hex[i]}' が含まれます。");
            }
        }

        private static int HexPair(string hex, int start)
        {
            return HexDigit(hex[start]) * 16 + HexDigit(hex[start + 1]);
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }
}
