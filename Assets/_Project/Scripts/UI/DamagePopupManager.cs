using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.UI
{
    // MatchStatsTracker と同じ1秒スキャン方式で HealthComponent.Damaged を動的購読し、
    // ダメージ数字ポップアップを頭上に生成する。
    public sealed class DamagePopupManager : MonoBehaviour
    {
        // プレイヤー GO を取得するためにタグ検索する（ビルダーでの結線不要・軽量）
        private GameObject _playerGo;

        private readonly HashSet<HealthComponent> _subscribed = new();

        private void Start()
        {
            _playerGo = GameObject.FindWithTag("Player");
            StartCoroutine(ScanLoop());
        }

        private IEnumerator ScanLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                SubscribeNew();
            }
        }

        private void SubscribeNew()
        {
            var all = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            foreach (var hc in all)
            {
                if (!_subscribed.Add(hc)) continue;
                // ローカルキャプチャ: ラムダがループ変数を誤捕捉しないよう明示コピー
                var captured = hc;
                captured.Damaged += amount => SpawnPopup(captured, amount);
            }
        }

        private void SpawnPopup(HealthComponent hc, float amount)
        {
            // 頭上 +1.8 をアンカー位置にする（ポップアップはそこから相対で上昇）
            var anchor = new GameObject("PopupAnchor");
            anchor.transform.position = hc.transform.position + Vector3.up * 1.8f;

            var popupGo  = new GameObject("DamagePopup");
            popupGo.transform.SetParent(anchor.transform, false);
            popupGo.transform.localPosition = Vector3.zero;

            var tm             = popupGo.AddComponent<TextMesh>();
            tm.fontSize        = 48;
            tm.characterSize   = 0.06f;
            tm.anchor          = TextAnchor.MiddleCenter;
            tm.alignment       = TextAlignment.Center;

            var popup = popupGo.AddComponent<DamagePopup>();
            // プレイヤーが受けたダメージは赤文字、それ以外は白文字
            bool isPlayerDamage = _playerGo != null && hc.gameObject == _playerGo;
            popup.Init(amount, isPlayerDamage);

            // アンカーは1秒後に自動破棄（ポップアップ本体が先に Destroy されても孤立しない）
            Destroy(anchor, 1.1f);
        }
    }
}
