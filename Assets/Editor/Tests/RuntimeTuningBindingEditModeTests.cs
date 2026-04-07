using NUnit.Framework;
using Playdate.Gameplay;

namespace Playdate.Tests.Editor
{
    public sealed class RuntimeTuningBindingEditModeTests
    {
        [Test]
        public void Increase_UsesFivePercentOfDefaultValuePerClick()
        {
            float value = 8f;
            RuntimeTuningBinding binding = new(
                "Base Gravity",
                () => value,
                next => value = next,
                8f);

            binding.Increase();

            Assert.That(value, Is.EqualTo(8.4f).Within(0.0001f));
        }

        [Test]
        public void Increase_UsesDefaultValueAsStepBaseline_NotCurrentValue()
        {
            float value = 20f;
            RuntimeTuningBinding binding = new(
                "Lift",
                () => value,
                next => value = next,
                10f);

            binding.Increase();

            Assert.That(value, Is.EqualTo(20.5f).Within(0.0001f));
        }

        [Test]
        public void Reset_RestoresCapturedDefaultValue()
        {
            float value = 3f;
            RuntimeTuningBinding binding = new(
                "Smoothing",
                () => value,
                next => value = next,
                1.5f);

            binding.Increase();
            binding.Reset();

            Assert.That(value, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void Decrease_ClampsToMinimumValue()
        {
            float value = 0.02f;
            RuntimeTuningBinding binding = new(
                "Deadzone",
                () => value,
                next => value = next,
                0.1f,
                0f);

            binding.Decrease();

            Assert.That(value, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
