using UnityEngine;

namespace QCXR.MenuReferenciaV6
{
    // Cinta visual independiente: no mueve ni instancia productos reales.
    // Al salir del menú no altera ConveyorBelt ni ProductFlow.
    public sealed class QCXRRefV6Belt : MonoBehaviour
    {
        [SerializeField] private Renderer superficie;
        [SerializeField] private float velocidadUV = 0.17f;
        [SerializeField] private Transform productoDecorativo;
        private Material instancia;
        private Vector3 posProducto;

        private void Awake()
        {
            if (superficie != null)
            {
                instancia = superficie.material;
            }
            if (productoDecorativo != null) posProducto = productoDecorativo.localPosition;
        }
        private void Update()
        {
            if (instancia != null)
            {
                string prop = instancia.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (instancia.HasProperty(prop))
                    instancia.SetTextureOffset(prop, new Vector2(0f, Time.time * velocidadUV));
            }
            if (productoDecorativo != null)
            {
                var p = posProducto;
                p.x += Mathf.Sin(Time.time * 0.6f) * 0.09f;
                productoDecorativo.localPosition = p;
            }
        }
        private void OnDestroy()
        {
            if (instancia != null) Destroy(instancia);
        }
    }
}
