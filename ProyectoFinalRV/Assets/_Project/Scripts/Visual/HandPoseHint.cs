using UnityEngine;

namespace QCXR.Visual
{
    // Poses de agarre disponibles en el Animator de las manos (parámetro entero "Pose").
    public enum HandPose { None = 0, Object = 1, Hammer = 2, Screwdriver = 3, Magnifier = 4, Press = 5, Inspect = 6 }

    // Marca visual: indica qué pose de mano usar al agarrar este objeto.
    // Sin lógica de juego. Los objetos sin este componente usan la pose genérica "Object".
    [DisallowMultipleComponent]
    public class HandPoseHint : MonoBehaviour
    {
        [SerializeField] HandPose pose = HandPose.Object;
        public HandPose Pose => pose;
    }
}
