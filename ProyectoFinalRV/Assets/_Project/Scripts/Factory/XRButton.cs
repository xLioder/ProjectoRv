using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QCXR
{
    // Botón físico 3D. Emite ButtonPressed(id): "StartShift", "SkipTutorial", "ResetBelt"...
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class XRButton : MonoBehaviour
    {
        [SerializeField] string buttonId = "Button";
        [SerializeField] float cooldown = 0.5f;
        [SerializeField] UnityEvent onPressed;

        XRSimpleInteractable interactable;
        float nextAllowed;

        void Awake() => interactable = GetComponent<XRSimpleInteractable>();
        void OnEnable()  => interactable.selectEntered.AddListener(Press);
        void OnDisable() => interactable.selectEntered.RemoveListener(Press);

        void Press(SelectEnterEventArgs args)
        {
            if (Time.time < nextAllowed) return;
            nextAllowed = Time.time + cooldown;
            HapticUtility.Send(args.interactorObject, 0.3f, 0.1f);
            GameEvents.RaiseSfx(SfxId.Click, transform.position);
            onPressed?.Invoke();
            GameEvents.RaiseButtonPressed(buttonId);
        }
    }
}
