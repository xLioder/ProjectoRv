using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QCXR.MenuSpatial
{
    // Cambia sutilmente el aspecto al recibir el puntero XR.
    public sealed class QCXRSpatialButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image fondo;
        [SerializeField] private Color normal = new Color(.065f, .095f, .12f, .75f);
        [SerializeField] private Color resaltado = new Color(.85f, .38f, .10f, .94f);
        private void OnEnable() { if (fondo) fondo.color = normal; }
        public void OnPointerEnter(PointerEventData eventData) { if (fondo) fondo.color = resaltado; }
        public void OnPointerExit(PointerEventData eventData) { if (fondo) fondo.color = normal; }
        public void Configurar(Image imagen) { fondo = imagen; }
    }
}
