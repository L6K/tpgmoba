using NUnit.Framework;
using UnityEngine;
using Enigma.Character;
using Enigma.Data;

namespace Enigma.Tests
{
    public class CharacterOwnershipServiceTests
    {
        private static CharacterData CreateCharacterData(string charId, bool ownedByDefault = false)
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.CharId         = charId;
            data.DisplayName    = charId;
            data.OwnedByDefault = ownedByDefault;
            return data;
        }

        [Test]
        public void IsOwned_OwnedByDefaultTrue_ReturnsTrue()
        {
            var store   = new FakeSaveStore();
            var service = new CharacterOwnershipService(store);
            var chara   = CreateCharacterData("char_01", ownedByDefault: true);

            Assert.IsTrue(service.IsOwned(chara));
        }

        [Test]
        public void IsOwned_UnlockedAfterUnlock_ReturnsTrue()
        {
            var store   = new FakeSaveStore();
            var service = new CharacterOwnershipService(store);
            var chara   = CreateCharacterData("char_02");

            Assert.IsFalse(service.IsOwned(chara), "前提: ロック済み");
            service.Unlock(chara.CharId);
            Assert.IsTrue(service.IsOwned(chara), "Unlock 後は IsOwned == true");
        }

        [Test]
        public void Unlock_WritesKeyToStore()
        {
            var store   = new FakeSaveStore();
            var service = new CharacterOwnershipService(store);

            service.Unlock("char_03");

            Assert.AreEqual(1, store.GetInt("owned_char_char_03", 0));
        }

        [Test]
        public void IsOwned_NullData_ReturnsFalse()
        {
            var store   = new FakeSaveStore();
            var service = new CharacterOwnershipService(store);

            Assert.IsFalse(service.IsOwned(null));
        }
    }
}
