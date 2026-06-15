using UnityEngine;
using Enigma.Combat;
using Enigma.Data;

namespace Enigma.Map
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class ForestToppleDirector : MonoBehaviour
    {
        [SerializeField] private float _waveSpeed = 14f;
        [SerializeField] private float _maxJitterSeconds = 0.4f;

        private HealthComponent _health;
        private bool _fired;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
        }

        private void Start()
        {
            _health.Model.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health?.Model != null)
                _health.Model.Died -= OnDied;
        }

        private void OnDied()
        {
            // 一度きりガード
            if (_fired) return;
            _fired = true;

            var trees = Object.FindObjectsByType<TreeTopplePresenter>(FindObjectsSortMode.None);
            var positions = new System.Collections.Generic.List<Vector3>(trees.Length);
            foreach (var tree in trees)
                positions.Add(tree.transform.position);

            var planner = new ToppleWavePlanner(new SystemRandomSource());
            float[] delays = planner.PlanDelays(transform.position, positions, _waveSpeed, _maxJitterSeconds);

            for (int i = 0; i < trees.Length; i++)
                trees[i].Fall(ToppleWavePlanner.ToppleAxis(transform.position, trees[i].transform.position), delays[i]);
        }
    }
}
