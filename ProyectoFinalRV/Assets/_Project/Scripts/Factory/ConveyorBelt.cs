using System.Collections.Generic;
using UnityEngine;

namespace QCXR
{
    /// Banda completa: mueve productos, los detiene en el punto de inspección
    /// y marca como perdidos los que caen al piso o salen por el final.
    /// Requiere un BoxCollider TRIGGER sobre la superficie de la banda.
    public class ConveyorBelt : MonoBehaviour
    {
        [SerializeField] GameConfigSO config;
        [SerializeField] Transform moveDirection;           // su forward = sentido de la banda
        [SerializeField] Transform inspectionPoint;         // donde se detiene frente al jugador
        [SerializeField] float inspectionRadius = 0.5f;
        [SerializeField] Transform beltEnd;                 // pasar este punto = producto perdido
        [SerializeField] float floorY = -0.5f;              // bajo esta altura = perdido
        [SerializeField] Renderer beltRenderer;             // opcional: scroll de textura
        [SerializeField] string textureProperty = "_BaseMap";
        [SerializeField] float textureScroll = 1f;

        readonly HashSet<Product> movedThisStep = new HashSet<Product>();
        Product inspecting;
        float difficulty, speedMultiplier = 1f;
        bool running, jammed;
        Vector2 offset;

        public float CurrentSpeed => running && !jammed
            ? config.beltSpeedByDifficulty.Evaluate(difficulty) * speedMultiplier : 0f;

        void OnEnable()
        {
            GameEvents.GameStateChanged += OnState;
            GameEvents.BeltJamChanged += OnJam;
            GameEvents.RushChanged += OnRush;
            GameEvents.DifficultyChanged += OnDifficulty;
        }

        void OnDisable()
        {
            GameEvents.GameStateChanged -= OnState;
            GameEvents.BeltJamChanged -= OnJam;
            GameEvents.RushChanged -= OnRush;
            GameEvents.DifficultyChanged -= OnDifficulty;
        }

        void OnState(GameState s) => running = s == GameState.Onboarding || s == GameState.Playing;
        void OnJam(bool j) => jammed = j;
        void OnRush(bool on, float belt, float time, float score) => speedMultiplier = belt;
        void OnDifficulty(float d) => difficulty = d;

        void FixedUpdate()
        {
            movedThisStep.Clear();
            if (inspecting != null && inspecting.State != ProductState.AtInspection) inspecting = null;

            for (int i = Product.Active.Count - 1; i >= 0; i--)
            {
                var p = Product.Active[i];
                if (p == null || p.IsResolved) continue;
                Vector3 pos = p.transform.position;

                // Perdido: cayó al piso o salió por el final de la banda
                bool pastEnd = beltEnd != null && p.State == ProductState.OnBelt &&
                               Vector3.Dot(pos - beltEnd.position, moveDirection.forward) > 0f;
                if (pos.y < floorY || pastEnd) { p.Resolve(ProductOutcome.TimedOut); continue; }

                // Llegó al punto de inspección (uno a la vez)
                if (inspecting == null && p.State == ProductState.OnBelt)
                {
                    Vector3 d = pos - inspectionPoint.position;
                    if (d.magnitude <= inspectionRadius && Vector3.Dot(d, moveDirection.forward) >= -0.02f)
                    {
                        inspecting = p;
                        p.SetAtInspection();
                        GameEvents.RaiseProductArrivedAtInspection(p);
                    }
                }
            }
        }

        void OnTriggerStay(Collider other)
        {
            var p = other.GetComponentInParent<Product>();
            if (p == null || p.State != ProductState.OnBelt || !movedThisStep.Add(p)) return;
            p.Body.MovePosition(p.Body.position + moveDirection.forward * CurrentSpeed * Time.fixedDeltaTime);
        }

        void Update()
        {
            if (beltRenderer == null) return;
            offset.y -= CurrentSpeed * textureScroll * Time.deltaTime;
            beltRenderer.material.SetTextureOffset(textureProperty, offset);
        }
    }
}
