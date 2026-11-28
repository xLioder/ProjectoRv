using TMPro;
using UnityEngine;

namespace QCXR.MenuSpatial
{
    [CreateAssetMenu(fileName = "QCXRMenuEspacialContent", menuName = "QCXR/Menu espacial/Contenido editable")]
    public sealed class QCXRSpatialMenuContent : ScriptableObject
    {
        [Header("Textos del espacio VR")]
        public string superior = "UAO IMMERSIVE SOLUTIONS    /    PLANTA 01";
        [TextArea(2, 3)] public string titulo = "CONTROL DE\nCALIDAD XR";
        public string subtitulo = "CENTRO DE INSPECCION INDUSTRIAL";
        public string iniciar = "INICIAR INSPECCION";
        public string salir = "SALIR";
        public string zonaIzquierda = "RECEPCION DE PRODUCTOS";
        public string zonaDerecha = "CONTROL DE CALIDAD";
        public string estado = "LINEA LISTA   /   TURNO 01";
        public string pie = "INSPECCIONA   /   REPARA   /   CLASIFICA";
        [Header("Estilo editable")]
        public TMP_FontAsset fuentePersonalizada;
        public Color texto = new Color(.96f, .94f, .86f, 1f);
        public Color acentoNaranja = new Color(.93f, .38f, .11f, 1f);
        public Color acentoVerde = new Color(.26f, .88f, .51f, 1f);
        public Color tarjetaBoton = new Color(.065f, .095f, .12f, .75f);
    }
}
