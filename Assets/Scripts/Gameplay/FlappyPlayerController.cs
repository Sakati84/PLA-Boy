using System.Collections.Generic;
using Playdate.MagnetVectors;
using UnityEngine;

namespace Playdate.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MagnetAngleTracker))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlappyPlayerController : MonoBehaviour
    {
        [Header("References")]
        public MagnetAngleTracker magnetAngleTracker;
        public Rigidbody body;
        public Transform propellerVisual;
        public GameSessionController sessionController;

        [Header("Flight")]
        public float forwardSpeed = 5f;
        public float baseGravity = 8f;
        public float verticalDamping = 2f;
        public float liftAcceleration = 0.35f;
        public float diveAcceleration = 0.45f;
        public float angleDeltaDeadzone = 0.05f;
        public float maxRiseSpeed = 5f;
        public float maxFallSpeed = 9f;
        public float rotationVisualMultiplier = 1f;
        public float minY = -4.5f;
        public float maxY = 4.5f;

        public float CurrentVerticalVelocity => currentVerticalVelocity;
        public float CurrentEnvironmentalAcceleration => GetEnvironmentalAcceleration();

        private float currentVerticalVelocity;
        private bool simulationEnabled;
        private Vector3 spawnPosition;
        private readonly HashSet<WindZoneVolume> activeWindZones = new();

        private void Awake()
        {
            if (magnetAngleTracker == null)
            {
                magnetAngleTracker = GetComponent<MagnetAngleTracker>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            spawnPosition = transform.position;
            ConfigureBody();
        }

        private void OnValidate()
        {
            if (magnetAngleTracker == null)
            {
                magnetAngleTracker = GetComponent<MagnetAngleTracker>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
        }

        private void LateUpdate()
        {
            if (propellerVisual == null || magnetAngleTracker == null)
            {
                return;
            }

            propellerVisual.localRotation = Quaternion.Euler(0f, -magnetAngleTracker.Angle * rotationVisualMultiplier, 0f);
        }

        private void FixedUpdate()
        {
            if (!simulationEnabled || body == null)
            {
                return;
            }

            float angleDelta = magnetAngleTracker != null ? magnetAngleTracker.AngleDelta : 0f;
            float environmentalAcceleration = GetEnvironmentalAcceleration();
            currentVerticalVelocity = ComputeNextVerticalVelocity(
                currentVerticalVelocity,
                angleDelta,
                Time.fixedDeltaTime,
                baseGravity,
                verticalDamping,
                liftAcceleration,
                diveAcceleration,
                angleDeltaDeadzone,
                maxRiseSpeed,
                maxFallSpeed,
                environmentalAcceleration);

            Vector3 nextPosition = body.position + Vector3.up * (currentVerticalVelocity * Time.fixedDeltaTime);
            if (nextPosition.y < minY || nextPosition.y > maxY)
            {
                nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);
                body.MovePosition(nextPosition);
                NotifyCollision();
                return;
            }

            body.MovePosition(nextPosition);
        }

        public void SetSimulationEnabled(bool enabled)
        {
            simulationEnabled = enabled;
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            if (!enabled)
            {
                currentVerticalVelocity = 0f;
                ClearWindZones();
            }
        }

        public void ResetForRound()
        {
            simulationEnabled = false;
            currentVerticalVelocity = 0f;
            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;

            if (body != null)
            {
                body.position = spawnPosition;
                body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (magnetAngleTracker != null)
            {
                magnetAngleTracker.ResetAngle();
            }

            ClearWindZones();
        }

        public void SetSpawnPosition(Vector3 position)
        {
            spawnPosition = position;
            ResetForRound();
        }

        public void NotifyCollision()
        {
            if (!simulationEnabled)
            {
                return;
            }

            simulationEnabled = false;
            sessionController?.HandlePlayerCollision();
        }

        public void RegisterWindZone(WindZoneVolume windZone)
        {
            if (windZone != null)
            {
                activeWindZones.Add(windZone);
            }
        }

        public void UnregisterWindZone(WindZoneVolume windZone)
        {
            if (windZone != null)
            {
                activeWindZones.Remove(windZone);
            }
        }

        public static float ComputeNextVerticalVelocity(
            float currentVelocity,
            float angleDelta,
            float deltaTime,
            float baseGravity,
            float verticalDamping,
            float liftAcceleration,
            float diveAcceleration,
            float angleDeltaDeadzone,
            float maxRiseSpeed,
            float maxFallSpeed,
            float environmentalAcceleration = 0f)
        {
            float clampedDeltaTime = Mathf.Max(0f, deltaTime);
            float nextVelocity = currentVelocity;

            nextVelocity = Mathf.MoveTowards(nextVelocity, 0f, Mathf.Max(0f, verticalDamping) * clampedDeltaTime);
            nextVelocity -= Mathf.Max(0f, baseGravity) * clampedDeltaTime;

            float effectiveDelta = Mathf.Abs(angleDelta) > Mathf.Max(0f, angleDeltaDeadzone)
                ? Mathf.Abs(angleDelta) - Mathf.Max(0f, angleDeltaDeadzone)
                : 0f;

            if (effectiveDelta > 0f)
            {
                if (angleDelta > 0f)
                {
                    nextVelocity += effectiveDelta * Mathf.Max(0f, liftAcceleration) * clampedDeltaTime;
                }
                else
                {
                    nextVelocity -= effectiveDelta * Mathf.Max(0f, diveAcceleration) * clampedDeltaTime;
                }
            }

            nextVelocity += environmentalAcceleration * clampedDeltaTime;

            return Mathf.Clamp(nextVelocity, -Mathf.Max(0f, maxFallSpeed), Mathf.Max(0f, maxRiseSpeed));
        }

        private void ConfigureBody()
        {
            if (body == null)
            {
                return;
            }

            body.useGravity = false;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezePositionX |
                               RigidbodyConstraints.FreezePositionZ |
                               RigidbodyConstraints.FreezeRotation;
        }

        private float GetEnvironmentalAcceleration()
        {
            float acceleration = 0f;
            activeWindZones.RemoveWhere(zone => zone == null || !zone.isActiveAndEnabled);
            foreach (WindZoneVolume windZone in activeWindZones)
            {
                acceleration += windZone.VerticalAcceleration;
            }

            return acceleration;
        }

        private void ClearWindZones()
        {
            activeWindZones.Clear();
        }
    }
}
