using UnityEngine;

namespace Market.Player
{
    /// <summary>
    /// Покачивание камеры при ходьбе. При спринте — амплитуда выше.
    /// Когда игрок стоит — плавный возврат в исходную позицию.
    /// </summary>
    public class HeadBob : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private Transform cameraTransform;

        [Header("Walk Bob")]
        [SerializeField] private float walkFrequency   = 8f;
        [SerializeField] private float walkAmplitudeY  = 0.045f;
        [SerializeField] private float walkAmplitudeX  = 0.025f;
        [SerializeField] private float sprintMultiplier = 1.4f;
        [SerializeField] private float resetSpeed      = 6f;

        [Header("Tuning")]
        [Tooltip("Скорость, при которой амплитуда максимальна (walkSpeed FirstPersonController).")]
        [SerializeField] private float baseSpeed = 4f;
        [Tooltip("Скорость выше этого порога считается спринтом.")]
        [SerializeField] private float sprintThreshold = 5f;

        private Vector3 _restPosition;
        private float _phase;

        private void Awake()
        {
            if (cameraTransform == null) cameraTransform = transform;
            _restPosition = cameraTransform.localPosition;
        }

        private void LateUpdate()
        {
            if (controller != null && controller.IsMoving)
                ApplyBob();
            else
                ApplyReset();
        }

        private void ApplyBob()
        {
            float rawSpeed   = controller.CurrentSpeed;
            float phaseFactor = Mathf.Clamp01(rawSpeed / baseSpeed);
            float mult        = rawSpeed > sprintThreshold ? sprintMultiplier : 1f;

            _phase += Time.deltaTime * walkFrequency * phaseFactor;

            float y = Mathf.Sin(_phase * 2f) * walkAmplitudeY * mult;
            float x = Mathf.Cos(_phase)      * walkAmplitudeX * mult;
            cameraTransform.localPosition = _restPosition + new Vector3(x, y, 0f);
        }

        private void ApplyReset()
        {
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition, _restPosition, Time.deltaTime * resetSpeed);
            _phase = 0f;
        }
    }
}
