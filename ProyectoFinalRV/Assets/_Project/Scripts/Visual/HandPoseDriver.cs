using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QCXR.Visual
{
    // Capa visual de las manos: traduce gatillo/grip y los eventos de agarre de XRI a parámetros
    // del Animator. No contiene lógica de juego, no toca GameEvents y no mueve la mano
    // (la posición siempre la da el controlador real).
    //
    // Animator esperado (lo genera QCXRHandBuilder):
    //   Capa 0 "Base"   : blend 2D Grip × Trigger (abierta / señalar / índice / puño)
    //   Capa 1 "Hold"   : poses de agarre por herramienta (peso controlado aquí)
    //   Capa 2 "Action" : aditiva (golpe, pulsar, girar, inspeccionar)
    [RequireComponent(typeof(Animator))]
    public class HandPoseDriver : MonoBehaviour
    {
        [Header("Entrada (Starter Assets)")]
        [SerializeField] InputActionProperty gripInput;
        [SerializeField] InputActionProperty triggerInput;

        [Header("Interactores de esta mano (se asignan en la escena)")]
        [SerializeField] XRBaseInteractor[] interactors;

        [Header("Ajustes")]
        [SerializeField] float inputSmoothing = 20f;
        [SerializeField] float holdBlendSpeed = 10f;
        [SerializeField] float strikeSpeed = 1.4f;
        [SerializeField] float strikeCooldown = 0.3f;

        static readonly int GripId = Animator.StringToHash("Grip");
        static readonly int TriggerId = Animator.StringToHash("Trigger");
        static readonly int PoseId = Animator.StringToHash("Pose");
        static readonly int StrikeId = Animator.StringToHash("Strike");
        static readonly int PressId = Animator.StringToHash("Press");
        static readonly int TwistId = Animator.StringToHash("Twist");
        static readonly int InspectId = Animator.StringToHash("Inspect");
        const int HoldLayer = 1;

        Animator anim;
        float grip, trigger, holdWeight, lastStrike = -10f;
        HandPose pose;
        Vector3 lastPos;
        bool hasLastPos;

        public HandPose CurrentPose => pose;

        void Awake() => anim = GetComponent<Animator>();

        void OnEnable()
        {
            if (interactors == null) return;
            foreach (var i in interactors)
            {
                if (i == null) continue;
                i.selectEntered.AddListener(OnSelect);
                i.selectExited.AddListener(OnDeselect);
            }
        }

        void OnDisable()
        {
            if (interactors == null) return;
            foreach (var i in interactors)
            {
                if (i == null) continue;
                i.selectEntered.RemoveListener(OnSelect);
                i.selectExited.RemoveListener(OnDeselect);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-inputSmoothing * dt);
            grip = Mathf.Lerp(grip, Read(gripInput), k);
            trigger = Mathf.Lerp(trigger, Read(triggerInput), k);
            anim.SetFloat(GripId, grip);
            anim.SetFloat(TriggerId, trigger);

            holdWeight = Mathf.MoveTowards(holdWeight, pose == HandPose.None ? 0f : 1f, holdBlendSpeed * dt);
            anim.SetLayerWeight(HoldLayer, holdWeight);

            anim.SetBool(TwistId, pose == HandPose.Screwdriver && trigger > 0.5f);
            anim.SetBool(InspectId, pose == HandPose.Magnifier);

            // Apretón visual al golpear con el martillo (solo dedos; el golpe real lo detecta Tool.cs)
            Vector3 p = transform.position;
            if (pose == HandPose.Hammer && hasLastPos && dt > 0f)
            {
                float speed = (p - lastPos).magnitude / dt;
                if (speed > strikeSpeed && Time.time - lastStrike > strikeCooldown)
                {
                    lastStrike = Time.time;
                    anim.SetTrigger(StrikeId);
                }
            }
            lastPos = p;
            hasLastPos = true;
        }

        static float Read(InputActionProperty p)
        {
            var a = p.action;
            return a != null ? a.ReadValue<float>() : 0f;
        }

        void OnSelect(SelectEnterEventArgs args)
        {
            if (args.interactableObject is XRSimpleInteractable) { PlayPress(); return; }   // botones físicos
            var hint = args.interactableObject.transform.GetComponentInParent<HandPoseHint>();
            SetPose(hint != null ? hint.Pose : HandPose.Object);
        }

        void OnDeselect(SelectExitEventArgs args)
        {
            if (args.interactableObject is XRSimpleInteractable) return;
            SetPose(HandPose.None);
        }

        // Fija la pose de agarre. None = la mano vuelve a la pose natural controlada por grip/gatillo.
        public void SetPose(HandPose p)
        {
            pose = p;
            if (p != HandPose.None) anim.SetInteger(PoseId, (int)p);   // al soltar se conserva para el fundido de salida
        }

        // Animación de pulsar un botón. También se puede llamar desde un UnityEvent (p. ej. XRButton.onPressed).
        public void PlayPress() => anim.SetTrigger(PressId);
    }
}
