using System;
using System.Collections.Generic;
using UnityEngine;

namespace Playdate.MagnetVectors
{
    internal sealed class MagnetAngleTrackerCore
    {
        internal const float CalibrationDuration = 5f;
        internal static readonly CalibrationData DebugCalibrationData = new(Vector2.zero, Vector2.one);

        private const float MinFieldMagnitude = 0.0001f;
        private const float MinDirectAngleRadius = 0.0001f;
        private const float MinAngleSmoothing = 0f;
        private const float MaxAngleSmoothing = 25f;

        private readonly List<Vector3> calibrationSamples = new();

        private CalibrationRun calibrationRun;
        private CalibrationData calibrationData;
        private AngleState angleState;

        public bool IsCalibrated => calibrationData.IsValid;
        public float Angle => angleState.Angle;
        public float AngleDelta => angleState.LastDelta;

        public float AngleSmoothing
        {
            get => angleState.Smoothing;
            set => angleState.Smoothing = Mathf.Clamp(value, MinAngleSmoothing, MaxAngleSmoothing);
        }

        public void Calibrate(Action<bool> onFinished)
        {
            calibrationSamples.Clear();
            calibrationData = CalibrationData.Invalid;
            angleState.Reset();
            calibrationRun = new CalibrationRun(true, CalibrationDuration, onFinished);
        }

        public void CompleteDebugCalibration(Action<bool> onFinished)
        {
            calibrationSamples.Clear();
            calibrationRun = CalibrationRun.None;
            calibrationData = DebugCalibrationData;
            angleState.Reset();
            onFinished?.Invoke(true);
        }

        public void ResetAngle()
        {
            angleState.Reset();
        }

        public void Tick(Vector3 sample, float deltaTime, bool sensorAvailable)
        {
            Vector3 validSample = Vector3.zero;
            bool hasValidSample = sensorAvailable && TryGetMagneticVector(sample, out validSample);
            bool calibrationWasRunning = calibrationRun.IsRunning;

            if (calibrationWasRunning)
            {
                TickCalibration(validSample, hasValidSample, deltaTime);
            }

            if (calibrationWasRunning)
            {
                angleState.LastDelta = 0f;
                return;
            }

            TickAngle(validSample, hasValidSample, deltaTime);
        }

        public void TickDebugDelta(float angleDelta, float deltaTime)
        {
            if (!calibrationData.IsValid)
            {
                angleState.LastDelta = 0f;
                return;
            }

            angleState.AccumulateDelta(angleDelta, deltaTime);
        }

        private void TickCalibration(Vector3 validSample, bool hasValidSample, float deltaTime)
        {
            calibrationRun = calibrationRun.WithTimeRemaining(
                Mathf.Max(0f, calibrationRun.TimeRemaining - Mathf.Max(0f, deltaTime)));
            if (hasValidSample)
            {
                calibrationSamples.Add(validSample);
            }

            if (calibrationRun.TimeRemaining > 0f)
            {
                return;
            }

            bool success = TryCreateCalibrationData(calibrationSamples, out calibrationData);
            if (!success)
            {
                calibrationData = CalibrationData.Invalid;
            }

            calibrationSamples.Clear();
            angleState.Reset();

            Action<bool> callback = calibrationRun.OnFinished;
            calibrationRun = CalibrationRun.None;
            callback?.Invoke(success);
        }

        private void TickAngle(Vector3 validSample, bool hasValidSample, float deltaTime)
        {
            if (!calibrationData.IsValid || !hasValidSample)
            {
                angleState.LastDelta = 0f;
                return;
            }

            if (!TryGetDirectHeading(validSample, calibrationData, out float heading))
            {
                angleState.LastDelta = 0f;
                return;
            }

            angleState.AccumulateHeading(heading, deltaTime);
        }

