using UnityEngine;

namespace QCXR.Menu
{
    /// Animación ligera para dar vida al menú:
    /// flota, pulsa la escala y rota lentamente si se desea.
    public class QCXRMenuFloating : MonoBehaviour
    {
        [SerializeField] private Vector3 floatAxis = Vector3.up;
        [SerializeField] private float floatAmplitude = 0.04f;
        [SerializeField] private float floatSpeed = 1.2f;

        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float rotationSpeed = 0f;

        [SerializeField] private float scalePulse = 0.04f;
        [SerializeField] private float scaleSpeed = 1.5f;

        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private float seed;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalScale = transform.localScale;
            seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float t = Time.time * floatSpeed + seed;
            transform.localPosition = baseLocalPosition + floatAxis.normalized * (Mathf.Sin(t) * floatAmplitude);

            float scaleFactor = 1f + Mathf.Sin(Time.time * scaleSpeed + seed) * scalePulse;
            transform.localScale = baseLocalScale * scaleFactor;

            if (rotationSpeed != 0f)
                transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}
