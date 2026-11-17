using System;
using System.Collections.Generic;
using UnityEngine;

namespace QCXR
{
    public enum OnboardingTrigger
    {
        MoveAround, GrabTool, UseMagnifier, UseScrewdriver, UseHammer,
        ResolveProduct, SubmitReport, PressStart
    }

    /// Tutorial guiado. Cada paso se completa con un EVENTO real del juego.
    /// Incluye la detección de locomoción (poner este script en el XR Origin o asignar `rig`).
    public class OnboardingManager : MonoBehaviour
    {
        [Serializable]
        public class Step
        {
            [TextArea] public string instruction;
            public OnboardingTrigger completeOn;
            public Step(string i, OnboardingTrigger t) { instruction = i; completeOn = t; }
        }

        [SerializeField] Transform rig;                         // XR Origin
        [SerializeField] float moveDistance = 1f;
        [SerializeField] float turnAngle = 45f;
        [SerializeField] string startButtonId = "StartShift";
        [SerializeField] string skipButtonId = "SkipTutorial";

        [SerializeField]
        List<Step> steps = new List<Step>
        {
            new Step("Muévete con el joystick izquierdo y gira con el derecho.", OnboardingTrigger.MoveAround),
            new Step("Agarra una herramienta de la mesa con el botón de agarre (grip).", OnboardingTrigger.GrabTool),
            new Step("LUPA: acércala al producto y mira por el lente para revelar defectos ocultos.", OnboardingTrigger.UseMagnifier),
            new Step("DESTORNILLADOR: apoya la punta en el tornillo flojo y mantén el gatillo.", OnboardingTrigger.UseScrewdriver),
            new Step("MARTILLO: golpea la abolladura con fuerza para enderezarla.", OnboardingTrigger.UseHammer),
            new Step("Las grietas y circuitos quemados NO se reparan. Suelta el producto en el cubo de desecho.", OnboardingTrigger.ResolveProduct),
            new Step("Reporta el daño en el panel: elige qué defecto tenía el producto.", OnboardingTrigger.SubmitReport),
            new Step("¡Listo! Lo sano o reparado va al estante de su tipo. Presiona INICIAR TURNO.", OnboardingTrigger.PressStart),
        };

        int index;
        bool active;
        Vector3 lastPos;
        float lastYaw, accDistance, accAngle;

        void OnEnable()
        {
            GameEvents.GameStateChanged += OnState;
            GameEvents.ToolGrabbed += OnToolGrabbed;
            GameEvents.ToolUsed += OnToolUsed;
            GameEvents.ProductResolved += OnResolved;
            GameEvents.ReportSubmitted += OnReport;
            GameEvents.ButtonPressed += OnButton;
        }

        void OnDisable()
        {
            GameEvents.GameStateChanged -= OnState;
            GameEvents.ToolGrabbed -= OnToolGrabbed;
            GameEvents.ToolUsed -= OnToolUsed;
            GameEvents.ProductResolved -= OnResolved;
            GameEvents.ReportSubmitted -= OnReport;
            GameEvents.ButtonPressed -= OnButton;
        }

        void OnState(GameState s)
        {
            if (s != GameState.Onboarding) return;
            if (rig == null) rig = transform;
            lastPos = rig.position;
            lastYaw = rig.eulerAngles.y;
            active = true;
            index = 0;
            ShowCurrent();
        }

        // ── Detección de movimiento del jugador ──
        void Update()
        {
            if (!active) return;
            Vector3 p = rig.position;
            accDistance += Vector2.Distance(new Vector2(p.x, p.z), new Vector2(lastPos.x, lastPos.z));
            lastPos = p;
            float yaw = rig.eulerAngles.y;
            accAngle += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
            lastYaw = yaw;

            if (accDistance >= moveDistance || accAngle >= turnAngle)
            {
                accDistance = accAngle = 0f;
                Try(OnboardingTrigger.MoveAround);
            }
        }

        // ── Eventos que completan pasos ──
        void OnToolGrabbed(ToolType t) => Try(OnboardingTrigger.GrabTool);
        void OnResolved(Product p, ProductOutcome o) => Try(OnboardingTrigger.ResolveProduct);
        void OnReport(DefectType t, bool ok) => Try(OnboardingTrigger.SubmitReport);

        void OnToolUsed(ToolType t)
        {
            if (t == ToolType.Magnifier) Try(OnboardingTrigger.UseMagnifier);
            else if (t == ToolType.Screwdriver) Try(OnboardingTrigger.UseScrewdriver);
            else if (t == ToolType.Hammer) Try(OnboardingTrigger.UseHammer);
        }

        void OnButton(string id)
        {
            if (!active) return;
            if (id == skipButtonId) Finish();
            else if (id == startButtonId) Try(OnboardingTrigger.PressStart);
        }

        void Try(OnboardingTrigger trigger)
        {
            if (!active || steps[index].completeOn != trigger) return;
            index++;
            if (index >= steps.Count) Finish(); else ShowCurrent();
        }

        void ShowCurrent() => GameEvents.RaiseInstructionChanged(steps[index].instruction, index + 1, steps.Count);

        void Finish()
        {
            active = false;
            GameEvents.RaiseInstructionChanged(null, 0, 0);
            GameEvents.RaiseOnboardingCompleted();
        }
    }
}
