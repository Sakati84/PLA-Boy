using System.Collections.Generic;
using Playdate.MagnetVectors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Playdate.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RuntimeTuningMenu : MonoBehaviour
    {
        [Header("References")]
        public GameSessionController sessionController;
        public FlappyPlayerController playerController;
        public MagnetAngleTracker magnetAngleTracker;
        public Transform inGameUiRoot;

        private readonly List<RuntimeTuningBinding> bindings = new();
        private readonly List<RuntimeTuningRow> rows = new();

        private GameObject openButtonObject;
        private Button openButton;
        private GameObject overlayRoot;
        private Button closeButton;
        private Button resetButton;
        private bool uiBuilt;
        private bool menuOpen;
        private float previousTimeScale = 1f;

        private void Awake()
        {
            ResolveReferences();
            BuildBindings();
            EnsureUi();
            RefreshRows();
            RefreshButtonVisibility();
        }

        private void Update()
        {
            RefreshButtonVisibility();
            if (menuOpen)
            {
                RefreshRows();
            }
        }

        private void OnDisable()
        {
            ForceCloseMenu();
        }

        private void OnDestroy()
        {
            ForceCloseMenu();
        }

        public void OpenMenu()
        {
            if (menuOpen || sessionController == null || sessionController.CurrentState != GameSessionState.Playing)
            {
                return;
            }

            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            menuOpen = true;

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(true);
            }

            RefreshRows();
            RefreshButtonVisibility();
        }

        public void CloseMenu()
        {
            if (!menuOpen)
            {
                return;
            }

            menuOpen = false;
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            RefreshButtonVisibility();
        }

        public void ResetToDefaults()
        {
            foreach (RuntimeTuningBinding binding in bindings)
            {
                binding.Reset();
            }

            RefreshRows();
        }

        private void ResolveReferences()
        {
            if (sessionController == null)
            {
                sessionController = FindAnyObjectByType<GameSessionController>();
            }

            if (playerController == null && sessionController != null)
            {
                playerController = sessionController.playerController;
            }

            if (playerController == null)
            {
                playerController = FindAnyObjectByType<FlappyPlayerController>();
            }

            if (magnetAngleTracker == null && playerController != null)
            {
                magnetAngleTracker = playerController.magnetAngleTracker;
            }

            if (magnetAngleTracker == null)
            {
                magnetAngleTracker = FindAnyObjectByType<MagnetAngleTracker>();
            }

            if (inGameUiRoot == null)
            {
                Transform inGame = transform.Find("InGame");
                inGameUiRoot = inGame != null ? inGame : transform;
            }
        }

        private void BuildBindings()
        {
            bindings.Clear();
            if (playerController == null || magnetAngleTracker == null)
            {
                return;
            }

            bindings.Add(new RuntimeTuningBinding(
                "Base Gravity",
                () => playerController.baseGravity,
                value => playerController.baseGravity = value,
                playerController.baseGravity));

            bindings.Add(new RuntimeTuningBinding(
                "Vertical Damping",
                () => playerController.verticalDamping,
                value => playerController.verticalDamping = value,
                playerController.verticalDamping));

            bindings.Add(new RuntimeTuningBinding(
                "Lift Acceleration",
                () => playerController.liftAcceleration,
                value => playerController.liftAcceleration = value,
                playerController.liftAcceleration));

            bindings.Add(new RuntimeTuningBinding(
                "Dive Acceleration",
                () => playerController.diveAcceleration,
                value => playerController.diveAcceleration = value,
                playerController.diveAcceleration));

            bindings.Add(new RuntimeTuningBinding(
                "Angle Deadzone",
                () => playerController.angleDeltaDeadzone,
                value => playerController.angleDeltaDeadzone = value,
                playerController.angleDeltaDeadzone));

            bindings.Add(new RuntimeTuningBinding(
                "Max Rise Speed",
                () => playerController.maxRiseSpeed,
                value => playerController.maxRiseSpeed = value,
                playerController.maxRiseSpeed));

            bindings.Add(new RuntimeTuningBinding(
                "Max Fall Speed",
                () => playerController.maxFallSpeed,
                value => playerController.maxFallSpeed = value,
                playerController.maxFallSpeed));

            bindings.Add(new RuntimeTuningBinding(
                "Angle Smoothing",
                () => magnetAngleTracker.AngleSmoothing,
                value => magnetAngleTracker.AngleSmoothing = value,
                magnetAngleTracker.AngleSmoothing));

            bindings.Add(new RuntimeTuningBinding(
                "Wheel Multiplier",
                () => magnetAngleTracker.WindowsDebugWheelMultiplier,
                value => magnetAngleTracker.WindowsDebugWheelMultiplier = value,
                magnetAngleTracker.WindowsDebugWheelMultiplier));
        }

        private void EnsureUi()
        {
            if (uiBuilt)
            {
                return;
            }

            openButton = CreateOpenButton();
            overlayRoot = CreateOverlayRoot();
            uiBuilt = true;
        }

        private Button CreateOpenButton()
        {
            openButtonObject = new GameObject("RuntimeTuningButton", typeof(RectTransform), typeof(Image), typeof(Button));
            openButtonObject.transform.SetParent(inGameUiRoot, false);

            RectTransform rectTransform = openButtonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.sizeDelta = new Vector2(180f, 52f);
            rectTransform.anchoredPosition = new Vector2(-24f, -24f);

            Image image = openButtonObject.GetComponent<Image>();
            image.color = new Color(0.11f, 0.32f, 0.45f, 0.92f);

            Button button = openButtonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OpenMenu);

            CreateText(
                "Label",
                openButtonObject.transform,
                "Flight Tuning",
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                Color.white,
                true);

            return button;
        }

        private GameObject CreateOverlayRoot()
        {
            GameObject overlay = new GameObject("RuntimeTuningOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(transform, false);

            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0.04f, 0.08f, 0.12f, 0.82f);

            GameObject panel = new GameObject(
                "Panel",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            panel.transform.SetParent(overlay.transform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 0f);

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.94f, 0.97f, 0.99f, 0.98f);

            VerticalLayoutGroup panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(28, 28, 28, 28);
            panelLayout.spacing = 18f;
            panelLayout.childControlHeight = true;
            panelLayout.childControlWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.childForceExpandWidth = true;

            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateText(
                "Title",
                panel.transform,
                "Flight Tuning",
                34f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.09f, 0.17f, 0.24f),
                false);

            CreateText(
                "Hint",
                panel.transform,
                "Each click changes the value by 5% of its start value.",
                20f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Color(0.27f, 0.35f, 0.42f),
                false);

            GameObject rowsRoot = new GameObject(
                "Rows",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            rowsRoot.transform.SetParent(panel.transform, false);

            VerticalLayoutGroup rowsLayout = rowsRoot.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 10f;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandHeight = false;
            rowsLayout.childForceExpandWidth = true;

            ContentSizeFitter rowsFitter = rowsRoot.GetComponent<ContentSizeFitter>();
            rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (RuntimeTuningBinding binding in bindings)
            {
                RuntimeTuningRow row = CreateRow(rowsRoot.transform, binding);
                rows.Add(row);
            }

            GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            footer.transform.SetParent(panel.transform, false);

            HorizontalLayoutGroup footerLayout = footer.GetComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 16f;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlHeight = true;
            footerLayout.childControlWidth = false;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;

            resetButton = CreatePanelButton(footer.transform, "Reset Defaults", new Color(0.65f, 0.32f, 0.09f, 0.95f));
            resetButton.onClick.AddListener(ResetToDefaults);

            closeButton = CreatePanelButton(footer.transform, "Close", new Color(0.11f, 0.41f, 0.25f, 0.95f));
            closeButton.onClick.AddListener(CloseMenu);

            overlay.SetActive(false);
            return overlay;
        }

        private RuntimeTuningRow CreateRow(Transform parent, RuntimeTuningBinding binding)
        {
            GameObject rowObject = new GameObject(
                binding.DisplayName.Replace(" ", string.Empty) + "Row",
                typeof(RectTransform),
                typeof(Image),
                typeof(HorizontalLayoutGroup),
                typeof(RuntimeTuningRow));
            rowObject.transform.SetParent(parent, false);

            Image rowImage = rowObject.GetComponent<Image>();
            rowImage.color = new Color(0.84f, 0.9f, 0.95f, 0.55f);

            HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            TextMeshProUGUI nameLabel = CreateText(
                "Name",
                rowObject.transform,
                binding.DisplayName,
                23f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                new Color(0.1f, 0.15f, 0.22f),
                false);
            SetPreferredWidth(nameLabel.rectTransform, 280f);

            Button decrease = CreatePanelButton(rowObject.transform, "-", new Color(0.67f, 0.21f, 0.16f, 0.95f), 58f);

            TextMeshProUGUI valueLabel = CreateText(
                "Value",
                rowObject.transform,
                string.Empty,
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.1f, 0.15f, 0.22f),
                false);
            SetPreferredWidth(valueLabel.rectTransform, 128f);

            Button increase = CreatePanelButton(rowObject.transform, "+", new Color(0.16f, 0.44f, 0.2f, 0.95f), 58f);

            RuntimeTuningRow row = rowObject.GetComponent<RuntimeTuningRow>();
            row.nameLabel = nameLabel;
            row.valueLabel = valueLabel;
            row.decreaseButton = decrease;
            row.increaseButton = increase;
            row.Bind(binding);
            return row;
        }

        private Button CreatePanelButton(Transform parent, string label, Color backgroundColor, float width = 170f)
        {
            GameObject buttonObject = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(width, 52f);

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 52f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            CreateText(
                "Label",
                buttonObject.transform,
                label,
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                Color.white,
                true);

            return button;
        }

        private TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string value,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment,
            Color color,
            bool stretch)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = false;

            RectTransform rectTransform = text.rectTransform;
            if (stretch)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }
            else
            {
                LayoutElement layout = textObject.AddComponent<LayoutElement>();
                layout.preferredHeight = fontSize + 12f;
            }

            return text;
        }

        private void RefreshRows()
        {
            foreach (RuntimeTuningRow row in rows)
            {
                row?.Refresh();
            }
        }

        private void RefreshButtonVisibility()
        {
            if (openButtonObject == null || sessionController == null)
            {
                return;
            }

            bool shouldBeVisible = !menuOpen && sessionController.CurrentState == GameSessionState.Playing;
            if (openButtonObject.activeSelf != shouldBeVisible)
            {
                openButtonObject.SetActive(shouldBeVisible);
            }
        }

        private void ForceCloseMenu()
        {
            if (!menuOpen)
            {
                return;
            }

            menuOpen = false;
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }

        private static void SetPreferredWidth(RectTransform rectTransform, float width)
        {
            if (rectTransform == null)
            {
                return;
            }

            LayoutElement layout = rectTransform.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
        }
    }
}
