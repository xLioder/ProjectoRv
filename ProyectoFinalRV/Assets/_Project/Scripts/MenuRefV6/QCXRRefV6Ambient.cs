using UnityEngine;

namespace QCXR.MenuReferenciaV6
{
    // Animación decorativa sin impacto en la mecánica del juego.
    public sealed class QCXRRefV6Ambient : MonoBehaviour
    {
        public enum Efecto { Flotar, Girar, Oscilar, Pulsar }
        [SerializeField] private Efecto efecto = Efecto.Flotar;
        [SerializeField] private float velocidad = 1f;
        [SerializeField] private float intensidad = 0.015f;
        private Vector3 posInicial;
        private Vector3 escalaInicial;
        private Quaternion rotInicial;

        private void Awake()
        {
            posInicial = transform.localPosition;
            escalaInicial = transform.localScale;
            rotInicial = transform.localRotation;
        }
        private void Update()
        {
            float t = Time.time * velocidad;
            switch (efecto)
            {
                case Efecto.Flotar:
                    transform.localPosition = posInicial + Vector3.up * (Mathf.Sin(t) * intensidad);
                    break;
                case Efecto.Girar:
                    transform.localRotation = rotInicial * Quaternion.Euler(0, t * intensidad * 45f, 0);
                    break;
                case Efecto.Oscilar:
                    transform.localRotation = rotInicial * Quaternion.Euler(0, Mathf.Sin(t) * intensidad, 0);
                    break;
                case Efecto.Pulsar:
                    transform.localScale = escalaInicial * (1f + Mathf.Sin(t) * intensidad);
                    break;
            }
        }
    }
}
