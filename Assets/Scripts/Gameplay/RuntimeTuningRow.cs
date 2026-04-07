using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Playdate.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RuntimeTuningRow : MonoBehaviour
    {
        public TextMeshProUGUI nameLabel;
        public TextMeshProUGUI valueLabel;
        public Button decreaseButton;
        public Button increaseButton;

        private RuntimeTuningBinding binding;

        public void Bind(RuntimeTuningBinding nextBinding)
        {
            binding = nextBinding;

            if (nameLabel != null)
            {
                nameLabel.text = binding != null ? binding.DisplayName : string.Empty;
            }

            if (decreaseButton != null)
            {
                decreaseButton.onClick.RemoveAllListeners();
                decreaseButton.onClick.AddListener(HandleDecrease);
            }

            if (increaseButton != null)
            {
                increaseButton.onClick.RemoveAllListeners();
                increaseButton.onClick.AddListener(HandleIncrease);
            }

            Refresh();
        }

        public void Refresh()
        {
            if (valueLabel == null)
            {
                return;
            }

            valueLabel.text = binding != null
                ? binding.GetValue().ToString("0.###")
                : string.Empty;
        }

        private void HandleDecrease()
        {
            if (binding == null)
            {
                return;
            }

            binding.Decrease();
            Refresh();
        }

        private void HandleIncrease()
        {
            if (binding == null)
            {
                return;
            }

            binding.Increase();
            Refresh();
        }
    }
}
