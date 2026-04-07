using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public sealed class MagnetometerDisplay : MonoBehaviour
{
    private enum AutoCalibrationState
    {
        Idle,
        Capturing
    }

    private enum InterpolationDistanceMode
    {
        AbsoluteVector,
        NormalizedVector,
        MinMaxNormalized,
        CircleProjection,
        DirectCenterAngle
    }

    [Serializable]
    private sealed class ReferenceSlot
    {
        public string label;
        public float angle;
        public bool hasValue;
        public Vector3 value;
        public Button button;
        public RectTransform marker;
        public Image markerImage;
    }

    private static MagnetometerDisplay instance;

    [Header("Scene References")]
    [SerializeField] private Canvas existingCanvas;
    [SerializeField] private TextMeshProUGUI existingValueLabel;
    [SerializeField] private TextMeshProUGUI existingCalibrationLabel;
    [SerializeField] private RectTransform existingCircleContainer;
    [SerializeField] private RectTransform existingLiveMarker;
    [SerializeField] private RectTransform existingInterpolatedMarker;
    [SerializeField] private Button interpolationModeButton;
    [SerializeField] private TextMeshProUGUI interpolationModeLabel;
    [SerializeField] private Button autoCalibrationButton;
    [SerializeField] private TextMeshProUGUI autoCalibrationLabel;
    [SerializeField] private Button resetSlotsButton;
    [SerializeField] private TextMeshProUGUI resetSlotsLabel;
    [SerializeField] private Button hysteresisMinusButton;
    [SerializeField] private Button hysteresisPlusButton;
    [SerializeField] private TextMeshProUGUI hysteresisValueLabel;
    [SerializeField] private Button hitDominanceMinusButton;
    [SerializeField] private Button hitDominancePlusButton;
    [SerializeField] private TextMeshProUGUI hitDominanceValueLabel;
    [SerializeField] private Button smoothingMinusButton;
    [SerializeField] private Button smoothingPlusButton;
    [SerializeField] private TextMeshProUGUI smoothingValueLabel;

    [Header("Buttons")]
    [SerializeField] private Button slot0Button;
    [SerializeField] private Button slot45Button;
    [SerializeField] private Button slot90Button;
    [SerializeField] private Button slot135Button;
    [SerializeField] private Button slot180Button;
    [SerializeField] private Button slot225Button;
    [SerializeField] private Button slot270Button;
    [SerializeField] private Button slot315Button;

    [Header("Circle Markers")]
    [SerializeField] private RectTransform slot0Marker;
    [SerializeField] private RectTransform slot45Marker;
    [SerializeField] private RectTransform slot90Marker;
    [SerializeField] private RectTransform slot135Marker;
    [SerializeField] private RectTransform slot180Marker;
    [SerializeField] private RectTransform slot225Marker;
    [SerializeField] private RectTransform slot270Marker;
    [SerializeField] private RectTransform slot315Marker;

    [Header("Interpolation Stabilization")]
    [SerializeField] private bool enableInterpolationStabilization = true;
    [SerializeField] [Range(0.1f, 1f)] private float slotSwitchHysteresisRatio = 0.85f;
    [SerializeField] [Range(0.05f, 1f)] private float slotHitDominanceRatio = 0.35f;
    [SerializeField] [Range(0f, 25f)] private float interpolationAngleSmoothing = 10f;

    private readonly ReferenceSlot[] slots = new ReferenceSlot[8];
    private readonly List<Vector3> autoCalibrationSamples = new();

    private Vector3 rawMagneticField;
    private Vector3 magneticField;
    private string statusMessage = "Initialisiere Magnetometer...";
    private string calibrationMessage = "Speichere acht Referenzen fuer 0 bis 315 Grad.";
    private string interpolationMessage = string.Empty;
    private TextMeshProUGUI valueLabel;
    private TextMeshProUGUI calibrationLabel;
    private RectTransform circleContainer;
    private RectTransform liveMarker;
    private Image liveMarkerImage;
    private RectTransform interpolatedMarker;
    private Image interpolatedMarkerImage;
    private Transform visualizationRoot;
    private LineRenderer absoluteVectorLine;
    private LineRenderer normalizedVectorLine;
    private Transform absoluteArrowHead;
    private Transform normalizedArrowHead;
    private int currentSnappedSlotIndex = -1;
    private int currentInterpolationAnchorSlotIndex = -1;
    private float smoothedInterpolationAngle;
    private bool hasSmoothedInterpolationAngle;
    private AutoCalibrationState autoCalibrationState = AutoCalibrationState.Idle;
    private float autoCalibrationTimeRemaining;
    private InterpolationDistanceMode interpolationDistanceMode = InterpolationDistanceMode.AbsoluteVector;
    private bool hasDirectAngleCalibration;
    private Vector2 directAngleCenterXZ;
    private Vector2 directAngleExtentsXZ;

    private const float MaxFieldMagnitude = 250f;
    private const float AbsoluteVectorLength = 3f;
    private const float NormalizedVectorLength = 2.5f;
    private const float CircleRadius = 110f;
    private const float MinFieldMagnitude = 0.0001f;
    private const float ApproximateDistanceScale = 0.5f;
    private const float MinNormalizationRange = 0.0001f;
    private const float MinDirectAngleRadius = 0.0001f;
    private const float MinHysteresisRatio = 0.1f;
    private const float MaxHysteresisRatio = 1f;
    private const float MinDominanceRatio = 0.05f;
    private const float MaxDominanceRatio = 1f;
    private const float MinAngleSmoothing = 0f;
    private const float MaxAngleSmoothing = 25f;
    private const float RatioStep = 0.05f;
    private const float SmoothingStep = 1f;
    private const float AutoCalibrationDuration = 5f;
    private static readonly Color ReadyColor = new(0.2f, 0.9f, 0.55f, 1f);
    private static readonly Color MissingColor = new(0.45f, 0.5f, 0.6f, 0.9f);
    private static readonly Color ActiveSnapColor = new(1f, 0.8f, 0.2f, 1f);
    private static readonly Color InterpolatedMarkerColor = new(0.3f, 0.82f, 1f, 0.95f);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        InitializeSlots();
        CacheUiReferences();
        EnsureEventSystemUsesInputSystem();
        CreateVisualization();
        RefreshCircleVisualization();
        RefreshUi();
    }

    private void OnEnable()
    {
        if (MagneticFieldSensor.current != null)
        {
            InputSystem.EnableDevice(MagneticFieldSensor.current);
        }
    }

    private void Update()
    {
        rawMagneticField = MagneticFieldSensor.current != null
            ? MagneticFieldSensor.current.magneticField.ReadValue()
            : Vector3.zero;

        magneticField = ApproximatePosition(rawMagneticField, ApproximateDistanceScale);

        statusMessage = MagneticFieldSensor.current == null
            ? Application.isMobilePlatform ? "Kein Magnetometer-Sensor verfuegbar." : "Magnetometerwerte sind nur auf einem Mobilgeraet mit Sensor verfuegbar."
            : rawMagneticField == Vector3.zero
                ? "Magnetometer aktiv, aber es kommen noch keine Sensordaten."
                : $"Magnetometer aktiv (Input System, Distanz-Skalierung {Format(ApproximateDistanceScale)})";

        UpdateAutoCalibrationCapture();
        RefreshVisualization();
        RefreshCircleVisualization();
        RefreshUi();
    }

    private void InitializeSlots()
    {
        SetSlot(0, "0 Grad", 0f, slot0Button, slot0Marker);
        SetSlot(1, "45 Grad", 45f, slot45Button, slot45Marker);
        SetSlot(2, "90 Grad", 90f, slot90Button, slot90Marker);
        SetSlot(3, "135 Grad", 135f, slot135Button, slot135Marker);
        SetSlot(4, "180 Grad", 180f, slot180Button, slot180Marker);
        SetSlot(5, "225 Grad", 225f, slot225Button, slot225Marker);
        SetSlot(6, "270 Grad", 270f, slot270Button, slot270Marker);
        SetSlot(7, "315 Grad", 315f, slot315Button, slot315Marker);

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].button == null)
            {
                continue;
            }

            int slotIndex = i;
            slots[i].button.onClick.RemoveAllListeners();
            slots[i].button.onClick.AddListener(() => SaveCurrentVectorToSlot(slotIndex));
        }

        if (interpolationModeButton != null)
        {
            interpolationModeButton.onClick.RemoveAllListeners();
            interpolationModeButton.onClick.AddListener(ToggleInterpolationDistanceMode);
        }

        if (autoCalibrationButton != null)
        {
            autoCalibrationButton.onClick.RemoveAllListeners();
            autoCalibrationButton.onClick.AddListener(StartAutoCalibration);
        }

        if (resetSlotsButton != null)
        {
            resetSlotsButton.onClick.RemoveAllListeners();
            resetSlotsButton.onClick.AddListener(ResetAllSlots);
        }

        BindAdjustmentButton(hysteresisMinusButton, () => AdjustSlotSwitchHysteresis(-RatioStep));
        BindAdjustmentButton(hysteresisPlusButton, () => AdjustSlotSwitchHysteresis(RatioStep));
        BindAdjustmentButton(hitDominanceMinusButton, () => AdjustSlotHitDominance(-RatioStep));
        BindAdjustmentButton(hitDominancePlusButton, () => AdjustSlotHitDominance(RatioStep));
        BindAdjustmentButton(smoothingMinusButton, () => AdjustInterpolationAngleSmoothing(-SmoothingStep));
        BindAdjustmentButton(smoothingPlusButton, () => AdjustInterpolationAngleSmoothing(SmoothingStep));
    }

    private static void BindAdjustmentButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void SetSlot(int index, string label, float angle, Button button, RectTransform marker)
    {
        slots[index] = new ReferenceSlot
        {
            label = label,
            angle = angle,
            button = button,
            marker = marker,
            markerImage = marker != null ? marker.GetComponent<Image>() : null
        };
    }

    private void CacheUiReferences()
    {
        valueLabel = existingValueLabel;
        calibrationLabel = existingCalibrationLabel;
        circleContainer = existingCircleContainer;
        liveMarker = existingLiveMarker;
        liveMarkerImage = liveMarker != null ? liveMarker.GetComponent<Image>() : null;
        interpolatedMarker = existingInterpolatedMarker;
        interpolatedMarkerImage = interpolatedMarker != null ? interpolatedMarker.GetComponent<Image>() : null;
        UpdateInterpolationModeLabel();
        UpdateAutoCalibrationButtonLabel();
        UpdateResetButtonLabel();
        UpdateStabilizationLabels();

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].marker != null)
            {
                slots[i].markerImage = slots[i].marker.GetComponent<Image>();
            }
        }
    }

    private void RefreshUi()
    {
        if (valueLabel == null)
        {
            return;
        }

        float rawMagnitude = rawMagneticField.magnitude;
        float magnitude = magneticField.magnitude;
        Vector3 normalized = magnitude > Mathf.Epsilon ? magneticField / magnitude : Vector3.zero;
        Vector2 projected = ProjectToCirclePlane(magneticField);

        valueLabel.text =
            "Magnetometer\n" +
            $"Status: {statusMessage}\n\n" +
            $"Raw X: {Format(rawMagneticField.x)} uT\n" +
            $"Raw Y: {Format(rawMagneticField.y)} uT\n" +
            $"Raw Z: {Format(rawMagneticField.z)} uT\n" +
            $"Raw Betrag: {Format(rawMagnitude)} uT\n" +
            $"Approx Position: ({Format(magneticField.x)}, {Format(magneticField.y)}, {Format(magneticField.z)})\n" +
            $"Approx Distanz: {Format(magnitude)}\n" +
            $"Normalisierter Positionsvektor: ({Format(normalized.x)}, {Format(normalized.y)}, {Format(normalized.z)})\n" +
            $"Kreis-Projektion XZ: ({Format(projected.x)}, {Format(projected.y)})\n\n" +
            BuildSlotStatusText();

        if (calibrationLabel != null)
        {
            calibrationLabel.text = string.IsNullOrEmpty(interpolationMessage)
                ? calibrationMessage
                : $"{calibrationMessage}\n{interpolationMessage}";
        }

        if (interpolatedMarkerImage != null)
        {
            interpolatedMarkerImage.color = InterpolatedMarkerColor;
        }

        UpdateButtonInteractivity();
        UpdateStabilizationLabels();

        foreach (var slot in slots)
        {
            Color slotColor = !slot.hasValue
                ? MissingColor
                : Array.IndexOf(slots, slot) == currentSnappedSlotIndex ? ActiveSnapColor : ReadyColor;

            if (slot.markerImage != null)
            {
                slot.markerImage.color = slotColor;
            }

            if (slot.button != null && slot.button.image != null)
            {
                slot.button.image.color = slotColor;
            }
        }
    }

    private string BuildSlotStatusText()
    {
        string text = string.Empty;
        foreach (var slot in slots)
        {
            text += slot.hasValue
                ? $"{slot.label}: gespeichert ({Format(slot.value.x)}, {Format(slot.value.y)}, {Format(slot.value.z)})\n"
                : $"{slot.label}: offen\n";
        }

        return text.TrimEnd();
    }

    private void SaveCurrentVectorToSlot(int slotIndex)
    {
        if (IsAutoCalibrationRunning())
        {
            calibrationMessage = $"Auto-Kalibrierung laeuft ({Format(autoCalibrationTimeRemaining)} s). Manuelles Speichern ist pausiert.";
            RefreshUi();
            return;
        }

        if (!TryGetMagneticVector(magneticField, out var absoluteVector))
        {
            calibrationMessage = "Speichern nicht moeglich: aktueller Magnet-Vektor ist ungueltig.";
            RefreshCircleVisualization();
            RefreshUi();
            return;
        }

        slots[slotIndex].value = absoluteVector;
        slots[slotIndex].hasValue = true;
        calibrationMessage =
            $"{slots[slotIndex].label} gespeichert: ({Format(absoluteVector.x)}, {Format(absoluteVector.y)}, {Format(absoluteVector.z)})";
        RefreshCircleVisualization();
        RefreshUi();
    }

    private void RefreshCircleVisualization()
    {
        if (liveMarker == null || liveMarkerImage == null)
        {
            return;
        }

        EnsureMarkerLayout();
        interpolationMessage = string.Empty;
        if (IsAutoCalibrationRunning())
        {
            currentSnappedSlotIndex = -1;
            ResetInterpolationTracking();
            if (liveMarker != null)
            {
                liveMarker.gameObject.SetActive(false);
            }

            if (interpolatedMarker != null)
            {
                interpolatedMarker.gameObject.SetActive(false);
            }

            return;
        }

        RefreshInterpolatedMarker();

        if (interpolationDistanceMode == InterpolationDistanceMode.DirectCenterAngle)
        {
            currentSnappedSlotIndex = -1;
            liveMarker.gameObject.SetActive(false);
            return;
        }

        if (!AllSlotsConfigured())
        {
            currentSnappedSlotIndex = -1;
            liveMarker.gameObject.SetActive(false);
            calibrationMessage = "Noch nicht kalibriert. Speichere alle acht Referenzen.";
            return;
        }

        if (!TryGetMagneticVector(magneticField, out var current))
        {
            currentSnappedSlotIndex = -1;
            liveMarker.gameObject.SetActive(false);
            calibrationMessage = "Aktuell kein gueltiger Magnet-Vektor fuer das Slot-Matching.";
            return;
        }

        int snappedIndex = GetClosestSlotIndex(current);
        float distance = Vector3.Distance(current, slots[snappedIndex].value);
        currentSnappedSlotIndex = snappedIndex;
        liveMarker.gameObject.SetActive(true);
        liveMarker.anchoredPosition = AngleToCirclePosition(slots[snappedIndex].angle, CircleRadius);
        calibrationMessage = $"Gesnappte Position: {slots[snappedIndex].label} (Distanz {Format(distance)})";
    }

    private void RefreshInterpolatedMarker()
    {
        if (interpolatedMarker == null || interpolatedMarkerImage == null)
        {
            return;
        }

        if (interpolationDistanceMode == InterpolationDistanceMode.DirectCenterAngle)
        {
            RefreshDirectAngleMarker();
            return;
        }

        if (!TryGetInterpolationVector(magneticField, out var current))
        {
            interpolatedMarker.gameObject.SetActive(false);
            ResetInterpolationTracking();
            return;
        }

        if (!TryGetInterpolationPair(current, out int startIndex, out int endIndex, out float startDistance, out float endDistance))
        {
            interpolatedMarker.gameObject.SetActive(false);
            ResetInterpolationTracking();
            return;
        }

        float rawInterpolatedAngle;
        string interpolationDetail;
        float totalDistance = startDistance + endDistance;
        bool lockToSlot = enableInterpolationStabilization &&
            Mathf.Max(startDistance, endDistance) > Mathf.Epsilon &&
            Mathf.Min(startDistance, endDistance) <= Mathf.Max(startDistance, endDistance) * slotHitDominanceRatio;

        if (lockToSlot)
        {
            bool startWins = startDistance <= endDistance;
            int lockedSlotIndex = startWins ? startIndex : endIndex;
            rawInterpolatedAngle = slots[lockedSlotIndex].angle;
            interpolationDetail = $"Trefferzone aktiv bei {slots[lockedSlotIndex].label}";
        }
        else
        {
            float t = totalDistance <= Mathf.Epsilon ? 0.5f : Mathf.Clamp01(startDistance / totalDistance);
            rawInterpolatedAngle = Mathf.Repeat(
                slots[startIndex].angle + GetInterpolationArcDelta(slots[startIndex].angle, slots[endIndex].angle) * t,
                360f);
            interpolationDetail = $"Segment {slots[startIndex].label} -> {slots[endIndex].label}";
        }

        float interpolatedAngle = ApplyInterpolationAngleSmoothing(rawInterpolatedAngle);

        interpolatedMarker.gameObject.SetActive(true);
        interpolatedMarker.anchoredPosition = AngleToCirclePosition(interpolatedAngle, CircleRadius);
        interpolationMessage =
            $"Interpolation ({GetInterpolationModeName()}): zwischen {slots[startIndex].label} (Distanz {Format(startDistance)}{GetInterpolationDistanceUnit()}) und {slots[endIndex].label} (Distanz {Format(endDistance)}{GetInterpolationDistanceUnit()}) bei {Format(interpolatedAngle)} Grad | {interpolationDetail}";
    }

    private void RefreshDirectAngleMarker()
    {
        if (interpolatedMarker == null || interpolatedMarkerImage == null)
        {
            return;
        }

        currentInterpolationAnchorSlotIndex = -1;

        if (!TryGetDirectCenterAngle(magneticField, out float rawAngle, out string detail))
        {
            interpolatedMarker.gameObject.SetActive(false);
            hasSmoothedInterpolationAngle = false;
            interpolationMessage = $"Direktmodus: {detail}";
            return;
        }

        float interpolatedAngle = ApplyInterpolationAngleSmoothing(rawAngle);
        interpolatedMarker.gameObject.SetActive(true);
        interpolatedMarker.anchoredPosition = AngleToCirclePosition(interpolatedAngle, CircleRadius);
        interpolationMessage = $"Direktmodus: {Format(interpolatedAngle)} Grad | {detail}";
    }

    private void EnsureMarkerLayout()
    {
        foreach (var slot in slots)
        {
            if (slot.marker == null)
            {
                continue;
            }

            slot.marker.anchorMin = new Vector2(0.5f, 0.5f);
            slot.marker.anchorMax = new Vector2(0.5f, 0.5f);
            slot.marker.pivot = new Vector2(0.5f, 0.5f);
            slot.marker.anchoredPosition = AngleToCirclePosition(slot.angle, CircleRadius);
        }

        liveMarker.anchorMin = new Vector2(0.5f, 0.5f);
        liveMarker.anchorMax = new Vector2(0.5f, 0.5f);
        liveMarker.pivot = new Vector2(0.5f, 0.5f);

        if (interpolatedMarker != null)
        {
            interpolatedMarker.anchorMin = new Vector2(0.5f, 0.5f);
            interpolatedMarker.anchorMax = new Vector2(0.5f, 0.5f);
            interpolatedMarker.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    private bool AllSlotsConfigured()
    {
        foreach (var slot in slots)
        {
            if (!slot.hasValue)
            {
                return false;
            }
        }

        return true;
    }

    private int GetClosestSlotIndex(Vector3 current)
    {
        int bestIndex = 0;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < slots.Length; i++)
        {
            float distance = Vector3.Distance(current, slots[i].value);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private bool TryGetInterpolationPair(Vector3 current, out int startIndex, out int endIndex, out float startDistance, out float endDistance)
    {
        startIndex = -1;
        endIndex = -1;
        startDistance = float.PositiveInfinity;
        endDistance = float.PositiveInfinity;
        if (!TryGetClosestInterpolationSlots(current, out int candidateIndex, out float candidateDistance, out _, out _))
        {
            return false;
        }

        int anchorIndex = ResolveInterpolationAnchorSlot(current, candidateIndex, candidateDistance);
        if (anchorIndex < 0)
        {
            return false;
        }

        if (!TryGetNearestInterpolationNeighbor(current, anchorIndex, out int neighborIndex, out float neighborDistance))
        {
            return false;
        }

        currentInterpolationAnchorSlotIndex = anchorIndex;
        startIndex = anchorIndex;
        endIndex = neighborIndex;
        startDistance = GetInterpolationDistance(current, anchorIndex);
        endDistance = neighborDistance;
        return true;
    }

    private bool TryGetClosestInterpolationSlots(Vector3 current, out int bestIndex, out float bestDistance, out int secondIndex, out float secondDistance)
    {
        bestIndex = -1;
        secondIndex = -1;
        bestDistance = float.PositiveInfinity;
        secondDistance = float.PositiveInfinity;

        for (int i = 0; i < slots.Length; i++)
        {
            float distance = GetInterpolationDistance(current, i);
            if (float.IsPositiveInfinity(distance))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                secondDistance = bestDistance;
                secondIndex = bestIndex;
                bestDistance = distance;
                bestIndex = i;
            }
            else if (distance < secondDistance)
            {
                secondDistance = distance;
                secondIndex = i;
            }
        }

        return bestIndex >= 0 && secondIndex >= 0;
    }

    private int ResolveInterpolationAnchorSlot(Vector3 current, int candidateIndex, float candidateDistance)
    {
        if (!enableInterpolationStabilization || currentInterpolationAnchorSlotIndex < 0)
        {
            return candidateIndex;
        }

        float anchorDistance = GetInterpolationDistance(current, currentInterpolationAnchorSlotIndex);
        if (float.IsPositiveInfinity(anchorDistance))
        {
            return candidateIndex;
        }

        if (candidateIndex == currentInterpolationAnchorSlotIndex)
        {
            return candidateIndex;
        }

        return candidateDistance <= anchorDistance * slotSwitchHysteresisRatio
            ? candidateIndex
            : currentInterpolationAnchorSlotIndex;
    }

    private bool TryGetNearestInterpolationNeighbor(Vector3 current, int anchorIndex, out int neighborIndex, out float neighborDistance)
    {
        neighborIndex = -1;
        neighborDistance = float.PositiveInfinity;

        for (int i = 0; i < slots.Length; i++)
        {
            if (i == anchorIndex)
            {
                continue;
            }

            float distance = GetInterpolationDistance(current, i);
            if (float.IsPositiveInfinity(distance))
            {
                continue;
            }

            if (distance < neighborDistance)
            {
                neighborDistance = distance;
                neighborIndex = i;
            }
        }

        return neighborIndex >= 0;
    }

    private float GetInterpolationDistance(Vector3 current, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length || !slots[slotIndex].hasValue)
        {
            return float.PositiveInfinity;
        }

        if (!TryGetInterpolationVector(slots[slotIndex].value, out var comparisonValue))
        {
            return float.PositiveInfinity;
        }

        return Vector3.Distance(current, comparisonValue);
    }

    private static float GetInterpolationArcDelta(float startAngle, float endAngle)
    {
        float wrappedDelta = Mathf.Repeat(endAngle - startAngle, 360f);
        if (Mathf.Approximately(wrappedDelta, 180f))
        {
            return -180f;
        }

        return wrappedDelta > 180f ? wrappedDelta - 360f : wrappedDelta;
    }

    private static bool TryGetMagneticVector(Vector3 field, out Vector3 validField)
    {
        if (field.sqrMagnitude <= MinFieldMagnitude * MinFieldMagnitude)
        {
            validField = Vector3.zero;
            return false;
        }

        validField = field;
        return true;
    }

    private static Vector3 ApproximatePosition(Vector3 magneticField_uT, float scale)
    {
        float fieldStrength = magneticField_uT.magnitude;
        if (fieldStrength < MinFieldMagnitude)
        {
            return Vector3.zero;
        }

        float distance = scale / Mathf.Pow(fieldStrength, 1f / 3f);
        return magneticField_uT.normalized * distance;
    }

    private bool TryGetInterpolationVector(Vector3 source, out Vector3 interpolationVector)
    {
        if (!TryGetMagneticVector(source, out var validVector))
        {
            interpolationVector = Vector3.zero;
            return false;
        }

        switch (interpolationDistanceMode)
        {
            case InterpolationDistanceMode.AbsoluteVector:
                interpolationVector = validVector;
                return true;
            case InterpolationDistanceMode.NormalizedVector:
                interpolationVector = validVector.normalized;
                return true;
            case InterpolationDistanceMode.MinMaxNormalized:
                return TryGetMinMaxNormalizedVector(validVector, out interpolationVector);
            case InterpolationDistanceMode.CircleProjection:
                return TryGetCircleProjectionVector(validVector, out interpolationVector);
            default:
                interpolationVector = validVector;
                return true;
        }
    }

    private void ToggleInterpolationDistanceMode()
    {
        if (IsAutoCalibrationRunning())
        {
            calibrationMessage = $"Auto-Kalibrierung laeuft ({Format(autoCalibrationTimeRemaining)} s). Interpolation bleibt unveraendert.";
            RefreshUi();
            return;
        }

        interpolationDistanceMode = interpolationDistanceMode switch
        {
            InterpolationDistanceMode.AbsoluteVector => InterpolationDistanceMode.NormalizedVector,
            InterpolationDistanceMode.NormalizedVector => InterpolationDistanceMode.MinMaxNormalized,
            InterpolationDistanceMode.MinMaxNormalized => InterpolationDistanceMode.CircleProjection,
            InterpolationDistanceMode.CircleProjection => InterpolationDistanceMode.DirectCenterAngle,
            _ => InterpolationDistanceMode.AbsoluteVector
        };

        ResetInterpolationTracking();
        UpdateInterpolationModeLabel();
        RefreshCircleVisualization();
        RefreshUi();
    }

    private void UpdateInterpolationModeLabel()
    {
        if (interpolationModeLabel == null)
        {
            return;
        }

        interpolationModeLabel.text = $"Interpolation: {GetInterpolationModeName()}";
    }

    private void UpdateAutoCalibrationButtonLabel()
    {
        if (autoCalibrationLabel == null)
        {
            return;
        }

        autoCalibrationLabel.text = IsAutoCalibrationRunning()
            ? $"Kurbel kalibrieren ({Format(autoCalibrationTimeRemaining)} s)"
            : "Kurbel kalibrieren";
    }

    private void UpdateResetButtonLabel()
    {
        if (resetSlotsLabel == null)
        {
            return;
        }

        resetSlotsLabel.text = "Reset Slots";
    }

    private void UpdateStabilizationLabels()
    {
        if (hysteresisValueLabel != null)
        {
            hysteresisValueLabel.text = $"Hysterese: {Format(slotSwitchHysteresisRatio)}";
        }

        if (hitDominanceValueLabel != null)
        {
            hitDominanceValueLabel.text = $"Trefferzone: {Format(slotHitDominanceRatio)}";
        }

        if (smoothingValueLabel != null)
        {
            smoothingValueLabel.text = $"Glaettung: {Format(interpolationAngleSmoothing)}";
        }
    }

    private void ResetAllSlots()
    {
        if (IsAutoCalibrationRunning())
        {
            calibrationMessage = $"Auto-Kalibrierung laeuft ({Format(autoCalibrationTimeRemaining)} s). Reset ist pausiert.";
            RefreshUi();
            return;
        }

        ClearCalibrationState();
        calibrationMessage = "Noch nicht kalibriert. Speichere alle acht Referenzen.";

        if (liveMarker != null)
        {
            liveMarker.gameObject.SetActive(false);
        }

        if (interpolatedMarker != null)
        {
            interpolatedMarker.gameObject.SetActive(false);
        }

        RefreshCircleVisualization();
        RefreshUi();
    }

    private void StartAutoCalibration()
    {
        if (IsAutoCalibrationRunning())
        {
            return;
        }

        ClearCalibrationState();
        autoCalibrationState = AutoCalibrationState.Capturing;
        autoCalibrationTimeRemaining = AutoCalibrationDuration;
        calibrationMessage = "Auto-Kalibrierung gestartet. Drehe die Kurbel jetzt 5 Sekunden lang.";

        if (liveMarker != null)
        {
            liveMarker.gameObject.SetActive(false);
        }

        if (interpolatedMarker != null)
        {
            interpolatedMarker.gameObject.SetActive(false);
        }

        UpdateAutoCalibrationButtonLabel();
        RefreshUi();
    }

    private void ClearCalibrationState()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].hasValue = false;
            slots[i].value = Vector3.zero;
        }

        autoCalibrationSamples.Clear();
        hasDirectAngleCalibration = false;
        directAngleCenterXZ = Vector2.zero;
        directAngleExtentsXZ = Vector2.zero;
        currentSnappedSlotIndex = -1;
        interpolationDistanceMode = InterpolationDistanceMode.AbsoluteVector;
        ResetInterpolationTracking();
        UpdateInterpolationModeLabel();
    }

    private void UpdateAutoCalibrationCapture()
    {
        if (!IsAutoCalibrationRunning())
        {
            return;
        }

        autoCalibrationTimeRemaining = Mathf.Max(0f, autoCalibrationTimeRemaining - Time.deltaTime);
        if (TryGetMagneticVector(magneticField, out var sample))
        {
            autoCalibrationSamples.Add(sample);
        }

        calibrationMessage = autoCalibrationTimeRemaining > 0f
            ? $"Auto-Kalibrierung aktiv. Drehe die Kurbel jetzt ({Format(autoCalibrationTimeRemaining)} s verbleibend)."
            : "Auto-Kalibrierung wird ausgewertet...";

        if (autoCalibrationTimeRemaining > 0f)
        {
            UpdateAutoCalibrationButtonLabel();
            return;
        }

        CompleteAutoCalibration();
    }

    private void CompleteAutoCalibration()
    {
        autoCalibrationState = AutoCalibrationState.Idle;
        autoCalibrationTimeRemaining = 0f;

        if (!TryApplyAutoCalibration())
        {
            UpdateAutoCalibrationButtonLabel();
            RefreshUi();
            return;
        }

        currentSnappedSlotIndex = -1;
        interpolationDistanceMode = InterpolationDistanceMode.DirectCenterAngle;
        ResetInterpolationTracking();
        UpdateAutoCalibrationButtonLabel();
        UpdateInterpolationModeLabel();
        RefreshCircleVisualization();
        calibrationMessage = "Auto-Kalibrierung abgeschlossen. Alle Slots wurden automatisch gesetzt und Direktmodus ist aktiv.";
        RefreshUi();
    }

    private bool TryApplyAutoCalibration()
    {
        if (autoCalibrationSamples.Count == 0)
        {
            calibrationMessage = "Auto-Kalibrierung fehlgeschlagen: In den 5 Sekunden wurden keine gueltigen Magnetometerdaten erfasst.";
            return false;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;

        foreach (var sample in autoCalibrationSamples)
        {
            minX = Mathf.Min(minX, sample.x);
            maxX = Mathf.Max(maxX, sample.x);
            minZ = Mathf.Min(minZ, sample.z);
            maxZ = Mathf.Max(maxZ, sample.z);
        }

        float midX = (minX + maxX) * 0.5f;
        float midZ = (minZ + maxZ) * 0.5f;
        float extentX = (maxX - minX) * 0.5f;
        float extentZ = (maxZ - minZ) * 0.5f;

        if (extentX <= MinDirectAngleRadius || extentZ <= MinDirectAngleRadius)
        {
            calibrationMessage = "Auto-Kalibrierung fehlgeschlagen: X/Z-Ausdehnung ist zu klein fuer einen stabilen Direktwinkel.";
            return false;
        }

        hasDirectAngleCalibration = true;
        directAngleCenterXZ = new Vector2(midX, midZ);
        directAngleExtentsXZ = new Vector2(extentX, extentZ);

        Vector2[] targetPoints =
        {
            new(minX, midZ),
            new(minX, minZ),
            new(midX, minZ),
            new(maxX, minZ),
            new(maxX, midZ),
            new(maxX, maxZ),
            new(midX, maxZ),
            new(minX, maxZ)
        };

        int[] internalSlotMapping = { 4, 5, 6, 7, 0, 1, 2, 3 };
        for (int i = 0; i < targetPoints.Length; i++)
        {
            Vector3 nearestSample = FindClosestAutoCalibrationSample(targetPoints[i]);
            int internalSlotIndex = internalSlotMapping[i];
            slots[internalSlotIndex].value = nearestSample;
            slots[internalSlotIndex].hasValue = true;
        }

        return true;
    }

    private Vector3 FindClosestAutoCalibrationSample(Vector2 target)
    {
        Vector3 closest = autoCalibrationSamples[0];
        float closestDistance = float.PositiveInfinity;

        foreach (var sample in autoCalibrationSamples)
        {
            float distance = Vector2.SqrMagnitude(new Vector2(sample.x, sample.z) - target);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = sample;
            }
        }

        return closest;
    }

    private void UpdateButtonInteractivity()
    {
        bool interactable = !IsAutoCalibrationRunning();

        if (autoCalibrationButton != null)
        {
            autoCalibrationButton.interactable = interactable;
        }

        if (resetSlotsButton != null)
        {
            resetSlotsButton.interactable = interactable;
        }

        if (interpolationModeButton != null)
        {
            interpolationModeButton.interactable = interactable;
        }

        if (hysteresisMinusButton != null)
        {
            hysteresisMinusButton.interactable = interactable;
        }

        if (hysteresisPlusButton != null)
        {
            hysteresisPlusButton.interactable = interactable;
        }

        if (hitDominanceMinusButton != null)
        {
            hitDominanceMinusButton.interactable = interactable;
        }

        if (hitDominancePlusButton != null)
        {
            hitDominancePlusButton.interactable = interactable;
        }

        if (smoothingMinusButton != null)
        {
            smoothingMinusButton.interactable = interactable;
        }

        if (smoothingPlusButton != null)
        {
            smoothingPlusButton.interactable = interactable;
        }

        foreach (var slot in slots)
        {
            if (slot.button != null)
            {
                slot.button.interactable = interactable;
            }
        }

        UpdateAutoCalibrationButtonLabel();
    }

    private bool IsAutoCalibrationRunning() => autoCalibrationState == AutoCalibrationState.Capturing;

    private void AdjustSlotSwitchHysteresis(float delta)
    {
        slotSwitchHysteresisRatio = Mathf.Clamp(slotSwitchHysteresisRatio + delta, MinHysteresisRatio, MaxHysteresisRatio);
        UpdateStabilizationLabels();
        RefreshCircleVisualization();
        RefreshUi();
    }

    private void AdjustSlotHitDominance(float delta)
    {
        slotHitDominanceRatio = Mathf.Clamp(slotHitDominanceRatio + delta, MinDominanceRatio, MaxDominanceRatio);
        UpdateStabilizationLabels();
        RefreshCircleVisualization();
        RefreshUi();
    }

    private void AdjustInterpolationAngleSmoothing(float delta)
    {
        interpolationAngleSmoothing = Mathf.Clamp(interpolationAngleSmoothing + delta, MinAngleSmoothing, MaxAngleSmoothing);
        UpdateStabilizationLabels();
        RefreshCircleVisualization();
        RefreshUi();
    }

    private float ApplyInterpolationAngleSmoothing(float targetAngle)
    {
        if (!hasSmoothedInterpolationAngle || interpolationAngleSmoothing <= 0f)
        {
            smoothedInterpolationAngle = targetAngle;
            hasSmoothedInterpolationAngle = true;
            return targetAngle;
        }

        float blend = 1f - Mathf.Exp(-interpolationAngleSmoothing * Time.deltaTime);
        smoothedInterpolationAngle = Mathf.LerpAngle(smoothedInterpolationAngle, targetAngle, blend);
        return smoothedInterpolationAngle;
    }

    private string GetInterpolationModeName() => interpolationDistanceMode switch
    {
        InterpolationDistanceMode.AbsoluteVector => "Absolut",
        InterpolationDistanceMode.NormalizedVector => "Normalisiert",
        InterpolationDistanceMode.MinMaxNormalized => "Min/Max",
        InterpolationDistanceMode.CircleProjection => "Kreisprojektion",
        InterpolationDistanceMode.DirectCenterAngle => "Direktmodus",
        _ => "Absolut"
    };

    private string GetInterpolationDistanceUnit() =>
        interpolationDistanceMode == InterpolationDistanceMode.AbsoluteVector ? " uT" : string.Empty;

    private bool TryGetMinMaxNormalizedVector(Vector3 source, out Vector3 normalizedVector)
    {
        if (!TryGetSlotBounds(out var minBounds, out var maxBounds))
        {
            normalizedVector = Vector3.zero;
            return false;
        }

        normalizedVector = new Vector3(
            NormalizeAxis(source.x, minBounds.x, maxBounds.x),
            NormalizeAxis(source.y, minBounds.y, maxBounds.y),
            NormalizeAxis(source.z, minBounds.z, maxBounds.z));
        return true;
    }

    private bool TryGetCircleProjectionVector(Vector3 source, out Vector3 projectedVector)
    {
        if (!TryGetCircleProjectionFrame(out var center, out var basisX, out var basisY))
        {
            projectedVector = Vector3.zero;
            return false;
        }

        Vector3 centered = source - center;
        projectedVector = new Vector3(
            Vector3.Dot(centered, basisX),
            Vector3.Dot(centered, basisY),
            0f);
        return true;
    }

    private bool TryGetSlotBounds(out Vector3 minBounds, out Vector3 maxBounds)
    {
        minBounds = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        maxBounds = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool hasAny = false;

        foreach (var slot in slots)
        {
            if (!slot.hasValue)
            {
                continue;
            }

            hasAny = true;
            minBounds = Vector3.Min(minBounds, slot.value);
            maxBounds = Vector3.Max(maxBounds, slot.value);
        }

        return hasAny;
    }

    private bool TryGetCircleProjectionFrame(out Vector3 center, out Vector3 basisX, out Vector3 basisY)
    {
        center = Vector3.zero;
        basisX = Vector3.right;
        basisY = Vector3.up;

        Vector3[] configuredValues = new Vector3[slots.Length];
        int count = 0;
        foreach (var slot in slots)
        {
            if (!slot.hasValue)
            {
                continue;
            }

            configuredValues[count++] = slot.value;
            center += slot.value;
        }

        if (count < 3)
        {
            return false;
        }

        center /= count;

        float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = configuredValues[i] - center;
            xx += p.x * p.x;
            xy += p.x * p.y;
            xz += p.x * p.z;
            yy += p.y * p.y;
            yz += p.y * p.z;
            zz += p.z * p.z;
        }

        Vector3 normal = SmallestEigenVector(xx, xy, xz, yy, yz, zz);
        if (normal.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        normal.Normalize();

        Vector3 reference = Vector3.zero;
        float bestMagnitude = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 planar = Vector3.ProjectOnPlane(configuredValues[i] - center, normal);
            float magnitude = planar.sqrMagnitude;
            if (magnitude > bestMagnitude)
            {
                bestMagnitude = magnitude;
                reference = planar;
            }
        }

        if (reference.sqrMagnitude <= Mathf.Epsilon)
        {
            reference = Vector3.Cross(normal, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up);
        }

        basisX = reference.normalized;
        basisY = Vector3.Cross(normal, basisX).normalized;
        return basisY.sqrMagnitude > Mathf.Epsilon;
    }

    private bool TryGetDirectCenterAngle(Vector3 source, out float angle, out string detail)
    {
        angle = 0f;
        detail = string.Empty;

        if (!hasDirectAngleCalibration)
        {
            detail = "keine gueltige Mittelpunkt-Kalibrierung vorhanden.";
            return false;
        }

        if (!TryGetMagneticVector(source, out var validVector))
        {
            detail = "aktueller Magnet-Vektor ist ungueltig.";
            return false;
        }

        if (directAngleExtentsXZ.x <= MinDirectAngleRadius || directAngleExtentsXZ.y <= MinDirectAngleRadius)
        {
            detail = "Kalibrierdaten haben zu wenig X/Z-Ausdehnung.";
            return false;
        }

        Vector2 centered = new Vector2(validVector.x - directAngleCenterXZ.x, validVector.z - directAngleCenterXZ.y);
        float radius = centered.magnitude;
        if (radius <= MinDirectAngleRadius)
        {
            detail = $"Messpunkt liegt zu nah am Mittelpunkt ({Format(directAngleCenterXZ.x)}, {Format(directAngleCenterXZ.y)}).";
            return false;
        }

        angle = Mathf.Repeat(Mathf.Atan2(centered.y, centered.x) * Mathf.Rad2Deg, 360f);
        detail =
            $"XZ um Mittelpunkt ({Format(directAngleCenterXZ.x)}, {Format(directAngleCenterXZ.y)}) -> ({Format(centered.x)}, {Format(centered.y)})";
        return true;
    }

    private static float NormalizeAxis(float value, float minValue, float maxValue)
    {
        float range = maxValue - minValue;
        if (Mathf.Abs(range) <= MinNormalizationRange)
        {
            return 0.5f;
        }

        return Mathf.Clamp01((value - minValue) / range);
    }

    private static Vector3 SmallestEigenVector(float xx, float xy, float xz, float yy, float yz, float zz)
    {
        float[,] matrix =
        {
            { xx, xy, xz },
            { xy, yy, yz },
            { xz, yz, zz }
        };

        float[,] eigenVectors =
        {
            { 1f, 0f, 0f },
            { 0f, 1f, 0f },
            { 0f, 0f, 1f }
        };

        for (int iteration = 0; iteration < 10; iteration++)
        {
            int p = 0;
            int q = 1;
            float max = Mathf.Abs(matrix[0, 1]);

            if (Mathf.Abs(matrix[0, 2]) > max)
            {
                max = Mathf.Abs(matrix[0, 2]);
                p = 0;
                q = 2;
            }

            if (Mathf.Abs(matrix[1, 2]) > max)
            {
                max = Mathf.Abs(matrix[1, 2]);
                p = 1;
                q = 2;
            }

            if (max <= 0.00001f)
            {
                break;
            }

            float app = matrix[p, p];
            float aqq = matrix[q, q];
            float apq = matrix[p, q];
            float phi = 0.5f * Mathf.Atan2(2f * apq, aqq - app);
            float c = Mathf.Cos(phi);
            float s = Mathf.Sin(phi);

            for (int i = 0; i < 3; i++)
            {
                float mip = matrix[i, p];
                float miq = matrix[i, q];
                matrix[i, p] = c * mip - s * miq;
                matrix[i, q] = s * mip + c * miq;
            }

            for (int i = 0; i < 3; i++)
            {
                float mpi = matrix[p, i];
                float mqi = matrix[q, i];
                matrix[p, i] = c * mpi - s * mqi;
                matrix[q, i] = s * mpi + c * mqi;
            }

            for (int i = 0; i < 3; i++)
            {
                float vip = eigenVectors[i, p];
                float viq = eigenVectors[i, q];
                eigenVectors[i, p] = c * vip - s * viq;
                eigenVectors[i, q] = s * vip + c * viq;
            }
        }

        int minIndex = 0;
        if (matrix[1, 1] < matrix[minIndex, minIndex])
        {
            minIndex = 1;
        }

        if (matrix[2, 2] < matrix[minIndex, minIndex])
        {
            minIndex = 2;
        }

        return new Vector3(eigenVectors[0, minIndex], eigenVectors[1, minIndex], eigenVectors[2, minIndex]);
    }

    private void ResetInterpolationTracking()
    {
        currentInterpolationAnchorSlotIndex = -1;
        hasSmoothedInterpolationAngle = false;
        interpolationMessage = string.Empty;
    }

    private static Vector2 ProjectToCirclePlane(Vector3 field) => new Vector2(field.x, field.z);

    private void CreateVisualization()
    {
        if (visualizationRoot != null)
        {
            return;
        }

        visualizationRoot = new GameObject("MagnetometerVisualization").transform;
        visualizationRoot.SetParent(transform, false);
        CreateAxisGroup("AbsoluteAxes", new Vector3(-2.75f, 0f, 0f));
        CreateAxisGroup("NormalizedAxes", new Vector3(2.75f, 0f, 0f));
        absoluteVectorLine = CreateVectorLine("AbsoluteVector", new Color(1f, 0.35f, 0.25f), visualizationRoot);
        normalizedVectorLine = CreateVectorLine("NormalizedVector", new Color(0.2f, 0.95f, 0.55f), visualizationRoot);
        absoluteArrowHead = CreateArrowHead("AbsoluteHead", new Color(1f, 0.35f, 0.25f), visualizationRoot);
        normalizedArrowHead = CreateArrowHead("NormalizedHead", new Color(0.2f, 0.95f, 0.55f), visualizationRoot);
    }

    private void RefreshVisualization()
    {
        if (visualizationRoot == null)
        {
            return;
        }

        var absoluteOrigin = new Vector3(-2.75f, 0f, 0f);
        var normalizedOrigin = new Vector3(2.75f, 0f, 0f);
        var clamped = Vector3.ClampMagnitude(magneticField, MaxFieldMagnitude);
        var absoluteDirection = new Vector3(clamped.x, clamped.y, clamped.z) / MaxFieldMagnitude;
        var normalizedDirection = magneticField.sqrMagnitude > Mathf.Epsilon ? magneticField.normalized : Vector3.zero;
        var absoluteTip = absoluteOrigin + absoluteDirection * AbsoluteVectorLength;
        var normalizedTip = normalizedOrigin + normalizedDirection * NormalizedVectorLength;
        SetVectorLine(absoluteVectorLine, absoluteOrigin, absoluteTip);
        SetVectorLine(normalizedVectorLine, normalizedOrigin, normalizedTip);
        UpdateArrowHead(absoluteArrowHead, absoluteOrigin, absoluteTip);
        UpdateArrowHead(normalizedArrowHead, normalizedOrigin, normalizedTip);
    }

    private void EnsureEventSystemUsesInputSystem()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return;
        }

        ConfigureEventSystemForInputSystem(eventSystem);
    }

    private static void ConfigureEventSystemForInputSystem(EventSystem eventSystem)
    {
        if (eventSystem == null)
        {
            return;
        }

        var inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule == null)
        {
            inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        inputSystemModule.enabled = true;
        var legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            legacyModule.enabled = false;
        }
    }

    private void CreateAxisGroup(string name, Vector3 origin)
    {
        var group = new GameObject(name).transform;
        group.SetParent(visualizationRoot, false);
        CreateAxisLine(group, "X", Color.red, origin, Vector3.right * 3f);
        CreateAxisLine(group, "Y", Color.green, origin, Vector3.up * 3f);
        CreateAxisLine(group, "Z", Color.blue, origin, Vector3.forward * 3f);
    }

    private void CreateAxisLine(Transform parent, string axisName, Color color, Vector3 origin, Vector3 direction)
    {
        var axis = CreateVectorLine(axisName, color, parent);
        SetVectorLine(axis, origin - direction, origin + direction);
    }

    private static LineRenderer CreateVectorLine(string name, Color color, Transform parent)
    {
        var lineObject = new GameObject(name);
        lineObject.transform.SetParent(parent, false);
        var line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = 0.075f;
        line.endWidth = 0.035f;
        line.useWorldSpace = true;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 6;
        return line;
    }

    private static Transform CreateArrowHead(string name, Color color, Transform parent)
    {
        var arrowHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        arrowHead.name = name;
        arrowHead.transform.SetParent(parent, false);
        arrowHead.transform.localScale = Vector3.one * 0.18f;
        if (arrowHead.TryGetComponent<Collider>(out var collider))
        {
            Destroy(collider);
        }

        if (arrowHead.TryGetComponent<Renderer>(out var renderer))
        {
            renderer.material.color = color;
        }

        return arrowHead.transform;
    }

    private static void SetVectorLine(LineRenderer line, Vector3 start, Vector3 end)
    {
        if (line == null)
        {
            return;
        }

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private static void UpdateArrowHead(Transform arrowHead, Vector3 origin, Vector3 tip)
    {
        if (arrowHead == null)
        {
            return;
        }

        arrowHead.position = tip;
        arrowHead.localScale = (tip - origin).sqrMagnitude > Mathf.Epsilon ? Vector3.one * 0.18f : Vector3.one * 0.1f;
    }

    private static string Format(float value) => value.ToString("F3", CultureInfo.InvariantCulture);
    private static Vector2 AngleToCirclePosition(float angle, float radius) =>
        new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
}
