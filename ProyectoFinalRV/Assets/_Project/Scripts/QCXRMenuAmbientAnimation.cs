using UnityEngine;

namespace QCXR.MenuImmersive
{
    /// <summary>Decoracion ligera, sin Rigidbody ni interacciones; apta para Meta Quest 2.</summary>
    public sealed class QCXRMenuAmbientAnimation : MonoBehaviour
    {
        public enum Animacion { Rotacion, Oscilacion, LuzIntermitente }
        [SerializeField] private Animacion tipo;
        [SerializeField] private Vector3 eje = Vector3.forward;
        [SerializeField] private float velocidad = 32f;
        [SerializeField] private float amplitud = .035f;
        [SerializeField] private float frecuencia = 1.2f;
        private Vector3 posicionInicial;
        private Light luz;
        private float luzBase;
        private float fase;

        public void ConfigurarRotacion(Vector3 ejeLocal, float gradosPorSegundo)
        {
            tipo = Animacion.Rotacion; eje = ejeLocal; velocidad = gradosPorSegundo;
        }
        public void ConfigurarOscilacion(Vector3 ejeLocal, float metros, float ciclos)
        {
            tipo = Animacion.Oscilacion; eje = ejeLocal; amplitud = metros; frecuencia = ciclos;
        }
        public void ConfigurarLuz(float ciclos)
        {
            tipo = Animacion.LuzIntermitente; frecuencia = ciclos;
        }
        private void Awake()
        {
            posicionInicial = transform.localPosition;
            luz = GetComponent<Light>();
            if (luz) luzBase = luz.intensity;
            fase = transform.GetSiblingIndex() * 0.77f;
        }
        private void Update()
        {
            if (tipo == Animacion.Rotacion)
                transform.Rotate(eje, velocidad * Time.deltaTime, Space.Self);
            else if (tipo == Animacion.Oscilacion)
                transform.localPosition = posicionInicial + eje.normalized * (Mathf.Sin(Time.time * frecuencia + fase) * amplitud);
            else if (luz)
                luz.intensity = luzBase * (.66f + .34f * Mathf.Sin(Time.time * frecuencia * 6.283185f));
        }
    }
}
