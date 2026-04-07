using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Playdate.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class DistanceHud : MonoBehaviour
    {
        [Header("Overlay Roots")]
        public GameObject startOverlay;
        public GameObject gameOverOverlay;
        public GameObject inGameRoot;

        [Header("Buttons")]
        public Button startButton;
        public Button retryButton;

        [Header("Labels")]
        public TextMeshProUGUI currentDistanceLabel;
        public TextMeshProUGUI bestDistanceLabel;
        public TextMeshProUGUI startMessageLabel;
        public TextMeshProUGUI gameOverSummaryLabel;

        private GameSessionController sessionController;

        public void Bind(GameSessionController controller)
        {
            sessionController = controller;

            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(controller.BeginCalibrationAndStart);
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(controller.RetryRun);
            }
        }

        public void Render(GameSessionState state, float currentDistanceMeters, float bestDistanceMeters, bool lastCalibrationFailed)
        {
            if (currentDistanceLabel != null)
            {
                currentDistanceLabel.text = $"{Mathf.FloorToInt(currentDistanceMeters)} m";
            }

            if (bestDistanceLabel != null)
            {
                bestDistanceLabel.text = $"Best: {Mathf.FloorToInt(bestDistanceMeters)} m";
            }

            if (inGameRoot != null)
            {
                inGameRoot.SetActive(state == GameSessionState.Playing || state == GameSessionState.GameOver);
            }

            if (startOverlay != null)
            {
                startOverlay.SetActive(state == GameSessionState.AwaitingCalibration || state == GameSessionState.Boot);
            }

            if (gameOverOverlay != null)
            {
                gameOverOverlay.SetActive(state == GameSessionState.GameOver);
            }

            if (startMessageLabel != null)
            {
                switch (state)
                {
                    case GameSessionState.Boot:
                        startMessageLabel.text = "Kalibrierung laeuft...";
                        break;
                    case GameSessionState.AwaitingCalibration:
                        startMessageLabel.text = lastCalibrationFailed
                            ? "Kalibrierung fehlgeschlagen. Bitte noch einmal versuchen."
                            : "Propeller kalibrieren und dann losfliegen.";
                        break;
                    default:
                        startMessageLabel.text = string.Empty;
                        break;
                }
            }

            if (gameOverSummaryLabel != null)
            {
                gameOverSummaryLabel.text =
                    $"Strecke: {Mathf.FloorToInt(currentDistanceMeters)} m\nBestwert: {Mathf.FloorToInt(bestDistanceMeters)} m";
            }
        }
    }
}
