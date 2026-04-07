using System;
using UnityEngine;

namespace Playdate.Gameplay
{
    public sealed class RuntimeTuningBinding
    {
        private const float DefaultStepFactor = 0.05f;
        private const float MinimumStepSize = 0.01f;

        private readonly Func<float> getter;
        private readonly Action<float> setter;
        private readonly float? maximumValue;

        public RuntimeTuningBinding(
            string displayName,
            Func<float> getter,
            Action<float> setter,
            float defaultValue,
            float minimumValue = 0f,
            float? maximumValue = null)
        {
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            this.getter = getter ?? throw new ArgumentNullException(nameof(getter));
            this.setter = setter ?? throw new ArgumentNullException(nameof(setter));
            DefaultValue = defaultValue;
            MinimumValue = minimumValue;
            this.maximumValue = maximumValue;
        }

        public string DisplayName { get; }
        public float DefaultValue { get; }
        public float MinimumValue { get; }
        public float StepSize => Mathf.Max(MinimumStepSize, Mathf.Abs(DefaultValue) * DefaultStepFactor);

        public float GetValue()
        {
            return getter();
        }

        public void Increase()
        {
            SetValue(GetValue() + StepSize);
        }

        public void Decrease()
        {
            SetValue(GetValue() - StepSize);
        }

        public void Reset()
        {
            SetValue(DefaultValue);
        }

        private void SetValue(float value)
        {
            float clampedValue = Mathf.Max(MinimumValue, value);
            if (maximumValue.HasValue)
            {
                clampedValue = Mathf.Min(maximumValue.Value, clampedValue);
            }

            setter(clampedValue);
        }
    }
}
