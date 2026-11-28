using TMPro;
using UnityEngine;

namespace QCXR
{
    // Cambia textos, tipografia y colores desde un asset en el Inspector.
    [CreateAssetMenu(fileName = "QCXRMenuFinalContent", menuName = "QCXR/Menu final/Contenido editable")]
    public sealed class QCXRMenuFinalContent : ScriptableObject
    {
        [Header("Textos editables")]
        public string insignia = "UAO  /  CONTROL INDUSTRIAL";
        [TextArea(1, 3)] public string titulo = "CONTROL DE CALIDAD XR";
        [TextArea(2, 4)] public string descripcion = "Inspecciona, repara y clasifica los productos de la linea de produccion.";
        public string botonInicio = "INICIAR INSPECCION";
        public string botonSalir = "SALIR";
        public string tarjetaTitulo = "ORDEN DE TRABAJO";
        [TextArea(3, 6)] public string tarjetaDetalle = "Recibe objetos de la banda, identifica sus defectos y decide si se reparan o descartan.";
        public string instruccionInferior = "El tutorial se inicia al entrar a la fabrica.";
        public string indicadorEstado = "SISTEMA EN ESPERA  /  ESTACION 01";

        [Header("Fuente TextMeshPro opcional")]
        public TMP_FontAsset tipografia;

        [Header("Colores industriales")]
        public Color fondo = new Color(0.08f, 0.10f, 0.12f, 1f);
        public Color superficie = new Color(0.14f, 0.17f, 0.19f, 1f);
        public Color naranja = new Color(0.93f, 0.41f, 0.13f, 1f);
        public Color verde = new Color(0.28f, 0.85f, 0.48f, 1f);
        public Color texto = new Color(0.96f, 0.95f, 0.91f, 1f);
    }
}
