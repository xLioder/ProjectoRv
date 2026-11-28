using TMPro;
using UnityEngine;

namespace QCXR.MenuImmersive
{
    // Cambiar desde Inspector. No depende de GameManager ni de los scripts del juego.
    [CreateAssetMenu(fileName = "QCXRMenuImmersiveContent", menuName = "QCXR/Menu inmersivo/Contenido")]
    public sealed class QCXRImmersiveMenuContent : ScriptableObject
    {
        [Header("Textos del terminal")]
        public string cabecera = "UAO IMMERSIVE SOLUTIONS   /   PLANTA 01";
        [TextArea(1, 3)] public string titulo = "CONTROL DE\nCALIDAD XR";
        [TextArea(2, 4)] public string bajada = "Toma tu puesto en la linea de inspeccion.";
        public string iniciar = "INICIAR TURNO";
        public string salir = "SALIR";
        public string estado = "SISTEMA PREPARADO   /   ESTACION 01";
        [TextArea(2, 4)] public string detalle = "Inspecciona  ·  Repara  ·  Clasifica";
        public string ayuda = "Al comenzar, sigue el tutorial de la fabrica.";
        public string marcaEscenario = "CONTROL DE CALIDAD";
        [Header("Tipografia y colores")]
        public TMP_FontAsset fuente;
        public Color naranja = new Color(0.92f, 0.37f, 0.10f, 1f);
        public Color azulGrafito = new Color(0.055f, 0.088f, 0.11f, 1f);
        public Color blanco = new Color(0.93f, 0.92f, 0.87f, 1f);
        public Color verde = new Color(0.25f, 0.84f, 0.43f, 1f);
    }
}
