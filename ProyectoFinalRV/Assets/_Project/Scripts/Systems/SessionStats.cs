using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace QCXR
{
    /// Recolecta métricas, arma el SessionRecord y lo guarda al terminar (JSON + CSV).
    public class SessionStats : GameSystem
    {
        [SerializeField] GameConfigSO config;

        SessionRecord rec;
        int score;
        float startTime;

        protected override void OnEnable()
        {
            base.OnEnable();
            GameEvents.ProductResolved += OnResolved;
            GameEvents.DefectFixed += OnDefectFixed;
            GameEvents.ReportSubmitted += OnReport;
            GameEvents.ScoreChanged += OnScore;
            GameEvents.GameEnded += OnEnded;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameEvents.ProductResolved -= OnResolved;
            GameEvents.DefectFixed -= OnDefectFixed;
            GameEvents.ReportSubmitted -= OnReport;
            GameEvents.ScoreChanged -= OnScore;
            GameEvents.GameEnded -= OnEnded;
        }

        protected override void OnPlayStart() { rec = new SessionRecord(); score = 0; startTime = Time.time; }

        void OnResolved(Product p, ProductOutcome o)
        {
            if (!IsPlaying || p.IsTutorial) return;
            rec.productsEvaluated++;
            if (o.IsSuccess()) rec.successes++; else rec.failures++;
            rec.log.Add(new ProductLogEntry
            {
                product = p.Type.ToString(),
                outcome = o.ToString(),
                defects = p.ActiveDefectTypes.Select(t => t.ToString()).ToArray(),
                seconds = Time.time - p.SpawnTime
            });
        }

        void OnDefectFixed(ProductDefect d)
        {
            if (!IsPlaying) return;
            var p = d.GetComponentInParent<Product>();
            if (p != null && !p.IsTutorial) rec.defectsFixed++;
        }

        void OnReport(DefectType t, bool ok)
        {
            if (!IsPlaying) return;
            if (ok) rec.reportsCorrect++; else rec.reportsWrong++;
        }

        void OnScore(int s, int combo)
        {
            if (rec == null) return;
            score = s;
            if (combo > rec.bestCombo) rec.bestCombo = combo;
        }

        void OnEnded(EndReason reason)
        {
            if (rec == null) return;
            rec.score = score;
            rec.accuracy = rec.productsEvaluated > 0 ? rec.successes / (float)rec.productsEvaluated : 0f;
            rec.grade = config.GetGrade(rec.accuracy);
            rec.endReason = reason.ToString();
            rec.durationSeconds = Time.time - startTime;
            rec.dateUtc = DateTime.UtcNow.ToString("o");

            var save = SaveSystem.Append(rec);
            SaveSystem.ExportCsv(rec);
            GameEvents.RaiseSessionSummaryReady(rec, save);
        }
    }

    /// Persistencia: historial + récord en JSON y registro por producto en CSV.
    public static class SaveSystem
    {
        const int MaxHistory = 20;
        static string SavePath => Path.Combine(Application.persistentDataPath, "qcxr_save.json");
        static string CsvPath  => Path.Combine(Application.persistentDataPath, "qcxr_last_session.csv");

        public static SaveData Load()
        {
            try { if (File.Exists(SavePath)) return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData(); }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] Lectura: {e.Message}"); }
            return new SaveData();
        }

        public static SaveData Append(SessionRecord record)
        {
            var data = Load();
            data.lastWasHighScore = record.score > data.highScore;
            if (data.lastWasHighScore) data.highScore = record.score;
            data.history.Add(record);
            if (data.history.Count > MaxHistory) data.history.RemoveAt(0);

            try { File.WriteAllText(SavePath, JsonUtility.ToJson(data, true)); }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] Guardado: {e.Message}"); }
            return data;
        }

        public static void ExportCsv(SessionRecord record)
        {
            var sb = new StringBuilder("producto,resultado,defectos,segundos\n");
            foreach (var e in record.log)
                sb.AppendLine($"{e.product},{e.outcome},{string.Join("|", e.defects)},{e.seconds:F1}");
            try { File.WriteAllText(CsvPath, sb.ToString()); }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] CSV: {e.Message}"); }
        }
    }
}
