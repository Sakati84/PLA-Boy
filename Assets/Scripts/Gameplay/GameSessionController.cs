using Playdate.MagnetVectors;
using UnityEngine;

namespace Playdate.Gameplay
{
    public enum GameSessionState
    {
        Boot,
        AwaitingCalibration,
        Ready,
        Playing,
        GameOver
    }

    [DisallowMultipleComponent]
    public sealed class GameSessionController : MonoBehaviour
    {
        private const string BestDistanceKey = "Playdate.FlappyBestDistance";

        [Header("References")]
        public MagnetAngleTracker magnetAngleTracker;
        public FlappyPlayerController playerController;
        public ObstacleSpawner obstacleSpawner;
        public DistanceHud distanceHud;

        [Header("Progression")]
        public float metersPerWorldUnit = 1f;
        public float difficultyRampDistanceMeters = 250f;

        public GameSessionState CurrentState { get; private set; } = GameSessionState.Boot;
        public float CurrentDistanceMeters { get; private set; }
        public float BestDistanceMeters { get; private set; }
        public bool LastCalibrationFailed { get; private set; }

        private void Awake()
        {
            BestDistanceMeters = PlayerPrefs.GetFloat(BestDistanceKey, 0f);

            if (distanceHud != null)
            {
                distanceHud.Bind(this);
            }
        }

        private void Start()
        {
            if (playerController != null)
            {
                playerController.sessionController = this;
                playerController.ResetForRound();
                playerController.SetSimulationEnabled(false);
            }

            if (obstacleSpawner != null)
            {
                obstacleSpawner.playerController = playerController;
                obstacleSpawner.ResetRun();
            }

            SetState(GameSessionState.AwaitingCalibration);
        }

        private void Update()
        {
            if (CurrentState == GameSessionState.Playing)
            {
                float scrollSpeed = playerController != null ? playerController.forwardSpeed : 0f;
                CurrentDistanceMeters = AdvanceDistance(CurrentDistanceMeters, scrollSpeed, metersPerWorldUnit, Time.deltaTime);
                BestDistanceMeters = Mathf.Max(BestDistanceMeters, CurrentDistanceMeters);

                if (obstacleSpawner != null)
                {
                    obstacleSpawner.SetDifficulty(CalculateDifficulty(CurrentDistanceMeters, difficultyRampDistanceMeters));
                }
            }

            distanceHud?.Render(CurrentState, CurrentDistanceMeters, BestDistanceMeters, LastCalibrationFailed);
        }

        public void BeginCalibrationAndStart()
        {
            if (magnetAngleTracker == null || CurrentState == GameSessionState.Boot)
            {
                return;
            }

            LastCalibrationFailed = false;
            SetState(GameSessionState.Boot);
            magnetAngleTracker.ResetAngle();
            magnetAngleTracker.Calibrate(HandleCalibrationFinished);
        }

        public void RetryRun()
        {
            if (CurrentState != GameSessionState.GameOver && CurrentState != GameSessionState.Ready)
            {
                return;
            }

            StartRun();
        }

        public void HandlePlayerCollision()
        {
            if (CurrentState != GameSessionState.Playing)
            {
                return;
            }

            BestDistanceMeters = Mathf.Max(BestDistanceMeters, CurrentDistanceMeters);
            PlayerPrefs.SetFloat(BestDistanceKey, BestDistanceMeters);
            PlayerPrefs.Save();

            playerController?.SetSimulationEnabled(false);
            obstacleSpawner?.StopSimulation();
            SetState(GameSessionState.GameOver);
        }

        public static float AdvanceDistance(float currentDistanceMeters, float scrollSpeed, float metersPerWorldUnit, float deltaTime)
        {
            return currentDistanceMeters +
                   Mathf.Max(0f, scrollSpeed) *
                   Mathf.Max(0f, metersPerWorldUnit) *
                   Mathf.Max(0f, deltaTime);
        }

        public static float CalculateDifficulty(float currentDistanceMeters, float difficultyRampDistanceMeters)
        {
            if (difficultyRampDistanceMeters <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(currentDistanceMeters / difficultyRampDistanceMeters);
        }

        private void HandleCalibrationFinished(bool success)
        {
            if (!success)
            {
                LastCalibrationFailed = true;
                SetState(GameSessionState.AwaitingCalibration);
                return;
            }

            LastCalibrationFailed = false;
            SetState(GameSessionState.Ready);
            StartRun();
        }

        private void StartRun()
        {
            CurrentDistanceMeters = 0f;
            LastCalibrationFailed = false;

            if (playerController != null)
            {
                playerController.ResetForRound();
                playerController.SetSimulationEnabled(true);
            }

            if (obstacleSpawner != null)
            {
                obstacleSpawner.playerController = playerController;
                obstacleSpawner.ResetRun();
                obstacleSpawner.SetScrollSpeed(playerController != null ? playerController.forwardSpeed : 0f);
                obstacleSpawner.SetDifficulty(0f);
                obstacleSpawner.BeginSpawning();
            }

            SetState(GameSessionState.Playing);
        }

        private void SetState(GameSessionState nextState)
        {
            CurrentState = nextState;

            if (nextState != GameSessionState.Playing)
            {
                playerController?.SetSimulationEnabled(false);
            }

            if (nextState == GameSessionState.AwaitingCalibration)
            {
                obstacleSpawner?.ResetRun();
            }
        }
    }
}
