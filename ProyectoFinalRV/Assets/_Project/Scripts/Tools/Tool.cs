using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace QCXR
{
    /// Una sola herramienta configurable: Lupa, Destornillador o Martillo (campo Tool Type).
    /// Cada comportamiento es un método separado (UpdateHammer/UpdateScrewdriver/UpdateMagnifier).
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public class Tool : MonoBehaviour
    {
        [SerializeField] ToolType toolType;
        [SerializeField] Transform tip;
        [SerializeField] float tipRadius = 0.04f;
        [SerializeField] LayerMask defectMask;       // capa "Defect"
        [SerializeField] float fallY = -0.5f;        // si cae bajo esta altura vuelve a su mesa

        [Header("Martillo")]
        [SerializeField] float minHitSpeed = 1.2f;
        [SerializeField] float hitCooldown = 0.35f;

        [Header("Destornillador")]
        [SerializeField] Transform spinPart;
        [SerializeField] float sfxInterval = 0.25f;

        [Header("Lupa")]
        [SerializeField] Transform lens;
        [SerializeField] Light lensLight;
        [SerializeField] float revealDistance = 0.30f;
        [SerializeField] float revealAngle = 25f;
        [SerializeField] float revealTime = 0.8f;

        static readonly Collider[] hits = new Collider[8];
        readonly Dictionary<ProductDefect, float> revealProgress = new Dictionary<ProductDefect, float>();

        XRGrabInteractable grab;
        IXRSelectInteractor holder;
        Vector3 homePos, lastTipPos;
        Quaternion homeRot;
        float lastHit = -10f, nextSfx;
        bool isHeld, triggerPressed;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (tip == null) tip = transform;
            homePos = transform.position;
            homeRot = transform.rotation;
            lastTipPos = tip.position;
        }

        void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrab);
            grab.selectExited.AddListener(OnRelease);
            grab.activated.AddListener(OnActivate);
            grab.deactivated.AddListener(OnDeactivate);
        }

        void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
            grab.activated.RemoveListener(OnActivate);
            grab.deactivated.RemoveListener(OnDeactivate);
        }

        // ── Agarre ──
        void OnGrab(SelectEnterEventArgs a)
        {
            if (a.interactorObject is XRSocketInteractor) return;   // guardada en un soporte
            holder = a.interactorObject;
            isHeld = true;
            if (lensLight) lensLight.enabled = true;
            GameEvents.RaiseToolGrabbed(toolType);
            GameEvents.RaiseSfx(SfxId.Grab, transform.position);
        }

        void OnRelease(SelectExitEventArgs a)
        {
            if (!isHeld) return;
            isHeld = false; triggerPressed = false; holder = null;
            if (lensLight) lensLight.enabled = false;
            revealProgress.Clear();
        }

        void OnActivate(ActivateEventArgs a) => triggerPressed = true;
        void OnDeactivate(DeactivateEventArgs a) => triggerPressed = false;

        // ── Bucle ──
        void FixedUpdate()
        {
            float speed = (tip.position - lastTipPos).magnitude / Time.fixedDeltaTime;
            lastTipPos = tip.position;
            if (!isHeld) return;

            if (toolType == ToolType.Hammer) UpdateHammer(speed);
            else if (toolType == ToolType.Screwdriver) UpdateScrewdriver();
        }

        void Update()
        {
            if (toolType == ToolType.Magnifier && isHeld) UpdateMagnifier();
            if (transform.position.y < fallY) ReturnHome();
        }

        // ── Comportamientos ──
        void UpdateHammer(float speed)
        {
            if (speed < minHitSpeed || Time.time - lastHit < hitCooldown) return;
            var defect = FindDefectAtTip();
            if (defect == null) return;

            lastHit = Time.time;
            GameEvents.RaiseSfx(SfxId.Hit, tip.position);
            HapticUtility.Send(holder, Mathf.Clamp01(speed / 4f), 0.1f);
            defect.ApplyWork(toolType, 1f);
        }

        void UpdateScrewdriver()
        {
            if (!triggerPressed) return;
            var defect = FindDefectAtTip();
            if (defect == null || !defect.ApplyWork(toolType, Time.fixedDeltaTime)) return;

            HapticUtility.Send(holder, 0.15f, 0.05f);
            if (spinPart) spinPart.Rotate(0f, 0f, 720f * Time.fixedDeltaTime, Space.Self);
            if (Time.time >= nextSfx) { nextSfx = Time.time + sfxInterval; GameEvents.RaiseSfx(SfxId.Screw, tip.position); }
        }

        void UpdateMagnifier()
        {
            foreach (var product in Product.Active)
            {
                foreach (var defect in product.Defects)
                {
                    if (defect.State != DefectState.Hidden) continue;

                    Vector3 toDefect = defect.transform.position - lens.position;
                    bool inView = toDefect.magnitude <= revealDistance && Vector3.Angle(lens.forward, toDefect) <= revealAngle;
                    if (!inView) { revealProgress.Remove(defect); continue; }

                    revealProgress.TryGetValue(defect, out float t);
                    t += Time.deltaTime;
                    if (t >= revealTime)
                    {
                        revealProgress.Remove(defect);
                        defect.Reveal();
                        GameEvents.RaiseToolUsed(toolType);
                        HapticUtility.Send(holder, 0.4f, 0.2f);
                    }
                    else revealProgress[defect] = t;
                }
            }
        }

        ProductDefect FindDefectAtTip()
        {
            int n = Physics.OverlapSphereNonAlloc(tip.position, tipRadius, hits, defectMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var d = hits[i].GetComponentInParent<ProductDefect>();
                if (d != null && d.State == DefectState.Visible) return d;
            }
            return null;
        }

        void ReturnHome()
        {
            var rb = GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(homePos, homeRot);
        }

        void OnDrawGizmosSelected()
        {
            if (tip == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(tip.position, tipRadius);
        }
    }

    /// ÚNICO lugar que toca la API de vibración (XRI 3.x): busca el HapticImpulsePlayer
    /// del controlador (viene en los prefabs de Starter Assets) y le manda el pulso.
    public static class HapticUtility
    {
        public static void Send(IXRInteractor interactor, float amplitude, float duration)
        {
            if (interactor == null || interactor.transform == null) return;
            var player = interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(amplitude, duration);
        }
    }
}
