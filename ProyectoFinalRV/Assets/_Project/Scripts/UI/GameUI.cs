using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QCXR
{
    /// TODA la UI en un solo script. Solo escucha eventos y pinta; los botones solo emiten eventos.
    /// Ponerlo en un objeto SIEMPRE activo; los `*Root` son hijos (cada Canvas World Space) que se muestran/ocultan.
    public class GameUI : MonoBehaviour
    {
        [Serializable] public struct ReportOption { public Button button; public DefectType type; }

        [Header("HUD")]
        [SerializeField] GameObject hudRoot;
        [SerializeField] TMP_Text scoreText, comboText, timeText, strikesText;
        [SerializeField] Slider productTimeBar;
        [SerializeField] TMP_Text toastText, bannerText;   // fuera de hudRoot: visibles también en el tutorial

        [Header("Tutorial")]
        [SerializeField] GameObject instructionRoot;
        [SerializeField] TMP_Text instructionText, progressText;

        [Header("Reporte de daño")]
        [SerializeField] GameObject reportRoot;
        [SerializeField] TMP_Text reportTitle;
        [SerializeField] ReportOption[] reportOptions;

        [Header("Resultados")]
        [SerializeField] GameObject resultsRoot;
        [SerializeField] TMP_Text resultsTitle, gradeText, finalScoreText, detailsText, feedbackText;
        [SerializeField] Button restartButton, quitButton;
        [SerializeField] float resultsDistance = 1.6f;

        Coroutine toastRoutine;

        void Awake()
        {
            instructionRoot.SetActive(false);
            reportRoot.SetActive(false);
            resultsRoot.SetActive(false);
            hudRoot.SetActive(false);
            if (toastText) toastText.text = "";
            if (bannerText) bannerText.text = "";
            if (productTimeBar) productTimeBar.gameObject.SetActive(false);

            foreach (var o in reportOptions)
            {
                var type = o.type;
                o.button.onClick.AddListener(() => GameEvents.RaiseReportChoiceMade(type));
            }
            restartButton.onClick.AddListener(() => GameEvents.RaiseButtonPressed("Restart"));
            quitButton.onClick.AddListener(() => GameEvents.RaiseButtonPressed("Quit"));
        }

        void OnEnable()
        {
            GameEvents.GameStateChanged += OnState;
            GameEvents.ScoreChanged += OnScore;
            GameEvents.TimeChanged += OnTime;
            GameEvents.StrikesChanged += OnStrikes;
            GameEvents.ProductTimeChanged += OnProductTime;
            GameEvents.FeedbackRequested += OnFeedback;
            GameEvents.RandomEventStarted += OnEventStarted;
            GameEvents.RandomEventEnded += OnEventEnded;
            GameEvents.InstructionChanged += OnInstruction;
            GameEvents.ReportRequested += OnReportRequested;
            GameEvents.ReportSubmitted += OnReportSubmitted;
            GameEvents.ReportDismissed += OnReportDismissed;
            GameEvents.SessionSummaryReady += OnSummary;
        }

        void OnDisable()
        {
            GameEvents.GameStateChanged -= OnState;
            GameEvents.ScoreChanged -= OnScore;
            GameEvents.TimeChanged -= OnTime;
            GameEvents.StrikesChanged -= OnStrikes;
            GameEvents.ProductTimeChanged -= OnProductTime;
            GameEvents.FeedbackRequested -= OnFeedback;
            GameEvents.RandomEventStarted -= OnEventStarted;
            GameEvents.RandomEventEnded -= OnEventEnded;
            GameEvents.InstructionChanged -= OnInstruction;
            GameEvents.ReportRequested -= OnReportRequested;
            GameEvents.ReportSubmitted -= OnReportSubmitted;
            GameEvents.ReportDismissed -= OnReportDismissed;
            GameEvents.SessionSummaryReady -= OnSummary;
        }

        // ── HUD ──
        void OnState(GameState s) => hudRoot.SetActive(s == GameState.Playing);

        void OnScore(int score, int combo)
        {
            scoreText.text = score.ToString("N0");
            comboText.text = combo > 1 ? $"COMBO x{combo}" : "";
        }

        void OnTime(float t)
        {
            timeText.text = $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
            timeText.color = t <= 10f ? Color.red : Color.white;
        }

        void OnStrikes(int strikes, int max) => strikesText.text = $"Fallos {strikes}/{max}";

        void OnProductTime(float remaining, float total)
        {
            bool show = remaining >= 0f;
            productTimeBar.gameObject.SetActive(show);
            if (show) productTimeBar.value = Mathf.Clamp01(remaining / Mathf.Max(0.01f, total));
        }

        void OnFeedback(string message)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(Toast(message));
        }

        IEnumerator Toast(string message)
        {
            toastText.text = message;
            yield return new WaitForSeconds(1.8f);
            toastText.text = "";
        }

        void OnEventStarted(string name, float duration) => bannerText.text = $"¡{name}!";
        void OnEventEnded(string name) => bannerText.text = "";

        // ── Tutorial ──
        void OnInstruction(string text, int index, int total)
        {
            bool show = !string.IsNullOrEmpty(text);
            instructionRoot.SetActive(show);
            if (!show) return;
            instructionText.text = text;
            progressText.text = $"Paso {index}/{total}";
        }

        // ── Reporte ──
        void OnReportRequested(Product p)
        {
            reportTitle.text = $"Reportar daño: {p.DisplayName}";
            reportRoot.SetActive(true);
        }

        void OnReportSubmitted(DefectType t, bool ok) => reportRoot.SetActive(false);
        void OnReportDismissed() => reportRoot.SetActive(false);

        // ── Resultados ──
        void OnSummary(SessionRecord r, SaveData save)
        {
            PlaceInFrontOfPlayer();
            resultsRoot.SetActive(true);

            resultsTitle.text = r.endReason == EndReason.TimeUp.ToString() ? "¡Turno terminado!" : "¡Demasiados fallos!";
            gradeText.text = r.grade;
            finalScoreText.text = $"{r.score:N0} pts" + (save.lastWasHighScore ? "  ¡NUEVO RÉCORD!" : $"   (récord {save.highScore:N0})");
            detailsText.text =
                $"Productos evaluados: {r.productsEvaluated}\n" +
                $"Aciertos: {r.successes}   Fallos: {r.failures}\n" +
                $"Precisión: {r.accuracy:P0}\n" +
                $"Defectos reparados: {r.defectsFixed}\n" +
                $"Reportes correctos: {r.reportsCorrect}/{r.reportsCorrect + r.reportsWrong}\n" +
                $"Mejor combo: x{r.bestCombo}";
            feedbackText.text = BuildFeedback(r);
        }

        static string BuildFeedback(SessionRecord r)
        {
            if (r.productsEvaluated == 0) return "No evaluaste ningún producto. ¡Inténtalo de nuevo!";
            if (r.accuracy >= 0.9f) return "Inspector excelente: casi nada se te escapa.";
            if (r.reportsWrong > r.reportsCorrect) return "Revisa con la lupa antes de reportar: confundiste varios defectos.";
            if (r.failures > r.successes) return "Muchos productos mal clasificados. Usa la lupa en cada producto.";
            return "Buen trabajo. Sube tu precisión reparando más y desechando solo lo irreparable.";
        }

        void PlaceInFrontOfPlayer()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 flat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            resultsRoot.transform.SetPositionAndRotation(
                cam.transform.position + flat * resultsDistance, Quaternion.LookRotation(flat, Vector3.up));
        }
    }
}
