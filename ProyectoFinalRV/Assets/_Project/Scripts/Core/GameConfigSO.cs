using UnityEngine;

namespace QCXR
{
    // Todo el balance del juego en un asset (Create > QCXR > Game Config).
    [CreateAssetMenu(fileName = "GameConfig", menuName = "QCXR/Game Config")]
    public class GameConfigSO : ScriptableObject
    {
        [Header("Sesión")]
        public float sessionDuration = 180f;
        public int maxStrikes = 5;

        [Header("Banda y ritmo (eje X = dificultad 0..1)")]
        public AnimationCurve beltSpeedByDifficulty = AnimationCurve.Linear(0, 0.18f, 1, 0.40f);
        public AnimationCurve productTimeByDifficulty = AnimationCurve.Linear(0, 28f, 1, 15f);
        public float spawnDelayAfterResolve = 1.2f;

        [Header("Productos")]
        [Range(0, 1)] public float defectChance = 0.7f;
        [Range(1, 3)] public int maxDefectsPerProduct = 2;

        [Header("Puntaje base")]
        public int perfectPlaced = 100;
        public int repairedPlaced = 150;
        public int discardedCorrect = 120;
        public int reportCorrectBonus = 50;
        public int reportWrongPenalty = -10;
        public int wrongSocket = -50;
        public int placedDefective = -75;
        public int discardedWasteful = -40;
        public int timedOut = -30;

        [Header("Combo")]
        public float comboStep = 0.25f;
        public float maxComboMultiplier = 2.5f;

        [Header("Calificación (precisión 0..1)")]
        public float gradeS = 0.90f, gradeA = 0.75f, gradeB = 0.50f;

        [Header("Eventos inesperados")]
        public float firstEventDelay = 25f;
        public Vector2 eventInterval = new Vector2(18f, 32f);

        public int GetBasePoints(ProductOutcome o)
        {
            switch (o)
            {
                case ProductOutcome.PerfectPlaced:     return perfectPlaced;
                case ProductOutcome.RepairedPlaced:    return repairedPlaced;
                case ProductOutcome.DiscardedCorrect:  return discardedCorrect;
                case ProductOutcome.WrongSocket:       return wrongSocket;
                case ProductOutcome.PlacedDefective:   return placedDefective;
                case ProductOutcome.DiscardedWasteful: return discardedWasteful;
                default:                               return timedOut;
            }
        }

        public string GetGrade(float accuracy) =>
            accuracy >= gradeS ? "S" : accuracy >= gradeA ? "A" : accuracy >= gradeB ? "B" : "C";
    }
}
