using UnityEngine;

namespace QCXR
{
    // Reglas del turno: temporizador global, dificultad, puntaje con combo y fallos.
    // Cada regla es una sección independiente; todas reaccionan a eventos.
    public class SessionRules : GameSystem
    {
        [SerializeField] GameConfigSO config;

        // Temporizador y dificultad
        float remaining, elapsed, lastDifficulty;
        // Puntaje
        int score, combo;
        float scoreMultiplier = 1f;
        // Fallos
        int strikes;

        protected override void OnEnable()
        {
            base.OnEnable();
            GameEvents.ProductResolved += OnProductResolved;
            GameEvents.ReportSubmitted += OnReportSubmitted;
            GameEvents.StrikeRequested += OnStrikeRequested;
            GameEvents.RushChanged += OnRush;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameEvents.ProductResolved -= OnProductResolved;
            GameEvents.ReportSubmitted -= OnReportSubmitted;
            GameEvents.StrikeRequested -= OnStrikeRequested;
            GameEvents.RushChanged -= OnRush;
        }

        protected override void OnPlayStart()
        {
            remaining = config.sessionDuration;
            elapsed = 0f; lastDifficulty = 0f;
            score = 0; combo = 0; strikes = 0; scoreMultiplier = 1f;

            GameEvents.RaiseTimeChanged(remaining);
            GameEvents.RaiseDifficultyChanged(0f);
            GameEvents.RaiseScoreChanged(score, combo);
            GameEvents.RaiseStrikesChanged(strikes, config.maxStrikes);
        }

        // Tiempo y dificultad
        void Update()
        {
            if (!IsPlaying) return;

            remaining -= Time.deltaTime;
            elapsed += Time.deltaTime;
            GameEvents.RaiseTimeChanged(Mathf.Max(0f, remaining));

            float difficulty = Mathf.Clamp01(elapsed / config.sessionDuration);
            if (Mathf.Abs(difficulty - lastDifficulty) >= 0.02f)
            {
                lastDifficulty = difficulty;
                GameEvents.RaiseDifficultyChanged(difficulty);
            }

            if (remaining <= 0f) GameEvents.RaiseEndRequested(EndReason.TimeUp);
        }

        // Puntaje
        void OnRush(bool on, float belt, float time, float scoreMult) => scoreMultiplier = scoreMult;

        void OnProductResolved(Product p, ProductOutcome outcome)
        {
            if (!IsPlaying || p.IsTutorial) return;

            int points = config.GetBasePoints(outcome);
            if (outcome.IsSuccess())
            {
                combo++;
                float comboMult = Mathf.Min(1f + (combo - 1) * config.comboStep, config.maxComboMultiplier);
                points = Mathf.RoundToInt(points * comboMult * scoreMultiplier);
            }
            else
            {
                combo = 0;
                AddStrike();
            }
            ApplyPoints(points, outcome.ToFriendly());
        }

        void OnReportSubmitted(DefectType type, bool correct)
        {
            if (!IsPlaying) return;
            ApplyPoints(correct ? config.reportCorrectBonus : config.reportWrongPenalty,
                        correct ? "Reporte correcto" : "Reporte incorrecto");
        }

        void ApplyPoints(int points, string reason)
        {
            score = Mathf.Max(0, score + points);
            GameEvents.RaiseScoreChanged(score, combo);
            GameEvents.RaiseFeedback($"{reason}  {(points >= 0 ? "+" : "")}{points}");
        }

        // Fallos
        void OnStrikeRequested(string reason)
        {
            if (!IsPlaying) return;
            GameEvents.RaiseFeedback($"Fallo: {reason}");
            AddStrike();
        }

        void AddStrike()
        {
            strikes++;
            GameEvents.RaiseStrikesChanged(strikes, config.maxStrikes);
            if (strikes >= config.maxStrikes) GameEvents.RaiseEndRequested(EndReason.TooManyStrikes);
        }
    }
}
