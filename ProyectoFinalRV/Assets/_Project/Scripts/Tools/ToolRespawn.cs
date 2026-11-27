using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace QCXR
{
    /// Devuelve la herramienta a su sitio de la mesa cuando se cae o queda fuera del alcance del jugador
    /// (el jugador está fijo en la estación y no puede ir a recogerla).
    ///  · Solo actúa si NADIE la sujeta.
    ///  · "Perdida" = por debajo de minHeight (suelo o repisa inferior del banco; la superficie está a 0.8 m)
    ///    o a más de maxDistance (horizontal) de su sitio, de forma continuada durante `delay` segundos
    ///    (se la ve caer y después reaparece).
    ///  · El sitio es la pose que tiene al empezar la escena (la misma que guarda Tool.cs como "home").
    /// No modifica Tool.cs: su ReturnHome (fallY) sigue existiendo como último recurso.
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class ToolRespawn : MonoBehaviour
    {
        [Tooltip("Altura (m) por debajo de la cual se considera caída. Banco a 0.8 m, repisa inferior a 0.22 m.")]
        [SerializeField] float minHeight = 0.55f;
        [Tooltip("Distancia horizontal máxima (m) a su sitio antes de considerarla fuera de alcance.")]
        [SerializeField] float maxDistance = 0.9f;
        [Tooltip("Segundos que debe cumplirse la condición antes de reaparecer.")]
        [SerializeField] float delay = 0.8f;
        [Tooltip("Duración (s) del pequeño efecto de escala al reaparecer.")]
        [SerializeField] float popDuration = 0.25f;

        XRGrabInteractable grab;
        Rigidbody body;
        Vector3 homePos, baseScale;
        Quaternion homeRot;
        float lostTime;
        bool captured;
        Coroutine pop;

        public Vector3 HomePosition => homePos;
        public bool IsLost { get; private set; }

        void Awake() => CaptureHome();

        /// Guarda la pose actual como sitio de la herramienta (se llama solo en Awake).
        public void CaptureHome()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            homePos = transform.position;
            homeRot = transform.rotation;
            baseScale = transform.localScale;
            captured = true;
        }

        void Update() => Tick(Time.deltaTime);

        /// Un paso de la comprobación (separado de Update para poder probarlo).
        public void Tick(float dt)
        {
            if (!captured) CaptureHome();
            if (grab.isSelected) { lostTime = 0f; IsLost = false; return; }

            Vector3 p = transform.position;
            float flat = new Vector2(p.x - homePos.x, p.z - homePos.z).magnitude;
            IsLost = p.y < minHeight || flat > maxDistance;
            lostTime = IsLost ? lostTime + dt : 0f;
            if (lostTime >= delay) RespawnNow();
        }

        /// Devuelve la herramienta a su sitio inmediatamente (si nadie la sujeta).
        public void RespawnNow()
        {
            if (grab.isSelected) return;
            lostTime = 0f;
            IsLost = false;
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(homePos, homeRot);
            body.position = homePos;
            body.rotation = homeRot;

            if (Application.isPlaying)
            {
                GameEvents.RaiseSfx(SfxId.Spawn, homePos);
                if (pop != null) StopCoroutine(pop);
                pop = StartCoroutine(Pop());
            }
        }

        // Efecto de reaparición: crece de 0.6 a 1 con salida suave. Si la agarran a mitad, termina al instante.
        IEnumerator Pop()
        {
            for (float t = 0f; t < popDuration; t += Time.deltaTime)
            {
                if (grab.isSelected) break;
                float k = t / popDuration;
                transform.localScale = baseScale * Mathf.Lerp(0.6f, 1f, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            transform.localScale = baseScale;
            pop = null;
        }

        void OnDisable()
        {
            if (pop != null) { StopCoroutine(pop); pop = null; }
            if (captured) transform.localScale = baseScale;
        }
    }
}
