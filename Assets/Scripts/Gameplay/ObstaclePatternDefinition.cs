using System;
using System.Collections.Generic;
using UnityEngine;

namespace Playdate.Gameplay
{
    [CreateAssetMenu(fileName = "ObstaclePattern", menuName = "Playdate/Obstacle Pattern")]
    public sealed class ObstaclePatternDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class PatternElement
        {
            public GameObject prefab;
            public Vector3 localOffset;
            public Vector3 localRotation;
            public Vector2 randomZRotationRange;
            public Vector3 localScale = Vector3.one;
            public float gapOffsetFactor;
            public bool fitGapHeight;
            public float gapHeightPadding = 0.25f;
            public bool causesCollision = true;
            public bool enableMotion;
            public Vector3 motionAxis = Vector3.up;
            public float motionAmplitude = 1f;
            public float motionFrequency = 0.5f;
            public float motionPhaseOffset;
            public bool enableWind;
            public float windAcceleration;
            public bool useColorOverride;
            public Color colorOverride = Color.white;
        }

        [Min(0.01f)] public float weight = 1f;
        [Range(0f, 1f)] public float minDifficulty;
        [Range(0f, 1f)] public float maxDifficulty = 1f;
        public Vector2 spawnYRange = new(-2.5f, 2.5f);
        public Vector2 gapRange = new(3.5f, 5f);
        [Min(0.25f)] public float spawnIntervalMultiplier = 1f;
        public List<PatternElement> elements = new();

        public bool SupportsDifficulty(float difficulty)
        {
            float clampedDifficulty = Mathf.Clamp01(difficulty);
            return clampedDifficulty >= minDifficulty && clampedDifficulty <= maxDifficulty;
        }

        private void OnValidate()
        {
            weight = Mathf.Max(0.01f, weight);

            if (maxDifficulty < minDifficulty)
            {
                maxDifficulty = minDifficulty;
            }

            if (spawnYRange.y < spawnYRange.x)
            {
                spawnYRange = new Vector2(spawnYRange.y, spawnYRange.x);
            }

            if (gapRange.y < gapRange.x)
            {
                gapRange = new Vector2(gapRange.y, gapRange.x);
            }

            spawnIntervalMultiplier = Mathf.Max(0.25f, spawnIntervalMultiplier);

            for (int index = 0; index < elements.Count; index++)
            {
                PatternElement element = elements[index];
                if (element == null)
                {
                    continue;
                }

                if (element.localScale == Vector3.zero)
                {
                    element.localScale = Vector3.one;
                }

                if (element.randomZRotationRange.y < element.randomZRotationRange.x)
                {
                    element.randomZRotationRange = new Vector2(
                        element.randomZRotationRange.y,
                        element.randomZRotationRange.x);
                }

                element.gapHeightPadding = Mathf.Max(0f, element.gapHeightPadding);
                element.motionAmplitude = Mathf.Max(0f, element.motionAmplitude);
                element.motionFrequency = Mathf.Max(0f, element.motionFrequency);
            }
        }
    }
}
