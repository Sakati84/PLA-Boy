using NUnit.Framework;
using UnityEngine;

namespace Playdate.MagnetVectors.Tests
{
    public sealed class MagnetAngleTrackerCoreTests
    {
        [Test]
        public void CalibrationSucceedsWithValidSamples()
        {
            var core = new MagnetAngleTrackerCore();
            bool? result = null;

            core.Calibrate(success => result = success);
            FeedCalibrationCircle(core);

            Assert.That(result, Is.True);
            Assert.That(core.IsCalibrated, Is.True);
            Assert.That(core.Angle, Is.EqualTo(0f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void CalibrationFailsWithoutSamples()
        {
            var core = new MagnetAngleTrackerCore();
            bool? result = null;

            core.Calibrate(success => result = success);
            core.Tick(Vector3.zero, MagnetAngleTrackerCore.CalibrationDuration, false);

            Assert.That(result, Is.False);
            Assert.That(core.IsCalibrated, Is.False);
        }

        [Test]
        public void CalibrationFailsWithTooSmallExtent()
        {
            var core = new MagnetAngleTrackerCore();
            bool? result = null;

            core.Calibrate(success => result = success);
            for (int i = 0; i < 5; i++)
            {
                core.Tick(new Vector3(1f, 0f, 1f), 1f, true);
            }

            Assert.That(result, Is.False);
            Assert.That(core.IsCalibrated, Is.False);
        }

        [TestCase(2f, 0f, 0f)]
        [TestCase(0f, 2f, 90f)]
        [TestCase(-2f, 0f, 180f)]
        [TestCase(0f, -2f, -90f)]
        [TestCase(2f, 2f, 45f)]
        [TestCase(-2f, -2f, -135f)]
        public void DirectHeadingUsesExpectedOrientation(float x, float z, float expectedAngle)
        {
            var data = new MagnetAngleTrackerCore.CalibrationData(Vector2.zero, Vector2.one);
            bool success = MagnetAngleTrackerCore.TryGetDirectHeading(new Vector3(x, 0f, z), data, out float angle);

            Assert.That(success, Is.True);
            Assert.That(angle, Is.EqualTo(expectedAngle).Within(0.001f));
        }

        [Test]
        public void AngleUnwrapsAcrossZeroForward()
        {
            var core = CreateCalibratedCore();

            core.Tick(new Vector3(Mathf.Cos(350f * Mathf.Deg2Rad), 0f, Mathf.Sin(350f * Mathf.Deg2Rad)), 0.02f, true);
            core.Tick(new Vector3(Mathf.Cos(10f * Mathf.Deg2Rad), 0f, Mathf.Sin(10f * Mathf.Deg2Rad)), 0.02f, true);

            Assert.That(core.Angle, Is.EqualTo(20f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void AngleUnwrapsAcrossZeroBackward()
        {
            var core = CreateCalibratedCore();

            core.Tick(new Vector3(Mathf.Cos(10f * Mathf.Deg2Rad), 0f, Mathf.Sin(10f * Mathf.Deg2Rad)), 0.02f, true);
            core.Tick(new Vector3(Mathf.Cos(350f * Mathf.Deg2Rad), 0f, Mathf.Sin(350f * Mathf.Deg2Rad)), 0.02f, true);

            Assert.That(core.Angle, Is.EqualTo(-20f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(-20f).Within(0.001f));
        }

        [Test]
        public void AngleCanExceedOneFullTurn()
        {
            var core = CreateCalibratedCore();

            for (int angle = 0; angle <= 720; angle += 90)
            {
                core.Tick(new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)), 0.02f, true);
            }

            Assert.That(core.Angle, Is.GreaterThan(360f));
        }

        [Test]
        public void AngleCanGoBelowZero()
        {
            var core = CreateCalibratedCore();

            for (int angle = 0; angle >= -360; angle -= 90)
            {
                core.Tick(new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)), 0.02f, true);
            }

            Assert.That(core.Angle, Is.LessThan(0f));
        }

        [Test]
        public void ResetAngleKeepsCalibrationButZerosAngleState()
        {
            var core = CreateCalibratedCore();

            core.Tick(new Vector3(0f, 0f, 1f), 0.02f, true);
            core.Tick(new Vector3(-1f, 0f, 0f), 0.02f, true);
            core.ResetAngle();

            Assert.That(core.IsCalibrated, Is.True);
            Assert.That(core.Angle, Is.EqualTo(0f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(0f).Within(0.001f));

            core.Tick(new Vector3(0f, 0f, -1f), 0.02f, true);
            Assert.That(core.Angle, Is.EqualTo(0f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void InvalidFramesDoNotMoveAngle()
        {
            var core = CreateCalibratedCore();

            core.Tick(new Vector3(1f, 0f, 0f), 0.02f, true);
            core.Tick(Vector3.zero, 0.02f, true);

            Assert.That(core.Angle, Is.EqualTo(0f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SmoothingZeroUsesImmediateAngle()
        {
            var core = CreateCalibratedCore();
            core.AngleSmoothing = 0f;

            core.Tick(new Vector3(1f, 0f, 0f), 0.02f, true);
            core.Tick(new Vector3(0f, 0f, 1f), 0.02f, true);

            Assert.That(core.Angle, Is.EqualTo(90f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void PositiveSmoothingReducesPerFrameDelta()
        {
            var core = CreateCalibratedCore();
            core.AngleSmoothing = 10f;

            core.Tick(new Vector3(1f, 0f, 0f), 0.02f, true);
            core.Tick(new Vector3(0f, 0f, 1f), 0.02f, true);

            Assert.That(core.AngleDelta, Is.GreaterThan(0f));
            Assert.That(core.AngleDelta, Is.LessThan(90f));
        }

        [Test]
        public void DebugCalibrationCompletesImmediately()
        {
            var core = new MagnetAngleTrackerCore();
            bool? result = null;

            core.CompleteDebugCalibration(success => result = success);

            Assert.That(result, Is.True);
            Assert.That(core.IsCalibrated, Is.True);
            Assert.That(core.Angle, Is.EqualTo(0f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void DebugDeltaMovesAngleContinuouslyWithoutWrapping()
        {
            var core = new MagnetAngleTrackerCore();
            core.AngleSmoothing = 0f;
            core.CompleteDebugCalibration(null);

            core.TickDebugDelta(120f, 0.02f);
            core.TickDebugDelta(270f, 0.02f);
            core.TickDebugDelta(-810f, 0.02f);

            Assert.That(core.Angle, Is.EqualTo(-420f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(-810f).Within(0.001f));
        }

        [Test]
        public void DebugResetKeepsCalibration()
        {
            var core = new MagnetAngleTrackerCore();
            core.AngleSmoothing = 0f;
            core.CompleteDebugCalibration(null);
            core.TickDebugDelta(45f, 0.02f);

            core.ResetAngle();

            Assert.That(core.IsCalibrated, Is.True);
            Assert.That(core.Angle, Is.EqualTo(0f).Within(0.001f));
            Assert.That(core.AngleDelta, Is.EqualTo(0f).Within(0.001f));
        }

        private static MagnetAngleTrackerCore CreateCalibratedCore()
        {
            var core = new MagnetAngleTrackerCore();
            core.AngleSmoothing = 0f;
            bool? result = null;
            core.Calibrate(success => result = success);
            FeedCalibrationCircle(core);
            Assert.That(result, Is.True);
            return core;
        }

        private static void FeedCalibrationCircle(MagnetAngleTrackerCore core)
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
                core.Tick(samples[i], MagnetAngleTrackerCore.CalibrationDuration / samples.Length, true);
            }
        }
    }
}
