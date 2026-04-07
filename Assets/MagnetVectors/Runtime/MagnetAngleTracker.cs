using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Playdate.MagnetVectors
{
    public sealed class MagnetAngleTracker : MonoBehaviour
    {
        [SerializeField] [Range(0f, 25f)] private float angleSmoothing = 10f;
        [SerializeField] [Min(0f)] private float windowsDebugWheelMultiplier = 1f;

        private const float DebugDegreesPerWheelUnit = 15f;

        private readonly MagnetAngleTrackerCore core = new();

        public bool IsCalibrated => core.IsCalibrated;
        public float Angle => core.Angle;
        public float AngleDelta => core.AngleDelta;

        public float AngleSmoothing
        {
            get => angleSmoothing;
            set
            {
                angleSmoothing = value;
                core.AngleSmoothing = value;
            }
        }

        public float WindowsDebugWheelMultiplier
        {
            get => windowsDebugWheelMultiplier;
            set => windowsDebugWheelMultiplier = Mathf.Max(0f, value);
        }

        internal bool IsUsingWindowsDebugInput => UseWindowsDebugInput();

        private void Awake()
        {
            core.AngleSmoothing = angleSmoothing;
        }

        private void OnEnable()
        {
            if (MagneticFieldSensor.current != null)
            {
                InputSystem.EnableDevice(MagneticFieldSensor.current);
            }
        }

        private void Update()
        {
            if (UseWindowsDebugInput())
            {
                float wheelDelta = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
                ProcessDebugWheelFrame(wheelDelta, Time.deltaTime);
                return;
            }

            bool hasSensor = MagneticFieldSensor.current != null;
            Vector3 sample = hasSensor ? MagneticFieldSensor.current.magneticField.ReadValue() : Vector3.zero;
            ProcessSensorFrame(sample, Time.deltaTime, hasSensor);
        }

        public void Calibrate(Action<bool> onFinished)
        {
            if (UseWindowsDebugInput())
            {
                core.CompleteDebugCalibration(onFinished);
                return;
            }

            core.Calibrate(onFinished);
        }

        public void ResetAngle()
        {
            core.ResetAngle();
        }

        internal void ProcessSensorFrame(Vector3 sample, float deltaTime, bool sensorAvailable = true)
        {
            core.AngleSmoothing = angleSmoothing;
            core.Tick(sample, deltaTime, sensorAvailable);
        }

        internal void ProcessDebugWheelFrame(float wheelDelta, float deltaTime)
        {
            core.AngleSmoothing = angleSmoothing;
            core.TickDebugDelta(wheelDelta * DebugDegreesPerWheelUnit * Mathf.Max(0f, windowsDebugWheelMultiplier), deltaTime);
        }

        private void OnValidate()
        {
            angleSmoothing = Mathf.Clamp(angleSmoothing, 0f, 25f);
            windowsDebugWheelMultiplier = Mathf.Max(0f, windowsDebugWheelMultiplier);
        }

        private static bool UseWindowsDebugInput()
        {
            bool isWindows = Application.platform == RuntimePlatform.WindowsPlayer ||
                             Application.platform == RuntimePlatform.WindowsEditor;
            return isWindows && MagneticFieldSensor.current == null;
        }
    }
}
