using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace QCXR
{
    /// Producto que viaja por la banda. Conoce su estado y sus defectos; NO decide puntaje.
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class Product : MonoBehaviour
    {
        public static readonly List<Product> Active = new List<Product>();

        [SerializeField] ProductType type;
        [SerializeField] string displayName = "Producto";
        [SerializeField, Min(0f)] float spawnWeight = 1f;
        [SerializeField] ProductDefect[] defects;
        [SerializeField] float vanishDelay = 0.8f;

        XRGrabInteractable grab;

        public Rigidbody Body { get; private set; }
        public ProductType Type => type;
        public string DisplayName => displayName;
        public float SpawnWeight => spawnWeight;
        public ProductState State { get; private set; } = ProductState.OnBelt;
        public bool IsTutorial { get; set; }
        public float SpawnTime { get; private set; }
        public IReadOnlyList<ProductDefect> Defects => defects;

        public bool IsResolved => State == ProductState.Placed || State == ProductState.Discarded || State == ProductState.TimedOut;
        public bool HasPendingDefects => defects.Any(d => d.IsPending);
        public bool HasUnrepairableDefect => defects.Any(d => d.IsPending && !d.Repairable);
        public bool WasRepaired => defects.Any(d => d.State == DefectState.Fixed);
        public IEnumerable<DefectType> ActiveDefectTypes => defects.Where(d => d.State != DefectState.Inactive).Select(d => d.Type);
        public IEnumerable<DefectType> UnrepairableTypes => defects.Where(d => d.IsPending && !d.Repairable).Select(d => d.Type);

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            Body = GetComponent<Rigidbody>();
            SpawnTime = Time.time;
            if (defects == null || defects.Length == 0) defects = GetComponentsInChildren<ProductDefect>(true);
        }

        void OnEnable()
        {
            Active.Add(this);
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            Active.Remove(this);
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        /// Lo llama el spawner justo después de instanciar.
        public void ConfigureDefects(float chance, int maxDefects, bool forceAll)
        {
            foreach (var d in defects) d.Deactivate();
            if (!forceAll && Random.value > chance) return;

            int count = forceAll ? defects.Length : Random.Range(1, Mathf.Min(maxDefects, defects.Length) + 1);
            foreach (var d in defects.OrderBy(_ => Random.value).Take(count)) d.Activate();
        }

        public void SetAtInspection() { if (State == ProductState.OnBelt) State = ProductState.AtInspection; }

        /// Punto único de cierre (estante, cubo, timer o pérdida).
        public void Resolve(ProductOutcome outcome)
        {
            if (IsResolved) return;

            if (outcome == ProductOutcome.TimedOut) State = ProductState.TimedOut;
            else if (outcome == ProductOutcome.DiscardedCorrect || outcome == ProductOutcome.DiscardedWasteful) State = ProductState.Discarded;
            else State = ProductState.Placed;

            Active.Remove(this);
            GameEvents.RaiseProductResolved(this, outcome);
            StartCoroutine(Vanish(outcome == ProductOutcome.TimedOut ? 0.1f : vanishDelay));
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            if (IsResolved || args.interactorObject is XRSocketInteractor) return;
            State = ProductState.Held;
            GameEvents.RaiseProductGrabbed(this);
            GameEvents.RaiseSfx(SfxId.Grab, transform.position);
        }

        void OnReleased(SelectExitEventArgs args) { if (State == ProductState.Held) State = ProductState.OnBelt; }

        IEnumerator Vanish(float delay)
        {
            yield return new WaitForSeconds(delay);
            Vector3 start = transform.localScale;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                transform.localScale = Vector3.Lerp(start, Vector3.zero, t / 0.35f);
                yield return null;
            }
            Destroy(gameObject);
        }
    }

    /// Reglas de evaluación como funciones puras: fáciles de probar y de explicar.
    public static class ProductEvaluator
    {
        public static ProductOutcome EvaluatePlacement(Product p, ProductType shelfType)
        {
            if (p.Type != shelfType) return ProductOutcome.WrongSocket;
            if (p.HasPendingDefects) return ProductOutcome.PlacedDefective;
            return p.WasRepaired ? ProductOutcome.RepairedPlaced : ProductOutcome.PerfectPlaced;
        }

        public static ProductOutcome EvaluateDiscard(Product p) =>
            p.HasUnrepairableDefect ? ProductOutcome.DiscardedCorrect : ProductOutcome.DiscardedWasteful;
    }
}
