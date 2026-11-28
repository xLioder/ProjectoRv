using TMPro;
using UnityEngine;

namespace QCXR
{
    // Agrega el desglose de metricas a la pantalla de resultados.
    // No guarda datos ni altera botones; SessionStats y GameUI siguen a cargo.
    // Instalar en GameUI (siempre activo), no en Results (oculto al inicio).
    [DisallowMultipleComponent]
    public sealed class QCXRResultsMetricsUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text breakdownText;
        [SerializeField] private TMP_Text summaryHeading;

        private void OnEnable() => GameEvents.SessionSummaryReady += HandleSummary;
        private void OnDisable() => GameEvents.SessionSummaryReady -= HandleSummary;

        private void HandleSummary(SessionRecord record, SaveData save)
        {
            if (record == null) return;
            int perfect = 0, repaired = 0, discarded = 0;
            if (record.log != null)
            {
                foreach (var entry in record.log)
                {
                    if (entry == null) continue;
                    if (entry.outcome == ProductOutcome.PerfectPlaced.ToString()) perfect++;
                    else if (entry.outcome == ProductOutcome.RepairedPlaced.ToString()) repaired++;
                    else if (entry.outcome == ProductOutcome.DiscardedCorrect.ToString()) discarded++;
                }
            }

            if (breakdownText != null)
                breakdownText.text = $"APROBADOS  {perfect}    REPARADOS  {repaired}    DESCARTADOS  {discarded}";

            if (summaryHeading != null)
                summaryHeading.text = record.accuracy >= 0.9f ? "EXCELENTE CONTROL DE CALIDAD" :
                    record.accuracy >= 0.7f ? "BUEN CONTROL DE CALIDAD" : "REVISA TU PROCEDIMIENTO";
        }
    }
}
