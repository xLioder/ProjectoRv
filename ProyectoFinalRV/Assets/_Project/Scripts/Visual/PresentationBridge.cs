using UnityEngine;
using UnityEngine.UI;

namespace QCXR.Visual
{
    /// Puente de PRESENTACIÓN: escucha hechos de GameEvents y dispara animaciones/partículas.
    /// Solo visual: no emite eventos, no cambia estado de juego, no decide nada.
    ///
    ///  · BeltJamChanged          → Animator de la banda (bool "Jammed": vibración de la cinta)
    ///  · Sfx Hit (martillo)       → FX_HammerDust en el punto de impacto
    ///  · Sfx Fixed (reparación)   → FX_RepairFlash en el defecto reparado
    ///  · Sfx Success/Fail/Report  → destello verde/rojo del hueco de estante o del cubo más cercano
    ///  · SessionSummaryReady      → sello de nota (sprite/color por S, A, B, C) + confeti
    public class PresentationBridge : MonoBehaviour
    {
        [Header("Banda")]
        [SerializeField] Animator beltAnimator;

        [Header("Partículas (instancias reutilizables de la escena)")]
        [SerializeField] ParticleSystem repairFlash;
        [SerializeField] ParticleSystem hammerDust;
        [SerializeField] ParticleSystem confetti;

        [Header("Destellos de estantes y cubo (triggers Good / Bad)")]
        [SerializeField] Animator[] flashTargets;
        [SerializeField] float flashRadius = 0.4f;

        [Header("Sello de nota final")]
        [SerializeField] Image stampImage;
        [SerializeField] Sprite[] gradeSprites = new Sprite[4];   // S, A, B, C

        [Header("Respuesta de las herramientas (solo piezas hijas, nunca la raíz)")]
        [SerializeField] Animator hammerAnimator;       // trigger "Hit": la cabeza se aplasta y rebota
        [SerializeField] Animator screwdriverAnimator;  // trigger "Press": el eje entra y vuelve (el giro es de Tool.cs)
        [SerializeField] Animator magnifierAnimator;    // trigger "Reveal": destello del aro al revelar
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int PressToolId = Animator.StringToHash("Press");
        static readonly int RevealId = Animator.StringToHash("Reveal");

        static readonly int JammedId = Animator.StringToHash("Jammed");
        static readonly int GoodId = Animator.StringToHash("Good");
        static readonly int BadId = Animator.StringToHash("Bad");

        void OnEnable()
        {
            GameEvents.BeltJamChanged += OnJam;
            GameEvents.SfxRequested += OnSfx;
            GameEvents.SessionSummaryReady += OnSummary;
        }

        void OnDisable()
        {
            GameEvents.BeltJamChanged -= OnJam;
            GameEvents.SfxRequested -= OnSfx;
            GameEvents.SessionSummaryReady -= OnSummary;
        }

        void OnJam(bool jammed)
        {
            if (beltAnimator) beltAnimator.SetBool(JammedId, jammed);
        }

        void OnSfx(SfxId id, Vector3 pos)
        {
            switch (id)
            {
                case SfxId.Hit: PlayAt(hammerDust, pos); if (hammerAnimator) hammerAnimator.SetTrigger(HitId); break;
                case SfxId.Screw: if (screwdriverAnimator) screwdriverAnimator.SetTrigger(PressToolId); break;
                case SfxId.Reveal: if (magnifierAnimator) magnifierAnimator.SetTrigger(RevealId); break;
                case SfxId.Fixed: PlayAt(repairFlash, pos); break;
                case SfxId.Success:
                case SfxId.Report: Flash(pos, GoodId); break;
                case SfxId.Fail: Flash(pos, BadId); break;
            }
        }

        static void PlayAt(ParticleSystem ps, Vector3 pos)
        {
            if (ps == null) return;
            ps.transform.position = pos;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }

        void Flash(Vector3 pos, int trigger)
        {
            if (flashTargets == null) return;
            Animator best = null;
            float bestD = flashRadius;
            foreach (var a in flashTargets)
            {
                if (a == null) continue;
                var r = a.GetComponent<Renderer>();
                float d = Vector3.Distance(r != null ? r.bounds.center : a.transform.position, pos);
                if (d < bestD) { bestD = d; best = a; }
            }
            if (best != null) best.SetTrigger(trigger);
        }

        void OnSummary(SessionRecord record, SaveData data)
        {
            if (stampImage != null && record != null)
            {
                int i = record.grade == "S" ? 0 : record.grade == "A" ? 1 : record.grade == "B" ? 2 : 3;
                if (i < gradeSprites.Length && gradeSprites[i] != null) stampImage.sprite = gradeSprites[i];
            }
            if (confetti != null) { confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); confetti.Play(true); }
        }
    }
}
