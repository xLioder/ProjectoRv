using TMPro;
using UnityEngine;

namespace QCXR
{
    /// <summary>
    /// FASE 4. Complementa HUD existente; GameUI sigue mostrando score, combo,
    /// tiempo, fallos y barra por producto. Este script solo suma contadores
    /// de productos procesados y reparaciones hechas, sin calcular puntaje.
    /// Debe estar en un objeto SIEMPRE activo (GameUI), NO dentro de HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QCXRHudMetricsUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text evaluatedText;
        [SerializeField] private TMP_Text repairsText;

        private GameState state = GameState.Boot;
        private int evaluated;
        private int repairs;

        private void OnEnable()
        {
            GameEvents.GameStateChanged += HandleState;
            GameEvents.ProductResolved += HandleProductResolved;
            GameEvents.DefectFixed += HandleDefectFixed;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= HandleState;
            GameEvents.ProductResolved -= HandleProductResolved;
            GameEvents.DefectFixed -= HandleDefectFixed;
        }

        private void Start() => Refresh();

        private void HandleState(GameState next)
        {
            state = next;
            if (next != GameState.Playing) return;
            evaluated = 0;
            repairs = 0;
            Refresh();
        }

        private void HandleProductResolved(Product product, ProductOutcome outcome)
        {
            if (state != GameState.Playing || product == null || product.IsTutorial) return;
            evaluated++;
            Refresh();
        }

        private void HandleDefectFixed(ProductDefect defect)
        {
            if (state != GameState.Playing || defect == null) return;
            var product = defect.GetComponentInParent<Product>();
            if (product == null || product.IsTutorial) return;
            repairs++;
            Refresh();
        }

        private void Refresh()
        {
            if (evaluatedText != null) evaluatedText.text = $"PROCESADOS  {evaluated}";
            if (repairsText != null) repairsText.text = $"REPARADOS  {repairs}";
        }
    }
}
