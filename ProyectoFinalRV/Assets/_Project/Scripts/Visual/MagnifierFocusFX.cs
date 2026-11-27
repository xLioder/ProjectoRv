using UnityEngine;

namespace QCXR.Visual
{
    /// Respuesta VISUAL de la lupa: mientras se sujeta y apunta a un defecto OCULTO dentro de su cono de visión,
    /// activa el bool "Focus" del Animator (aro cian pulsante = "aquí hay algo, mantén la lupa").
    /// Solo lee el estado (Product.Active / ProductDefect.State); revelar sigue siendo trabajo de Tool.cs.
    /// Distancia y ángulo por defecto = los de Tool.cs (revealDistance 0.30 m, revealAngle 25°).
    [RequireComponent(typeof(Animator))]
    public class MagnifierFocusFX : MonoBehaviour
    {
        [SerializeField] Transform lens;          // LensPoint (+Z = dirección de observación)
        [SerializeField] Light lensLight;         // Tool.cs la enciende solo mientras la lupa está agarrada
        [SerializeField] float distance = 0.30f;
        [SerializeField] float angle = 25f;

        static readonly int FocusId = Animator.StringToHash("Focus");
        Animator anim;
        bool focused;

        void Awake() => anim = GetComponent<Animator>();

        void Update()
        {
            bool now = lens != null && lensLight != null && lensLight.enabled && AnyHiddenInView();
            if (now == focused) return;
            focused = now;
            anim.SetBool(FocusId, now);
        }

        bool AnyHiddenInView()
        {
            foreach (var product in Product.Active)
            {
                if (product == null) continue;
                foreach (var d in product.Defects)
                {
                    if (d == null || d.State != DefectState.Hidden) continue;
                    Vector3 to = d.transform.position - lens.position;
                    if (to.magnitude <= distance && Vector3.Angle(lens.forward, to) <= angle) return true;
                }
            }
            return false;
        }
    }
}
