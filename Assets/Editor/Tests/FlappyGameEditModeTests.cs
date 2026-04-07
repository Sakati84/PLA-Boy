using NUnit.Framework;
using Playdate.Gameplay;
using UnityEngine;

public sealed class FlappyGameEditModeTests
{
    [Test]
    public void FlightModel_NoRotation_Sinks()
    {
        float nextVelocity = FlappyPlayerController.ComputeNextVerticalVelocity(
            0f,
            0f,
            1f,
            3f,
            0f,
            0.3f,
            0.5f,
            0.1f,
            5f,
            10f);

        Assert.Less(nextVelocity, 0f);
    }

    [Test]
    public void FlightModel_PositiveAngleDelta_AddsLift()
    {
        float nextVelocity = FlappyPlayerController.ComputeNextVerticalVelocity(
            0f,
            15f,
            1f,
            2f,
            0f,
            0.35f,
            0.5f,
            0.1f,
            10f,
            10f);

        Assert.Greater(nextVelocity, 0f);
    }

    [Test]
    public void FlightModel_NegativeAngleDelta_IncreasesFallSpeed()
    {
        float neutralVelocity = FlappyPlayerController.ComputeNextVerticalVelocity(
            0f,
            0f,
            1f,
            2f,
            0f,
            0.35f,
            0.5f,
            0.1f,
            10f,
            10f);
        float diveVelocity = FlappyPlayerController.ComputeNextVerticalVelocity(
            0f,
            -15f,
            1f,
            2f,
            0f,
            0.35f,
            0.5f,
            0.1f,
            10f,
            10f);

        Assert.Less(diveVelocity, neutralVelocity);
    }

    [Test]
    public void FlightModel_Deadzone_IgnoresTinyInput()
    {
        float noInputVelocity = FlappyPlayerController.ComputeNextVerticalVelocity(
            0f,
            0f,
            1f,
            3f,
            0.2f,
            0.35f,
            0.5f,
            0.25f,
            10f,
            10f);
        float tinyInputVelocity = FlappyPlayerController.ComputeNextVerticalVelocity(
            0f,
            0.2f,
            1f,
            3f,
            0.2f,
            0.35f,
            0.5f,
            0.25f,
            10f,
            10f);

        Assert.AreEqual(noInputVelocity, tinyInputVelocity, 0.0001f);
    }

    [Test]
    public void DistanceAdvance_IsLinear()
    {
        float distance = GameSessionController.AdvanceDistance(5f, 4f, 2f, 1.5f);

        Assert.AreEqual(17f, distance, 0.0001f);
    }

    [Test]
    public void FlightModel_UpwardWind_AddsLift()
    {
        float calmVelocity = FlappyPlayerController.ComputeNextVerticalVelocity(
            0f,
            0f,
            1f,
            3f,
            0f,
            0.3f,
            0.5f,
            0.1f,
            10f,
            10f,
            0f);
        float windyVelocity = FlappyPlayerController.ComputeNextVerticalVelocity(
            0f,
            0f,
            1f,
            3f,
            0f,
            0.3f,
            0.5f,
            0.1f,
            10f,
            10f,
            5f);

        Assert.Greater(windyVelocity, calmVelocity);
    }

    [Test]
    public void ObstacleSelection_ReturnsOnlyEligiblePatterns()
    {
        ObstaclePatternDefinition easy = ScriptableObject.CreateInstance<ObstaclePatternDefinition>();
        ObstaclePatternDefinition hard = ScriptableObject.CreateInstance<ObstaclePatternDefinition>();

        easy.minDifficulty = 0f;
        easy.maxDifficulty = 0.3f;
        easy.weight = 1f;

        hard.minDifficulty = 0.7f;
        hard.maxDifficulty = 1f;
        hard.weight = 1f;

        ObstaclePatternDefinition selected = ObstacleSpawner.SelectPatternForDifficulty(
            new[] { easy, hard },
            0.9f,
            0.5f);

        Assert.AreSame(hard, selected);

        Object.DestroyImmediate(easy);
        Object.DestroyImmediate(hard);
    }
}
