using System;
using System.Collections.Generic;
using BepInEx.Logging;
using CasualtiesTogetherAddons.Features;
using HarmonyLib;
using TMPro;
using Together;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesTogetherAddons.UI;

// Use Together's native controls, inserted immediately after its deactivation button.
internal sealed class MultiplayerSettings : IDisposable
{
    private static MultiplayerSettings instance;
    private readonly FeatureModule[] features;
    private readonly ManualLogSource logger;
    private readonly Harmony harmony = new Harmony(Plugin.Id + ".Settings");
    private readonly List<GameObject> rows = new List<GameObject>();
    private readonly Dictionary<FeatureModule, GameObject> featureRows = new Dictionary<FeatureModule, GameObject>();
    private readonly Dictionary<FeatureModule, GameObject> restartRows = new Dictionary<FeatureModule, GameObject>();
    private readonly Dictionary<RectTransform, Vector2> originalPositions = new Dictionary<RectTransform, Vector2>();
    private float baseHeight;
    private float insertionTop;
    private static readonly System.Reflection.FieldInfo deactivateField =
        AccessTools.DeclaredField(typeof(SettingsMenu_Start_MultiplayerPatch), "DeactivateButton");

    public MultiplayerSettings(FeatureModule[] features, ManualLogSource logger)
    {
        this.features = features;
        this.logger = logger;
    }

    public void Initialize()
    {
        try
        {
            var build = AccessTools.DeclaredMethod(typeof(SettingsMenu_Start_MultiplayerPatch), "BuildMPSettingsUI", Type.EmptyTypes);
            if (build == null || !build.IsStatic || build.ReturnType != typeof(void) || deactivateField == null)
                throw new MissingMethodException("Multiplayer settings menu is not compatible with this add-on.");
            instance = this;
            harmony.Patch(build, postfix: new HarmonyMethod(typeof(MultiplayerSettings), nameof(AfterBuild)));
        }
        catch (Exception error)
        {
            Dispose();
            logger.LogError($"Could not integrate addon settings. {error}");
        }
    }

    private static void AfterBuild() => instance?.InsertControls();

    private void InsertControls()
    {
        float originalHeight = SettingsMenu_Start_MultiplayerPatch.contentHeight;

        try
        {
            ClearRows();
            var deactivate = deactivateField.GetValue(null) as GameObject;
            if (deactivate == null) throw new MissingReferenceException("Multiplayer deactivation button is unavailable.");
            var anchor = deactivate.GetComponent<RectTransform>();
            insertionTop = -anchor.anchoredPosition.y + anchor.sizeDelta.y * 0.5f + 16f;
            // Snapshot the original rows before our helpers register additional objects.
            var originalRows = new List<GameObject>(SettingsMenu_Start_MultiplayerPatch.spawnedSettings);
            float height = insertionTop;
            AddHeading(ref height);
            foreach (var feature in features)
            {
                var row = global::Utils.Create("Special/GameSettingBool", SettingsMenu_Start_MultiplayerPatch.content);
                rows.Add(row);
                featureRows.Add(feature, row);
                row.name = Plugin.Id + "." + feature.Id;
                var toggle = row.transform.GetChild(1).GetComponent<Toggle>();
                if (toggle == null) throw new MissingComponentException("GameSettingBool has no Toggle.");
                toggle.onValueChanged.RemoveAllListeners();
                toggle.SetIsOnWithoutNotify(feature.Enabled.Value);
                SettingsMenu_Start_MultiplayerPatch.SetupSetting(row, feature.DisplayName, feature.Summary, ref height);
                toggle.onValueChanged.AddListener(value =>
                {
                    FeatureSettingsActions.SetEnabled(feature, value);
                    RefreshLabels();
                    LayoutControls();
                });
                SettingsMenu_Start_MultiplayerPatch.CreateSettingButton("Restart Game", ref height,
                    out var restartRow, out var restartButton, out var restartText);
                rows.Add(restartRow);
                restartRows.Add(feature, restartRow);
                restartRow.name = Plugin.Id + "." + feature.Id + ".Restart";
                if (restartButton == null) throw new MissingComponentException("Restart row has no Button.");
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(() => FeatureSettingsActions.Restart(feature));
                // A compact right-aligned button directly below its own feature checkbox.
                var restartRect = restartRow.GetComponent<RectTransform>();
                restartRect.anchorMin = new Vector2(1, restartRect.anchorMin.y);
                restartRect.anchorMax = new Vector2(1, restartRect.anchorMax.y);
                restartRect.pivot = new Vector2(1, restartRect.pivot.y);
                restartRect.sizeDelta = new Vector2(240, restartRect.sizeDelta.y);
                restartRect.anchoredPosition = new Vector2(-12, restartRect.anchoredPosition.y);
            }
            RefreshLabels();
            baseHeight = originalHeight;
            foreach (var row in originalRows)
            {
                if (row == null) continue;
                var rect = row.GetComponent<RectTransform>();
                if (rect == null || row == deactivate) continue;
                float top = -rect.anchoredPosition.y - rect.sizeDelta.y * 0.5f;
                if (top < insertionTop - 0.5f) continue;
                originalPositions.Add(rect, rect.anchoredPosition);
            }
            LayoutControls();
        }
        catch (Exception error)
        {
            foreach (var pair in originalPositions)
                if (pair.Key != null) pair.Key.anchoredPosition = pair.Value;
            ClearRows();
            SetContentHeight(originalHeight);
            logger.LogError($"Could not build addon settings section. {error}");
        }
    }

