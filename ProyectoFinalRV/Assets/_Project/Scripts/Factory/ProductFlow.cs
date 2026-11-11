using System.Collections;
using UnityEngine;

namespace QCXR
{
    /// Entrega UN producto a la vez y vigila su tiempo límite.
    /// Onboarding: producto de práctica con todos los defectos (catalog[0]).
    /// Playing: producto aleatorio; si se acaba su tiempo se pierde.
    public class ProductFlow : MonoBehaviour
    {
        [SerializeField] GameConfigSO config;
        [SerializeField] Product[] catalog;     // prefabs. [0] = producto de práctica (≥3 slots de defecto)
        [SerializeField] Transform spawnPoint;

        GameState state;
        Coroutine routine;
        Product current;
        float remaining, total, difficulty, timeMultiplier = 1f;

        void OnEnable()
        {
            GameEvents.GameStateChanged += OnState;
            GameEvents.ProductResolved += OnResolved;
            GameEvents.ProductArrivedAtInspection += BeginTimer;
            GameEvents.ProductGrabbed += BeginTimer;
            GameEvents.DifficultyChanged += OnDifficulty;
            GameEvents.RushChanged += OnRush;
        }

        void OnDisable()
        {
            GameEvents.GameStateChanged -= OnState;
            GameEvents.ProductResolved -= OnResolved;
            GameEvents.ProductArrivedAtInspection -= BeginTimer;
            GameEvents.ProductGrabbed -= BeginTimer;
            GameEvents.DifficultyChanged -= OnDifficulty;
            GameEvents.RushChanged -= OnRush;
        }

        void OnDifficulty(float d) => difficulty = d;
        void OnRush(bool on, float belt, float time, float score) => timeMultiplier = time;

        // ── Spawn ──
        void OnState(GameState s)
        {
            state = s;
            if (routine != null) { StopCoroutine(routine); routine = null; }

            if (s == GameState.Playing || s == GameState.Finished) { ClearAll(); EndTimer(); }
            if (s == GameState.Onboarding) Queue(0.5f);
            if (s == GameState.Playing) Queue(1.0f);
        }

        void OnResolved(Product p, ProductOutcome o)
        {
            if (p == current) EndTimer();
            if (state == GameState.Onboarding || state == GameState.Playing) Queue(config.spawnDelayAfterResolve);
        }

        void Queue(float delay)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(SpawnAfter(delay));
        }

        IEnumerator SpawnAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            Spawn();
            routine = null;
        }

        void Spawn()
        {
            bool tutorial = state == GameState.Onboarding;
            var prefab = tutorial ? catalog[0] : PickWeighted();
            var product = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            product.IsTutorial = tutorial;
            product.ConfigureDefects(config.defectChance, config.maxDefectsPerProduct, tutorial);
            GameEvents.RaiseSfx(SfxId.Spawn, spawnPoint.position);
        }

        Product PickWeighted()
        {
            float sum = 0f;
            foreach (var p in catalog) sum += p.SpawnWeight;
            float r = Random.value * sum;
            foreach (var p in catalog) { r -= p.SpawnWeight; if (r <= 0f) return p; }
            return catalog[catalog.Length - 1];
        }

        void ClearAll()
        {
            foreach (var p in Product.Active.ToArray()) if (p != null) Destroy(p.gameObject);
        }

        // ── Tiempo límite por producto ──
        void BeginTimer(Product p)
        {
            if (state != GameState.Playing || p.IsTutorial || p.IsResolved || current != null) return;
            current = p;
            total = remaining = config.productTimeByDifficulty.Evaluate(difficulty) * timeMultiplier;
        }

        void EndTimer()
        {
            if (current == null) return;
            current = null;
            GameEvents.RaiseProductTimeChanged(-1f, total);
        }

        void Update()
        {
            if (current == null) return;
            remaining -= Time.deltaTime;
            GameEvents.RaiseProductTimeChanged(remaining, total);
            if (remaining > 0f) return;

            var p = current;
            EndTimer();
            p.Resolve(ProductOutcome.TimedOut);
        }
    }
}
