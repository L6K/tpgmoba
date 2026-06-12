using System;
using NUnit.Framework;
using UnityEngine;
using Enigma.Data;

namespace Enigma.Tests
{
    public sealed class CharacterRosterTests
    {
        // 1キャラ・3スキルの最小構成。テストごとに差し替えたい箇所だけ書き換える
        private static string ValidJson(
            string id = "zeph",
            string tint = "#7A5CF0",
            string slot2 = "E",
            string targeting2 = "GroundAoe",
            float baseHp = 200f,
            string secondId = null)
        {
            string second = secondId == null ? "" : $@",
    {{
      ""id"": ""{secondId}"", ""name"": ""b"", ""role"": ""r"", ""theme"": ""t"",
      ""ownedByDefault"": false, ""baseHp"": 100, ""tintColor"": ""#FFFFFF"",
      ""skills"": [
        {{ ""slot"": ""Q"", ""targeting"": ""Directional"" }},
        {{ ""slot"": ""E"", ""targeting"": ""GroundAoe"" }},
        {{ ""slot"": ""R"", ""targeting"": ""Targeted"" }}
      ]
    }}";

            return $@"
{{
  ""version"": 1,
  ""characters"": [
    {{
      ""id"": ""{id}"",
      ""name"": ""ゼフ"",
      ""role"": ""メイジ"",
      ""theme"": ""テーマ"",
      ""ownedByDefault"": true,
      ""baseHp"": {baseHp},
      ""hpPerLevel"": 16,
      ""moveSpeed"": 5.5,
      ""attackDamage"": 15,
      ""attackRange"": 12,
      ""attackCooldown"": 1.5,
      ""model"": ""Mage"",
      ""tintColor"": ""{tint}"",
      ""skills"": [
        {{ ""slot"": ""Q"", ""name"": ""q"", ""targeting"": ""Directional"", ""damage"": 32, ""range"": 18, ""cooldown"": 5, ""projectileSpeed"": 32, ""windup"": 0.2, ""recovery"": 0.35 }},
        {{ ""slot"": ""{slot2}"", ""name"": ""e"", ""targeting"": ""{targeting2}"", ""damage"": 45, ""range"": 14, ""radius"": 3.2, ""cooldown"": 9 }},
        {{ ""slot"": ""R"", ""name"": ""r"", ""targeting"": ""Targeted"", ""damage"": 110, ""range"": 13, ""cooldown"": 45 }}
      ]
    }}{second}
  ]
}}";
        }

        [Test]
        public void Parse_ValidJson_ReturnsCharacter()
        {
            var result = CharacterRoster.Parse(ValidJson());
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("zeph", result[0].id);
            Assert.AreEqual(200f, result[0].baseHp, 0.001f);
        }

        [Test]
        public void Parse_ValidJson_ReadsSkillFields()
        {
            var c = CharacterRoster.Parse(ValidJson())[0];
            Assert.AreEqual(3, c.skills.Length);
            Assert.AreEqual("Q", c.skills[0].slot);
            Assert.AreEqual("Directional", c.skills[0].targeting);
            Assert.AreEqual(32f, c.skills[0].damage, 0.001f);
            Assert.AreEqual("Targeted", c.skills[2].targeting);
        }

        [Test]
        public void Parse_TwoCharacters_BothParsed()
        {
            var result = CharacterRoster.Parse(ValidJson(secondId: "garon"));
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("garon", result[1].id);
        }

        [Test]
        public void Parse_DuplicateId_Throws()
        {
            var ex = Assert.Throws<FormatException>(() => CharacterRoster.Parse(ValidJson(secondId: "zeph")));
            StringAssert.Contains("zeph", ex.Message);
            StringAssert.Contains("重複", ex.Message);
        }

        [Test]
        public void Parse_FewerThanThreeSkills_Throws()
        {
            string json = @"
{ ""characters"": [
  { ""id"": ""x"", ""baseHp"": 100, ""tintColor"": ""#FFFFFF"", ""skills"": [
    { ""slot"": ""Q"", ""targeting"": ""Directional"" },
    { ""slot"": ""E"", ""targeting"": ""GroundAoe"" }
  ] }
] }";
            var ex = Assert.Throws<FormatException>(() => CharacterRoster.Parse(json));
            StringAssert.Contains("skills", ex.Message);
        }

        [Test]
        public void Parse_InvalidTargeting_Throws()
        {
            var ex = Assert.Throws<FormatException>(() => CharacterRoster.Parse(ValidJson(targeting2: "Beam")));
            StringAssert.Contains("targeting", ex.Message);
            StringAssert.Contains("Beam", ex.Message);
        }

        [Test]
        public void Parse_InvalidSlot_Throws()
        {
            var ex = Assert.Throws<FormatException>(() => CharacterRoster.Parse(ValidJson(slot2: "W")));
            StringAssert.Contains("slot", ex.Message);
        }

        [Test]
        public void Parse_NonPositiveHp_Throws()
        {
            var ex = Assert.Throws<FormatException>(() => CharacterRoster.Parse(ValidJson(baseHp: 0f)));
            StringAssert.Contains("baseHp", ex.Message);
        }

        [Test]
        public void Parse_EmptyId_Throws()
        {
            var ex = Assert.Throws<FormatException>(() => CharacterRoster.Parse(ValidJson(id: "")));
            StringAssert.Contains("id", ex.Message);
        }

        [Test]
        public void Parse_EmptyJson_Throws()
        {
            Assert.Throws<ArgumentException>(() => CharacterRoster.Parse(""));
        }

        [Test]
        public void Parse_EmptyCharacters_Throws()
        {
            Assert.Throws<FormatException>(() => CharacterRoster.Parse(@"{ ""characters"": [] }"));
        }

        [Test]
        public void ParseColor_ValidHex_ConvertsCorrectly()
        {
            var color = CharacterRoster.ParseColor("#7A5CF0");
            Assert.AreEqual(0x7A / 255f, color.r, 0.001f);
            Assert.AreEqual(0x5C / 255f, color.g, 0.001f);
            Assert.AreEqual(0xF0 / 255f, color.b, 0.001f);
            Assert.AreEqual(1f, color.a, 0.001f);
        }

        [Test]
        public void ParseColor_LowercaseHex_ConvertsCorrectly()
        {
            var color = CharacterRoster.ParseColor("#ffffff");
            Assert.AreEqual(1f, color.r, 0.001f);
            Assert.AreEqual(1f, color.g, 0.001f);
            Assert.AreEqual(1f, color.b, 0.001f);
        }

        [Test]
        public void ParseColor_Black_IsZero()
        {
            var color = CharacterRoster.ParseColor("#000000");
            Assert.AreEqual(0f, color.r, 0.001f);
            Assert.AreEqual(0f, color.g, 0.001f);
            Assert.AreEqual(0f, color.b, 0.001f);
        }

        [Test]
        public void ParseColor_MissingHash_Throws()
        {
            Assert.Throws<FormatException>(() => CharacterRoster.ParseColor("7A5CF0"));
        }

        [Test]
        public void ParseColor_WrongLength_Throws()
        {
            Assert.Throws<FormatException>(() => CharacterRoster.ParseColor("#7A5"));
        }

        [Test]
        public void ParseColor_InvalidHexChar_Throws()
        {
            Assert.Throws<FormatException>(() => CharacterRoster.ParseColor("#7A5CFG"));
        }

        [Test]
        public void Parse_InvalidColor_Throws()
        {
            var ex = Assert.Throws<FormatException>(() => CharacterRoster.Parse(ValidJson(tint: "purple")));
            StringAssert.Contains("tintColor", ex.Message);
        }
    }
}
