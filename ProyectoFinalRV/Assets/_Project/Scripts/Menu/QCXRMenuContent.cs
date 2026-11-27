using TMPro;
using UnityEngine;

namespace QCXR
{
    // EDITABLE: modifica este asset desde el Inspector, sin cambiar los scripts.
    [CreateAssetMenu(fileName = "QCXRMenuContent", menuName = "QCXR/Menu VR/Contenido editable")]
    public sealed class QCXRMenuContent : ScriptableObject
    {
        [Header("Identidad")]
        [TextArea(1, 2)] public string marca = "UAO  /  IMMERSIVE SOLUTIONS";
        [TextArea(2, 3)] public string titulo = "CONTROL DE\nCALIDAD XR";
        [TextArea(2, 5)] public string descripcion =
            "Bienvenido a la línea de inspección. Examina, repara y clasifica cada producto antes de que se acabe el turno.";
        public string etiquetaPrincipal = "INICIAR INSPECCIÓN";
        public string etiquetaTutorial = "CÓMO JUGAR";
        public string etiquetaSalir = "SALIR";
        public string etiquetaEstado = "SISTEMA LISTO  /  ESTACIÓN 01";

        [Header("Tarjeta informativa")]
        public string tarjetaTitulo = "TU MISIÓN";
        [TextArea(3, 6)] public string tarjetaDescripcion =
            "Inspecciona productos en una banda transportadora, repara los daños posibles y reporta los defectos irreparables.";
        public string tarjetaDato1 = "05  TIPOS DE PRODUCTO";
        public string tarjetaDato2 = "03  HERRAMIENTAS";
        public string tarjetaDato3 = "01  LÍNEA DE CALIDAD";

        [Header("Pantalla de instrucciones")]
        public string instruccionesTitulo = "MANUAL DEL INSPECTOR";
        [TextArea(8, 15)] public string instrucciones =
            "01  RECIBE EL PRODUCTO\nLa banda transportadora entrega los objetos de uno en uno.\n\n" +
            "02  EXAMINA Y REPARA\nUtiliza la lupa para inspeccionar; el destornillador y el martillo permiten resolver ciertos defectos.\n\n" +
            "03  DECIDE SU DESTINO\nEnvía los productos aptos a su estante. Los irreparables deben desecharse y reportarse.\n\n" +
            "04  CUIDA TU DESEMPEÑO\nEl tiempo, los aciertos y los fallos afectan tu resultado.\n\n" +
            "VR: apunta a los botones con el controlador y pulsa el gatillo.";
        public string etiquetaVolver = "VOLVER AL MENÚ";
        public string pie = "SIMULACIÓN DE ENTRENAMIENTO  /  UNITY XR";

        [Header("Tipografía (opcional)")]
        [Tooltip("Arrastra aquí un TMP Font Asset. Si se deja vacío, se usa la fuente predeterminada de TextMeshPro.")]
        public TMP_FontAsset fuentePersonalizada;

        [Header("Paleta de colores")]
        public Color fondo = new Color(0.025f, 0.051f, 0.086f, 1f);
        public Color superficie = new Color(0.061f, 0.115f, 0.164f, 1f);
        public Color acento = new Color(0.25f, 0.85f, 0.92f, 1f);
        public Color botonPrincipal = new Color(0.05f, 0.51f, 0.62f, 1f);
        public Color textoPrincipal = new Color(0.96f, 0.98f, 1f, 1f);
    }
}