    private void AddHeading(ref float height)
    {
        // Match the original General / User Interface headings rather than a centered button.
        SettingsMenu_Start_MultiplayerPatch.CreateSettingButton("     " + PluginInfo.Name, ref height,
            out var row, out var button, out var text);
        rows.Add(row);
        row.name = Plugin.Id + ".Heading";
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            Object.Destroy(button);
        }
        if (text == null) throw new MissingComponentException("Settings heading has no text component.");
        text.fontSize += 10;
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        text.color = Color.cyan;
        var image = row.GetComponent<Image>();
        if (image != null) image.color = Color.cyan;
    }

    private void RefreshLabels()
    {
        foreach (var pair in featureRows)
        {
            if (pair.Value == null) continue;
            string title = FeatureSettingsActions.Title(pair.Key);
            SettingsMenu_Start_MultiplayerPatch.SetSettingNameAndTooltip(pair.Value, title, pair.Key.Summary);
        }
    }

    private void LayoutControls()
    {
        float height = insertionTop;
        foreach (var row in rows)
        {
            if (row == null) continue;
            bool visible = true;
            foreach (var pair in restartRows)
                if (pair.Value == row) { visible = pair.Key.RestartRequired; break; }
            row.SetActive(visible);
            if (!visible) continue;
            var rect = row.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -height - rect.sizeDelta.y * 0.5f);
            height += rect.sizeDelta.y;
        }
        float addedHeight = height - insertionTop;
        foreach (var pair in originalPositions)
            if (pair.Key != null) pair.Key.anchoredPosition = pair.Value - new Vector2(0, addedHeight);
        SetContentHeight(baseHeight + addedHeight);
    }

    private static void SetContentHeight(float height)
    {
        SettingsMenu_Start_MultiplayerPatch.contentHeight = height;
        var content = SettingsMenu_Start_MultiplayerPatch.content;
        if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x, height);
    }

    private void ClearRows()
    {
        foreach (var row in rows)
        {
            if (row == null) continue;
            if (SettingsMenu.instance != null) SettingsMenu_Start_MultiplayerPatch.spawnedSettings.Remove(row);
            row.SetActive(false);
            Object.Destroy(row);
        }
        rows.Clear();
        featureRows.Clear();
        restartRows.Clear();
        originalPositions.Clear();
    }

    public void Dispose()
    {
        if (instance == this) instance = null;
        harmony.UnpatchSelf();
        ClearRows();
    }
}