        private static bool TryCreateCalibrationData(List<Vector3> samples, out CalibrationData data)
        {
            if (samples.Count == 0)
            {
                data = CalibrationData.Invalid;
                return false;
            }

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            foreach (Vector3 sample in samples)
            {
                minX = Mathf.Min(minX, sample.x);
                maxX = Mathf.Max(maxX, sample.x);
                minZ = Mathf.Min(minZ, sample.z);
                maxZ = Mathf.Max(maxZ, sample.z);
            }

            Vector2 center = new((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            Vector2 extents = new((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
            if (extents.x <= MinDirectAngleRadius || extents.y <= MinDirectAngleRadius)
            {
                data = CalibrationData.Invalid;
                return false;
            }

            data = new CalibrationData(center, extents);
            return true;
        }

        internal static bool TryGetDirectHeading(Vector3 sample, CalibrationData data, out float heading)
        {
            heading = 0f;
            if (!data.IsValid || !TryGetMagneticVector(sample, out Vector3 validSample))
            {
                return false;
            }

            Vector2 centered = new(validSample.x - data.Center.x, validSample.z - data.Center.y);
            if (centered.sqrMagnitude <= MinDirectAngleRadius * MinDirectAngleRadius)
            {
                return false;
            }

            heading = Mathf.Atan2(centered.y, centered.x) * Mathf.Rad2Deg;
            return true;
        }

        private static bool TryGetMagneticVector(Vector3 sample, out Vector3 validSample)
        {
            if (sample.sqrMagnitude <= MinFieldMagnitude * MinFieldMagnitude)
            {
                validSample = Vector3.zero;
                return false;
            }

            validSample = sample;
            return true;
        }

        internal readonly struct CalibrationData
        {
            public static CalibrationData Invalid => default;

            public CalibrationData(Vector2 center, Vector2 extents)
            {
                Center = center;
                Extents = extents;
                IsValid = true;
            }

            public Vector2 Center { get; }
            public Vector2 Extents { get; }
            public bool IsValid { get; }
        }

        private readonly struct CalibrationRun
        {
            public static CalibrationRun None => default;

            public CalibrationRun(bool isRunning, float timeRemaining, Action<bool> onFinished)
            {
                IsRunning = isRunning;
                TimeRemaining = timeRemaining;
                OnFinished = onFinished;
            }

            public bool IsRunning { get; }
            public float TimeRemaining { get; }
            public Action<bool> OnFinished { get; }

            public CalibrationRun WithTimeRemaining(float timeRemaining)
            {
                return new CalibrationRun(IsRunning, timeRemaining, OnFinished);
            }
        }

        private struct AngleState
        {
            public float Smoothing;
            public float Angle;
            public float LastDelta;
            public float SmoothedDelta;
            public float PreviousHeading;
            public bool HasSmoothedDelta;
            public bool HasPreviousHeading;

            public void Reset()
            {
                Angle = 0f;
                LastDelta = 0f;
                SmoothedDelta = 0f;
                PreviousHeading = 0f;
                HasSmoothedDelta = true;
                HasPreviousHeading = false;
            }

            public void AccumulateHeading(float heading, float deltaTime)
            {
                if (!HasPreviousHeading)
                {
                    PreviousHeading = heading;
                    LastDelta = 0f;
                    HasPreviousHeading = true;
                    return;
                }

                float rawDelta = Mathf.DeltaAngle(PreviousHeading, heading);
                PreviousHeading = heading;
                AccumulateDelta(rawDelta, deltaTime);
            }

            public void AccumulateDelta(float rawDelta, float deltaTime)
            {
                float appliedDelta = ApplyDeltaSmoothing(rawDelta, deltaTime);
                LastDelta = appliedDelta;
                Angle += appliedDelta;
            }

            private float ApplyDeltaSmoothing(float rawDelta, float deltaTime)
            {
                if (Smoothing <= 0f)
                {
                    SmoothedDelta = rawDelta;
                    HasSmoothedDelta = true;
                    return rawDelta;
                }

                float blend = 1f - Mathf.Exp(-Smoothing * Mathf.Max(0f, deltaTime));
                SmoothedDelta = Mathf.Lerp(SmoothedDelta, rawDelta, blend);
                return SmoothedDelta;
            }
        }
    }
}
