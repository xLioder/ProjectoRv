using UnityEngine;

namespace QCXR.MenuSpatial
{
    // Animacion decorativa, sin Rigidbody, independiente de la logica del juego.
    public sealed class QCXRSpatialAmbientAnimation : MonoBehaviour
    {
        public enum Modo { Giro, Flotacion, Baliza }
        [SerializeField] private Modo modo;
        [SerializeField] private Vector3 eje = Vector3.up;
        [SerializeField] private float velocidad = 30f;
        [SerializeField] private float amplitud = .015f;
        [SerializeField] private float frecuencia = 1.1f;
        private Vector3 inicial;
        private Light luz;
        private float intensidad;
        private float fase;

        public void Giro(Vector3 direccion, float grados) { modo = Modo.Giro; eje = direccion; velocidad = grados; }
        public void Flotar(float metros, float ciclos) { modo = Modo.Flotacion; amplitud = metros; frecuencia = ciclos; }
        public void Parpadear(float ciclos) { modo = Modo.Baliza; frecuencia = ciclos; }
        private void Awake()
        {
            inicial = transform.localPosition;
            luz = GetComponent<Light>();
            if (luz) intensidad = luz.intensity;
            fase = transform.GetSiblingIndex() * .61f;
        }
        private void Update()
        {
            if (modo == Modo.Giro) transform.Rotate(eje, velocidad * Time.deltaTime, Space.Self);
            else if (modo == Modo.Flotacion)
                transform.localPosition = inicial + Vector3.up * (Mathf.Sin(Time.time * frecuencia * 6.283185f + fase) * amplitud);
            else if (modo == Modo.Baliza && luz)
                luz.intensity = intensidad * (.72f + .28f * Mathf.Sin(Time.time * frecuencia * 6.283185f));
        }
    }
}
