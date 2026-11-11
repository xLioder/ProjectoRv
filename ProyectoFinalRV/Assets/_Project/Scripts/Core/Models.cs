using System;
using System.Collections.Generic;

namespace QCXR
{
    // ── Enums compartidos ──
    public enum GameState { Boot, Onboarding, Playing, Finished }
    public enum ToolType { None, Magnifier, Screwdriver, Hammer }
    public enum DefectType { LooseScrew, Dent, HiddenCrack, BurnedCircuit }
    public enum DefectState { Inactive, Hidden, Visible, Fixed }
    public enum ProductType { Robot, Radio, Lamp, Toaster, Clock }
    public enum ProductState { OnBelt, AtInspection, Held, Placed, Discarded, TimedOut }
    public enum EndReason { TimeUp, TooManyStrikes }
    public enum SfxId { Grab, Hit, Screw, Reveal, Fixed, Success, Fail, Alarm, Click, Spawn, Report, Jam }

    public enum ProductOutcome
    {
        PerfectPlaced,      // sin defectos, en su estante
        RepairedPlaced,     // reparado por el jugador, en su estante
        DiscardedCorrect,   // irreparable y desechado (requiere reporte)
        WrongSocket,        // estante equivocado
        PlacedDefective,    // se envió con defectos pendientes
        DiscardedWasteful,  // se desechó un producto sano o reparable
        TimedOut            // se acabó el tiempo o se perdió
    }

    public static class OutcomeExtensions
    {
        public static bool IsSuccess(this ProductOutcome o) =>
            o == ProductOutcome.PerfectPlaced || o == ProductOutcome.RepairedPlaced || o == ProductOutcome.DiscardedCorrect;

        public static string ToFriendly(this ProductOutcome o)
        {
            switch (o)
            {
                case ProductOutcome.PerfectPlaced:     return "¡Producto perfecto!";
                case ProductOutcome.RepairedPlaced:    return "¡Reparado y enviado!";
                case ProductOutcome.DiscardedCorrect:  return "Descarte correcto";
                case ProductOutcome.WrongSocket:       return "Estante equivocado";
                case ProductOutcome.PlacedDefective:   return "¡Enviaste un producto defectuoso!";
                case ProductOutcome.DiscardedWasteful: return "Desperdicio: se podía enviar o reparar";
                default:                               return "¡Producto perdido!";
            }
        }
    }

    // ── Datos serializables (JSON) ──
    [Serializable]
    public class ProductLogEntry { public string product; public string outcome; public string[] defects; public float seconds; }

    [Serializable]
    public class SessionRecord
    {
        public string dateUtc;
        public int score, productsEvaluated, successes, failures, defectsFixed, reportsCorrect, reportsWrong, bestCombo;
        public float accuracy, durationSeconds;
        public string grade, endReason;
        public List<ProductLogEntry> log = new List<ProductLogEntry>();
    }

    [Serializable]
    public class SaveData
    {
        public int highScore;
        public bool lastWasHighScore;
        public List<SessionRecord> history = new List<SessionRecord>();
    }
}
