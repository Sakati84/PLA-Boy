using UnityEngine;

namespace Playdate.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ObstacleMotionDriver : MonoBehaviour
    {
        private Vector3 baseLocalPosition;
        private Vector3 motionAxis = Vector3.up;
        private float motionAmplitude;
        private float motionFrequency = 1f;
        private float motionPhaseOffset;
        private float elapsedTime;
        private bool motionEnabled;

        public void Configure(Vector3 anchorLocalPosition, Vector3 axis, float amplitude, float frequency, float phaseOffset)
        {
            baseLocalPosition = anchorLocalPosition;
            motionAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            motionAmplitude = Mathf.Max(0f, amplitude);
            motionFrequency = Mathf.Max(0f, frequency);
            motionPhaseOffset = phaseOffset;
            elapsedTime = 0f;
            motionEnabled = motionAmplitude > 0f && motionFrequency > 0f;
            transform.localPosition = baseLocalPosition;
        }

        public void DisableMotion(Vector3 anchorLocalPosition)
        {
            motionEnabled = false;
            elapsedTime = 0f;
            baseLocalPosition = anchorLocalPosition;
            transform.localPosition = baseLocalPosition;
        }

        private void Update()
        {
            if (!motionEnabled)
            {
                return;
            }

            elapsedTime += Time.deltaTime;
            float wave = Mathf.Sin((elapsedTime + motionPhaseOffset) * motionFrequency * Mathf.PI * 2f);
            transform.localPosition = baseLocalPosition + motionAxis * (wave * motionAmplitude);
        }

        private void OnDisable()
        {
            transform.localPosition = baseLocalPosition;
        }
    }
}
