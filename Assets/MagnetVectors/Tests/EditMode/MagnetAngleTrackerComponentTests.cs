using NUnit.Framework;
using UnityEngine;

namespace Playdate.MagnetVectors.Tests
{
    public sealed class MagnetAngleTrackerComponentTests
    {
        [Test]
        public void ComponentCalibrateAndResetAngleReflectCoreState()
        {
            var gameObject = new GameObject("MagnetAngleTrackerTest");
            try
            {
                var tracker = gameObject.AddComponent<MagnetAngleTracker>();
                tracker.AngleSmoothing = 0f;
                bool? result = null;

                tracker.Calibrate(success => result = success);
                if (!tracker.IsUsingWindowsDebugInput)
                {
                    FeedCalibrationCircle(tracker);
                }

                Assert.That(result, Is.True);
                Assert.That(tracker.IsCalibrated, Is.True);

                if (tracker.IsUsingWindowsDebugInput)
                {
                    tracker.ProcessDebugWheelFrame(6f, 0.02f);
                }
                else
                {
                    tracker.ProcessSensorFrame(new Vector3(1f, 0f, 0f), 0.02f, true);
                    tracker.ProcessSensorFrame(new Vector3(0f, 0f, 1f), 0.02f, true);
                }

                Assert.That(tracker.Angle, Is.EqualTo(90f).Within(0.001f));

                tracker.ResetAngle();
                Assert.That(tracker.IsCalibrated, Is.True);
                Assert.That(tracker.Angle, Is.EqualTo(0f).Within(0.001f));
                Assert.That(tracker.AngleDelta, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ComponentUsesConfiguredSmoothing()
        {
            var gameObject = new GameObject("MagnetAngleTrackerSmoothingTest");
            try
            {
                var tracker = gameObject.AddComponent<MagnetAngleTracker>();
                tracker.AngleSmoothing = 10f;

                bool? result = null;
                tracker.Calibrate(success => result = success);
                if (!tracker.IsUsingWindowsDebugInput)
                {
                    FeedCalibrationCircle(tracker);
                }

                Assert.That(result, Is.True);

                if (tracker.IsUsingWindowsDebugInput)
                {
                    tracker.ProcessDebugWheelFrame(6f, 0.02f);
                }
                else
                {
                    tracker.ProcessSensorFrame(new Vector3(1f, 0f, 0f), 0.02f, true);
                    tracker.ProcessSensorFrame(new Vector3(0f, 0f, 1f), 0.02f, true);
                }

                Assert.That(tracker.AngleDelta, Is.GreaterThan(0f));
                Assert.That(tracker.AngleDelta, Is.LessThan(90f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ComponentDebugWheelInputMovesAngleWithoutWrapping()
        {
            var gameObject = new GameObject("MagnetAngleTrackerDebugWheelTest");
            try
            {
                var tracker = gameObject.AddComponent<MagnetAngleTracker>();
                tracker.AngleSmoothing = 0f;
                bool? result = null;

                tracker.Calibrate(success => result = success);
                if (!tracker.IsUsingWindowsDebugInput)
                {
                    FeedCalibrationCircle(tracker);
                }

                Assert.That(result, Is.True);

                tracker.ProcessDebugWheelFrame(2f, 0.02f);
                tracker.ProcessDebugWheelFrame(30f, 0.02f);
                tracker.ProcessDebugWheelFrame(-60f, 0.02f);

                Assert.That(tracker.Angle, Is.EqualTo(-420f).Within(0.001f));
                Assert.That(tracker.AngleDelta, Is.EqualTo(-900f).Within(0.001f));

                tracker.ResetAngle();
                Assert.That(tracker.IsCalibrated, Is.True);
                Assert.That(tracker.Angle, Is.EqualTo(0f).Within(0.001f));
                Assert.That(tracker.AngleDelta, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ComponentDebugWheelMultiplierScalesWindowsDebugInput()
        {
            var gameObject = new GameObject("MagnetAngleTrackerDebugWheelMultiplierTest");
            try
            {
                var tracker = gameObject.AddComponent<MagnetAngleTracker>();
                tracker.AngleSmoothing = 0f;
                tracker.WindowsDebugWheelMultiplier = 2f;

                bool? result = null;
                tracker.Calibrate(success => result = success);
                if (!tracker.IsUsingWindowsDebugInput)
                {
                    FeedCalibrationCircle(tracker);
                }

                Assert.That(result, Is.True);

                tracker.ProcessDebugWheelFrame(2f, 0.02f);

                Assert.That(tracker.Angle, Is.EqualTo(60f).Within(0.001f));
                Assert.That(tracker.AngleDelta, Is.EqualTo(60f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void FeedCalibrationCircle(MagnetAngleTracker tracker)
        {
            Vector3[] samples =
            {
                new(2f, 0f, 0f),
                new(1.4142f, 0f, 1.4142f),
                new(0f, 0f, 2f),
                new(-1.4142f, 0f, 1.4142f),
                new(-2f, 0f, 0f),
                new(-1.4142f, 0f, -1.4142f),
                new(0f, 0f, -2f),
                new(1.4142f, 0f, -1.4142f)
            };

            for (int i = 0; i < samples.Length; i++)
            {
                tracker.ProcessSensorFrame(samples[i], MagnetAngleTrackerCore.CalibrationDuration / samples.Length, true);
            }
        }
    }
}
