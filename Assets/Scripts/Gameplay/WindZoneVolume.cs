using System.Collections.Generic;
using UnityEngine;

namespace Playdate.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WindZoneVolume : MonoBehaviour
    {
        public FlappyPlayerController playerController;
        public float VerticalAcceleration => verticalAcceleration;

        private readonly HashSet<FlappyPlayerController> overlappingPlayers = new();
        private float verticalAcceleration;

        public void Configure(float newVerticalAcceleration, FlappyPlayerController targetPlayer)
        {
            verticalAcceleration = newVerticalAcceleration;
            playerController = targetPlayer;

            if (TryGetComponent(out Collider collider))
            {
                collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            FlappyPlayerController target = ResolvePlayer(other);
            if (target == null || overlappingPlayers.Contains(target))
            {
                return;
            }

            overlappingPlayers.Add(target);
            target.RegisterWindZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            FlappyPlayerController target = ResolvePlayer(other);
            if (target == null || !overlappingPlayers.Remove(target))
            {
                return;
            }

            target.UnregisterWindZone(this);
        }

        private void OnDisable()
        {
            foreach (FlappyPlayerController target in overlappingPlayers)
            {
                target?.UnregisterWindZone(this);
            }

            overlappingPlayers.Clear();
        }

        private FlappyPlayerController ResolvePlayer(Collider other)
        {
            if (other == null)
            {
                return null;
            }

            FlappyPlayerController target = other.GetComponentInParent<FlappyPlayerController>();
            if (target != null)
            {
                return target;
            }

            Rigidbody attachedBody = other.attachedRigidbody;
            if (attachedBody != null)
            {
                target = attachedBody.GetComponent<FlappyPlayerController>();
                if (target != null)
                {
                    return target;
                }
            }

            return playerController;
        }
    }
}
