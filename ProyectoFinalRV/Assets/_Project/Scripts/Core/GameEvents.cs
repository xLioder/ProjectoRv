using System;
using UnityEngine;

namespace QCXR
{
    // Bus de eventos: los sistemas se comunican solo por aquí.
    // Cada uno avisa lo que pasó y escucha a los demás, sin llamarse directamente.
    public static class GameEvents
    {
        // Flujo
        public static event Action<GameState> GameStateChanged;
        public static event Action OnboardingCompleted;
        public static event Action<EndReason> EndRequested;
        public static event Action<EndReason> GameEnded;
        public static event Action<SessionRecord, SaveData> SessionSummaryReady;
        public static event Action<string> ButtonPressed;
        public static event Action<string, int, int> InstructionChanged;
        // Productos
        public static event Action<Product> ProductArrivedAtInspection;
        public static event Action<Product> ProductGrabbed;
        public static event Action<Product, ProductOutcome> ProductResolved;
        // Herramientas y defectos
        public static event Action<ToolType> ToolGrabbed;
        public static event Action<ToolType> ToolUsed;
        public static event Action<ProductDefect> DefectFixed;
        // Reporte
        public static event Action<Product> ReportRequested;
        public static event Action<DefectType> ReportChoiceMade;
        public static event Action<DefectType, bool> ReportSubmitted;
        public static event Action ReportDismissed;
        // Reglas
        public static event Action<int, int> ScoreChanged;           // score, combo
        public static event Action<int, int> StrikesChanged;         // strikes, max
        public static event Action<string> StrikeRequested;          // razón
        public static event Action<float> TimeChanged;               // s restantes
        public static event Action<float, float> ProductTimeChanged; // restante, total (<0 = oculto)
        public static event Action<float> DifficultyChanged;         // 0..1
        // Eventos inesperados
        public static event Action<string, float> RandomEventStarted;
        public static event Action<string> RandomEventEnded;
        public static event Action<bool> BeltJamChanged;
        public static event Action<bool, float, float, float> RushChanged; // activo, banda, tiempo, puntos
        // Feedback
        public static event Action<string> FeedbackRequested;
        public static event Action<SfxId, Vector3> SfxRequested;

        public static void RaiseGameStateChanged(GameState s) => GameStateChanged?.Invoke(s);
        public static void RaiseOnboardingCompleted() => OnboardingCompleted?.Invoke();
        public static void RaiseEndRequested(EndReason r) => EndRequested?.Invoke(r);
        public static void RaiseGameEnded(EndReason r) => GameEnded?.Invoke(r);
        public static void RaiseSessionSummaryReady(SessionRecord r, SaveData d) => SessionSummaryReady?.Invoke(r, d);
        public static void RaiseButtonPressed(string id) => ButtonPressed?.Invoke(id);
        public static void RaiseInstructionChanged(string t, int i, int n) => InstructionChanged?.Invoke(t, i, n);
        public static void RaiseProductArrivedAtInspection(Product p) => ProductArrivedAtInspection?.Invoke(p);
        public static void RaiseProductGrabbed(Product p) => ProductGrabbed?.Invoke(p);
        public static void RaiseProductResolved(Product p, ProductOutcome o) => ProductResolved?.Invoke(p, o);
        public static void RaiseToolGrabbed(ToolType t) => ToolGrabbed?.Invoke(t);
        public static void RaiseToolUsed(ToolType t) => ToolUsed?.Invoke(t);
        public static void RaiseDefectFixed(ProductDefect d) => DefectFixed?.Invoke(d);
        public static void RaiseReportRequested(Product p) => ReportRequested?.Invoke(p);
        public static void RaiseReportChoiceMade(DefectType t) => ReportChoiceMade?.Invoke(t);
        public static void RaiseReportSubmitted(DefectType t, bool ok) => ReportSubmitted?.Invoke(t, ok);
        public static void RaiseReportDismissed() => ReportDismissed?.Invoke();
        public static void RaiseScoreChanged(int s, int c) => ScoreChanged?.Invoke(s, c);
        public static void RaiseStrikesChanged(int s, int m) => StrikesChanged?.Invoke(s, m);
        public static void RaiseStrikeRequested(string r) => StrikeRequested?.Invoke(r);
        public static void RaiseTimeChanged(float t) => TimeChanged?.Invoke(t);
        public static void RaiseProductTimeChanged(float r, float t) => ProductTimeChanged?.Invoke(r, t);
        public static void RaiseDifficultyChanged(float d) => DifficultyChanged?.Invoke(d);
        public static void RaiseRandomEventStarted(string n, float d) => RandomEventStarted?.Invoke(n, d);
        public static void RaiseRandomEventEnded(string n) => RandomEventEnded?.Invoke(n);
        public static void RaiseBeltJamChanged(bool j) => BeltJamChanged?.Invoke(j);
        public static void RaiseRushChanged(bool on, float belt, float time, float score) => RushChanged?.Invoke(on, belt, time, score);
        public static void RaiseFeedback(string m) => FeedbackRequested?.Invoke(m);
        public static void RaiseSfx(SfxId id, Vector3 pos) => SfxRequested?.Invoke(id, pos);

        // "Enter Play Mode" sin recargar dominio: limpia suscriptores viejos.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            GameStateChanged = null; OnboardingCompleted = null; EndRequested = null; GameEnded = null;
            SessionSummaryReady = null; ButtonPressed = null; InstructionChanged = null;
            ProductArrivedAtInspection = null; ProductGrabbed = null; ProductResolved = null;
            ToolGrabbed = null; ToolUsed = null; DefectFixed = null;
            ReportRequested = null; ReportChoiceMade = null; ReportSubmitted = null; ReportDismissed = null;
            ScoreChanged = null; StrikesChanged = null; StrikeRequested = null; TimeChanged = null;
            ProductTimeChanged = null; DifficultyChanged = null; RandomEventStarted = null; RandomEventEnded = null;
            BeltJamChanged = null; RushChanged = null; FeedbackRequested = null; SfxRequested = null;
        }
    }
}
