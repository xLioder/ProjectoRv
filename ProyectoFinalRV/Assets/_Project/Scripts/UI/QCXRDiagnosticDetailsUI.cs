using TMPro;
using UnityEngine;

namespace QCXR
{
    // Complementa al panel de inspeccion.
    // Solo presenta acciones verificadas mediante GameEvents. Nunca lee los
    // defectos aun ocultos de Product, por lo que no revela la respuesta.
    // Esta clase no decide si un producto es reparable o correcto.
    [DisallowMultipleComponent]
    public sealed class QCXRDiagnosticDetailsUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text activityText;
        [SerializeField] private TMP_Text repairsText;
        [SerializeField] private TMP_Text nextStepText;

        private Product currentProduct;
        private GameState state = GameState.Boot;
        private int repairs;

        private void OnEnable()
        {
            GameEvents.GameStateChanged += HandleGameState;
            GameEvents.ProductArrivedAtInspection += HandleArrived;
            GameEvents.ProductGrabbed += HandleGrabbed;
            GameEvents.ToolGrabbed += HandleToolGrabbed;
            GameEvents.ToolUsed += HandleToolUsed;
            GameEvents.DefectFixed += HandleFixed;
            GameEvents.ReportRequested += HandleReportRequested;
            GameEvents.ReportSubmitted += HandleReportSubmitted;
            GameEvents.ProductResolved += HandleResolved;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= HandleGameState;
            GameEvents.ProductArrivedAtInspection -= HandleArrived;
            GameEvents.ProductGrabbed -= HandleGrabbed;
            GameEvents.ToolGrabbed -= HandleToolGrabbed;
            GameEvents.ToolUsed -= HandleToolUsed;
            GameEvents.DefectFixed -= HandleFixed;
            GameEvents.ReportRequested -= HandleReportRequested;
            GameEvents.ReportSubmitted -= HandleReportSubmitted;
            GameEvents.ProductResolved -= HandleResolved;
        }

        private void Awake()
        {
            Display("ESPERANDO", "El turno todavia no ha comenzado.", "0 reparaciones", "Primero completa el tutorial.");
        }

        private void HandleGameState(GameState next)
        {
            state = next;
            currentProduct = null;
            repairs = 0;
            if (next == GameState.Playing)
                Display("ETAPA 1 / 4 · RECIBIR", "Esperando un producto de la banda.", "0 reparaciones", "Toma el producto cuando se detenga.");
        }

        private void HandleArrived(Product product)
        {
            if (!IsPlaying() || product == null || product.IsTutorial) return;
            currentProduct = product;
            repairs = 0;
            Display("ETAPA 2 / 4 · INSPECCIONAR", "Producto recibido. Examinalo antes de decidir.",
                    "0 reparaciones", "Busca daños con la lupa y revisa las piezas.");
        }

        private void HandleGrabbed(Product product)
        {
            if (!IsCurrent(product)) return;
            Set(stageText, "ETAPA 2 / 4 · INSPECCIONAR");
            Set(activityText, "Producto tomado. Inspecciona sus partes.");
        }

        private void HandleToolGrabbed(ToolType tool)
        {
            if (!IsPlaying() || currentProduct == null) return;
            switch (tool)
            {
                case ToolType.Magnifier:
                    Set(nextStepText, "Lupa: examina zonas sospechosas.");
                    break;
                case ToolType.Screwdriver:
                    Set(nextStepText, "Destornillador: corrige tornillos flojos visibles.");
                    break;
                case ToolType.Hammer:
                    Set(nextStepText, "Martillo: corrige abolladuras visibles.");
                    break;
            }
        }

        private void HandleToolUsed(ToolType tool)
        {
            if (!IsPlaying() || currentProduct == null || tool != ToolType.Magnifier) return;
            // ToolUsed(Magnifier) ocurre al revelar un defecto oculto de verdad.
            Set(activityText, "Se ha revelado un daño durante la inspeccion.");
            Set(nextStepText, "Examina el daño y decide como proceder.");
        }

        private void HandleFixed(ProductDefect defect)
        {
            if (!IsPlaying() || currentProduct == null || defect == null) return;
            if (defect.GetComponentInParent<Product>() != currentProduct) return;
            repairs++;
            Set(stageText, "ETAPA 3 / 4 · REPARAR");
            Set(activityText, $"Reparacion completada: {FriendlyDefect(defect.Type)}.");
            Set(repairsText, $"{repairs} reparacion(es) completada(s)");
            Set(nextStepText, "Comprueba si quedan otros daños antes de clasificar.");
        }

        private void HandleReportRequested(Product product)
        {
            if (!IsCurrent(product)) return;
            Set(stageText, "ETAPA 3 / 4 · REPORTAR");
            Set(activityText, "Se abrio el formulario de reporte.");
            Set(nextStepText, "Indica el defecto observado y confirma el reporte.");
        }

        private void HandleReportSubmitted(DefectType type, bool correct)
        {
            if (!IsPlaying() || currentProduct == null) return;
            Set(activityText, correct ? $"Reporte correcto: {FriendlyDefect(type)}." : "Reporte incorrecto.");
            Set(nextStepText, correct ? "Completa el descarte en el contenedor." : "Verifica el daño antes de continuar.");
        }

        private void HandleResolved(Product product, ProductOutcome outcome)
        {
            if (!IsCurrent(product)) return;
            Set(stageText, "ETAPA 4 / 4 · CLASIFICADO");
            Set(activityText, outcome.ToFriendly());
            Set(nextStepText, "Esperando el siguiente producto de la banda.");
            currentProduct = null;
        }

        private bool IsPlaying() => state == GameState.Playing;
        private bool IsCurrent(Product product) => IsPlaying() && product != null && product == currentProduct;

        private static string FriendlyDefect(DefectType type)
        {
            switch (type)
            {
                case DefectType.LooseScrew: return "tornillo flojo";
                case DefectType.Dent: return "abolladura";
                case DefectType.HiddenCrack: return "grieta";
                case DefectType.BurnedCircuit: return "circuito quemado";
                default: return "defecto";
            }
        }

        private void Display(string stage, string action, string count, string instruction)
        {
            Set(stageText, stage);
            Set(activityText, action);
            Set(repairsText, count);
            Set(nextStepText, instruction);
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
