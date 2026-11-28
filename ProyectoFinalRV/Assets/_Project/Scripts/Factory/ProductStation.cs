using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QCXR
{
    public enum StationKind { Shelf, DiscardBin }

    // Estación de entrega. Shelf: estante de un tipo (XR Socket Interactor en el mismo objeto).
    // DiscardBin: cubo con collider trigger; si el descarte es correcto abre y valida el reporte.
    public class ProductStation : MonoBehaviour
    {
        [SerializeField] StationKind kind;

        [Header("Estante")]
        [SerializeField] ProductType acceptedType;
        [SerializeField] TMP_Text label;
        [SerializeField] string labelText;

        [Header("Cubo de desecho")]
        [SerializeField] float reportWindow = 12f;

        XRSocketInteractor socket;
        readonly System.Collections.Generic.HashSet<DefectType> actualDefects = new System.Collections.Generic.HashSet<DefectType>();
        bool reportOpen;
        float reportDeadline;

        void Awake()
        {
            if (kind != StationKind.Shelf) return;
            socket = GetComponent<XRSocketInteractor>();
            if (socket == null) Debug.LogError("ProductStation (Shelf) necesita un XRSocketInteractor.", this);
            if (label) label.text = string.IsNullOrEmpty(labelText) ? acceptedType.ToString() : labelText;
        }

        void OnEnable()
        {
            if (kind == StationKind.Shelf && socket != null) socket.selectEntered.AddListener(OnPlaced);
            if (kind == StationKind.DiscardBin) GameEvents.ReportChoiceMade += OnReportChoice;
        }

        void OnDisable()
        {
            if (kind == StationKind.Shelf && socket != null) socket.selectEntered.RemoveListener(OnPlaced);
            if (kind == StationKind.DiscardBin) GameEvents.ReportChoiceMade -= OnReportChoice;
        }

        // Estante
        void OnPlaced(SelectEnterEventArgs args)
        {
            var p = args.interactableObject.transform.GetComponentInParent<Product>();
            if (p == null || p.IsResolved) return;

            var outcome = ProductEvaluator.EvaluatePlacement(p, acceptedType);
            GameEvents.RaiseSfx(outcome.IsSuccess() ? SfxId.Success : SfxId.Fail, transform.position);
            p.Resolve(outcome);
        }

        // Cubo de desecho
        void OnTriggerStay(Collider other)
        {
            if (kind != StationKind.DiscardBin) return;
            var p = other.GetComponentInParent<Product>();
            if (p == null || p.IsResolved || p.State == ProductState.Held) return;   // hay que soltarlo dentro

            var outcome = ProductEvaluator.EvaluateDiscard(p);
            GameEvents.RaiseSfx(outcome.IsSuccess() ? SfxId.Success : SfxId.Fail, transform.position);

            actualDefects.Clear();
            foreach (var t in p.UnrepairableTypes) actualDefects.Add(t);   // leer antes de que el producto desaparezca
            p.Resolve(outcome);

            if (outcome == ProductOutcome.DiscardedCorrect)
            {
                reportOpen = true;
                reportDeadline = Time.time + reportWindow;
                GameEvents.RaiseReportRequested(p);
            }
        }

        void OnReportChoice(DefectType choice)
        {
            if (!reportOpen) return;
            reportOpen = false;
            bool correct = actualDefects.Contains(choice);
            GameEvents.RaiseReportSubmitted(choice, correct);
            GameEvents.RaiseSfx(correct ? SfxId.Report : SfxId.Fail, transform.position);
        }

        void Update()
        {
            if (kind != StationKind.DiscardBin || !reportOpen || Time.time <= reportDeadline) return;
            reportOpen = false;
            GameEvents.RaiseReportDismissed();
            GameEvents.RaiseFeedback("Reporte no enviado");
        }
    }
}
