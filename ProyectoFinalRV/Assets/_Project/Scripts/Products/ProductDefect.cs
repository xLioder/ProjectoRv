using UnityEngine;
using UnityEngine.Events;

namespace QCXR
{
    // Un "slot" de defecto dentro de un producto. La definición va aquí mismo (sin assets extra).
    public class ProductDefect : MonoBehaviour
    {
        [Header("Definición")]
        [SerializeField] DefectType type;
        [SerializeField] ToolType requiredTool = ToolType.None;
        [SerializeField] float workRequired = 1f;           // golpes (martillo) o segundos (destornillador)
        [SerializeField] bool repairable = true;            // false = desechar y reportar
        [SerializeField] bool hiddenUntilInspected = false; // true = la lupa lo revela

        [Header("Visual")]
        [SerializeField] GameObject defectVisual;
        [SerializeField] GameObject fixedVisual;            // opcional
        [SerializeField] Collider workCollider;             // capa "Defect"
        [SerializeField] UnityEvent onFixed;

        float work, lastRejectTime = -10f;

        public DefectType Type => type;
        public bool Repairable => repairable;
        public DefectState State { get; private set; } = DefectState.Inactive;
        public bool IsPending => State == DefectState.Hidden || State == DefectState.Visible;

        void Awake() => Refresh();

        public void Activate()
        {
            work = 0f;
            State = hiddenUntilInspected ? DefectState.Hidden : DefectState.Visible;
            Refresh();
        }

        public void Deactivate() { State = DefectState.Inactive; Refresh(); }

        // Lo llama la lupa.
        public void Reveal()
        {
            if (State != DefectState.Hidden) return;
            State = DefectState.Visible;
            Refresh();
            GameEvents.RaiseSfx(SfxId.Reveal, transform.position);
            GameEvents.RaiseFeedback(repairable ? "¡Defecto encontrado!" : "¡Irreparable! Deséchalo y repórtalo");
        }

        // Lo llaman martillo y destornillador. true = trabajo aceptado.
        public bool ApplyWork(ToolType tool, float amount)
        {
            if (State != DefectState.Visible) return false;
            if (!repairable) return Reject("Irreparable: deséchalo y repórtalo");
            if (tool != requiredTool) return Reject("Herramienta incorrecta");

            work += amount;
            GameEvents.RaiseToolUsed(tool);
            if (work >= workRequired) Fix();
            return true;
        }

        bool Reject(string message)
        {
            if (Time.time - lastRejectTime > 1.5f)
            {
                lastRejectTime = Time.time;
                GameEvents.RaiseFeedback(message);
                GameEvents.RaiseSfx(SfxId.Fail, transform.position);
            }
            return false;
        }

        void Fix()
        {
            State = DefectState.Fixed;
            Refresh();
            onFixed?.Invoke();
            GameEvents.RaiseDefectFixed(this);
            GameEvents.RaiseSfx(SfxId.Fixed, transform.position);
        }

        void Refresh()
        {
            if (defectVisual) defectVisual.SetActive(State == DefectState.Visible);
            if (fixedVisual) fixedVisual.SetActive(State == DefectState.Fixed);
            if (workCollider) workCollider.enabled = State == DefectState.Visible;
        }
    }
}
