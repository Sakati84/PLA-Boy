using UnityEngine;

namespace Playdate.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ObstacleCollisionRelay : MonoBehaviour
    {
        public FlappyPlayerController playerController;

        private void OnCollisionEnter(Collision collision)
        {
            if (GetComponent<WindZoneVolume>() != null)
            {
                return;
            }

            if (!IsPlayerCollision(collision.collider))
            {
                return;
            }

            playerController?.NotifyCollision();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (GetComponent<WindZoneVolume>() != null)
            {
                return;
            }

            if (!IsPlayerCollision(other))
            {
                return;
            }

            playerController?.NotifyCollision();
        }

        private bool IsPlayerCollision(Collider other)
        {
            if (other == null || playerController == null)
            {
                return false;
            }

            FlappyPlayerController hitPlayer = other.GetComponentInParent<FlappyPlayerController>();
            if (hitPlayer != null)
            {
                return hitPlayer == playerController;
            }

            Rigidbody attachedBody = other.attachedRigidbody;
            if (attachedBody != null)
            {
                hitPlayer = attachedBody.GetComponent<FlappyPlayerController>();
                return hitPlayer == playerController;
            }

            return false;
        }
    }
}
