using TMPro;
using UnityEngine;

namespace QCXR
{
    // UI de diagnóstico: presenta el estado de la estación sin decidir defectos,
    // reparaciones, clasificación ni puntuación. Escucha el bus GameEvents existente.
    // El controlador debe quedar activo, aunque panelRoot pueda ocultarse.
    [DisallowMultipleComponent]
    public sealed class InspectionStatusUI : MonoBehaviour
    {
        [Header("Panel en World Space")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text productText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text repairedText;

        [Header("Colores de estado")]
        [SerializeField] private Color neutralColor = new Color(0.45f, 0.86f, 0.91f);
        [SerializeField] private Color warningColor = new Color(1f, 0.73f, 0.38f);
        [SerializeField] private Color successColor = new Color(0.47f, 0.88f, 0.66f);
        [SerializeField] private Color errorColor = new Color(0.96f, 0.42f, 0.40f);

        private GameState state = GameState.Boot;
        private Product currentProduct;
        private int repairsOnCurrent;

        private void Awake()
        {
            ShowProduct("Esperando producto");
            ShowStatus("Estación preparada", neutralColor);
            ShowHint("Completa el tutorial para comenzar el turno.");
            ShowRepairCount();
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            GameEvents.GameStateChanged += OnGameStateChanged;
            GameEvents.ProductArrivedAtInspection += OnProductArrived;
            GameEvents.ProductGrabbed += OnProductGrabbed;
            GameEvents.ToolGrabbed += OnToolGrabbed;
            GameEvents.DefectFixed += OnDefectFixed;
            GameEvents.ReportRequested += OnReportRequested;
            GameEvents.ReportSubmitted += OnReportSubmitted;
            GameEvents.ReportDismissed += OnReportDismissed;
            GameEvents.ProductResolved += OnProductResolved;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= OnGameStateChanged;
            GameEvents.ProductArrivedAtInspection -= OnProductArrived;
            GameEvents.ProductGrabbed -= OnProductGrabbed;
            GameEvents.ToolGrabbed -= OnToolGrabbed;
            GameEvents.DefectFixed -= OnDefectFixed;
            GameEvents.ReportRequested -= OnReportRequested;
            GameEvents.ReportSubmitted -= OnReportSubmitted;
            GameEvents.ReportDismissed -= OnReportDismissed;
            GameEvents.ProductResolved -= OnProductResolved;
        }

        private void OnGameStateChanged(GameState newState)
        {
            state = newState;
            SetPanelVisible(state == GameState.Playing);
            if (state == GameState.Playing)
            {
                currentProduct = null;
                repairsOnCurrent = 0;
                ShowProduct("Esperando producto");
                ShowStatus("Turno iniciado", neutralColor);
                ShowHint("El siguiente producto llegará por la banda.");
                ShowRepairCount();
            }
        }

        private void OnProductArrived(Product p)
        {
            if (!IsActiveSession() || p == null || p.IsTutorial) return;
            currentProduct = p;
            repairsOnCurrent = 0;
            ShowProduct(p.DisplayName);
            ShowStatus("Pendiente de inspección", warningColor);
            ShowHint("Toma el producto y examínalo. Usa la lupa para buscar daños ocultos.");
            ShowRepairCount();
        }

        private void OnProductGrabbed(Product p)
        {
            if (!IsCurrent(p)) return;
            ShowStatus("Inspección en curso", neutralColor);
            ShowHint("Comprueba el estado del producto antes de clasificarlo.");
        }

        private void OnToolGrabbed(ToolType tool)
        {
            if (!IsActiveSession() || currentProduct == null) return;
            switch (tool)
            {
                case ToolType.Magnifier:
                    ShowHint("Lupa: inspecciona el producto para revelar daños ocultos.");
                    break;
                case ToolType.Screwdriver:
                    ShowHint("Destornillador: úsalo si encuentras un tornillo flojo.");
                    break;
                case ToolType.Hammer:
                    ShowHint("Martillo: úsalo si encuentras una abolladura reparable.");
                    break;
            }
        }

        private void OnDefectFixed(ProductDefect defect)
        {
            if (!IsActiveSession() || currentProduct == null || defect == null) return;
            if (defect.GetComponentInParent<Product>() != currentProduct) return;

            repairsOnCurrent++;
            ShowStatus("Reparación realizada", successColor);
            ShowHint("Continúa inspeccionando: podría haber otros daños.");
            ShowRepairCount();
        }

        private void OnReportRequested(Product p)
        {
            if (!IsCurrent(p)) return;
            ShowStatus("Reporte de daño", warningColor);
            ShowHint("Selecciona el defecto que has detectado en el panel de reporte.");
        }

        private void OnReportSubmitted(DefectType defectType, bool correct)
        {
            if (!IsActiveSession() || currentProduct == null) return;
            ShowStatus(correct ? "Reporte correcto" : "Reporte incorrecto", correct ? successColor : errorColor);
            ShowHint(correct ? "Reporte registrado. Continúa con el procedimiento de descarte."
                             : "Comprueba qué defecto tenía el producto.");
        }

        private void OnReportDismissed()
        {
            if (!IsActiveSession() || currentProduct == null) return;
            ShowStatus("Inspección en curso", neutralColor);
            ShowHint("Comprueba el producto antes de confirmar su clasificación.");
        }

        private void OnProductResolved(Product p, ProductOutcome outcome)
        {
            if (!IsCurrent(p)) return;
            ShowStatus(outcome.ToFriendly(), outcome.IsSuccess() ? successColor : errorColor);
            ShowHint("Producto procesado. Esperando el siguiente de la banda.");
            currentProduct = null;
        }

        private bool IsActiveSession() => state == GameState.Playing;
        private bool IsCurrent(Product p) => IsActiveSession() && p != null && p == currentProduct;

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
        }

        private void ShowProduct(string text)
        {
            if (productText != null) productText.text = text;
        }

        private void ShowStatus(string text, Color color)
        {
            if (statusText == null) return;
            statusText.text = text;
            statusText.color = color;
        }

        private void ShowHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        private void ShowRepairCount()
        {
            if (repairedText != null) repairedText.text = $"Reparaciones realizadas: {repairsOnCurrent}";
        }
    }
}
