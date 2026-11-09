using System;
using Together;
using UnityEngine;
using CasualtiesTogetherAddons.Networking;
using CasualtiesTogetherUtils;

namespace CasualtiesTogetherAddons.UI;

// Owns presentation only. The reconnect controller owns all network operations.
internal sealed class RejoinPopup
{
    private readonly Action applyMenuSkin;
    public bool IsOpen { get; private set; }
    private string error;

    public RejoinPopup(Action applyMenuSkin) => this.applyMenuSkin = applyMenuSkin;
    public void ShowProgress() { error = null; IsOpen = true; }
    public void ShowError(string message) { error = message; IsOpen = true; }
    public void Close() { error = null; IsOpen = false; }

    public void Draw(RejoinPhase phase, Action cancel)
    {
        if (!IsOpen || Screen.width <= 0 || Screen.height <= 0) return;
        int depth = GUI.depth;
        bool enabled = GUI.enabled;
        var matrix = GUI.matrix;
        var skin = GUI.skin;
        var color = GUI.color;
        var contentColor = GUI.contentColor;
        var backgroundColor = GUI.backgroundColor;
        try
        {
            GUI.depth = -10000;
            GUI.enabled = true;
            GUI.color = GUI.contentColor = GUI.backgroundColor = Color.white;
            applyMenuSkin();
            float nativeScale = Mathf.Max(0.1f, UIMainMenu.GetMenuUIScale());
            float padding = Mathf.Max(12f * nativeScale, UIBullshit.uiBlockSmallBorderSize);
            var title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = Mathf.RoundToInt(GUI.skin.label.fontSize * 1.25f)
            };
            var label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            label.normal.textColor = error == null ? new Color(0.75f, 0.75f, 0.75f) : new Color(1f, 0.35f, 0.35f);
            string message = error ?? ProgressText(phase) + new string('.', 1 + (int)(Time.realtimeSinceStartup * 2f) % 3);
            string action = error == null ? "Cancel" : "Close";
            float width = Mathf.Max(420f * nativeScale, 2f * padding + 240f * nativeScale);
            float innerWidth = width - 2f * padding;
            float titleHeight = title.CalcHeight(new GUIContent("Rejoin server"), innerWidth);
            float textHeight = Mathf.Max(40f * nativeScale, label.CalcHeight(new GUIContent(message), innerWidth));
            float buttonHeight = Mathf.Max(32f * nativeScale, GUI.skin.button.CalcHeight(new GUIContent(action), innerWidth));
            float height = 2f * padding + titleHeight + textHeight + buttonHeight + 16f * nativeScale;
            var layout = RejoinPopupLayout.Fit(width, height, Screen.width, Screen.height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(layout.Scale, layout.Scale, 1f));
            // The mod's overlap helper expects screen coordinates, not window-local coordinates.
            UIBullshit.CheckCursorOverlap(new Rect(layout.Window.x * layout.Scale, layout.Window.y * layout.Scale,
                width * layout.Scale, height * layout.Scale));
            GUI.ModalWindow(0x4354524a, layout.Window, _ =>
            {
                UIBullshit._GUI_9SlicePanel(new Rect(0, 0, width, height), 1f, 0.6980392f, false,
                    UIBullshit.unscaled_uiBlockNano);
                float y = padding;
                GUI.Label(new Rect(padding, y, innerWidth, titleHeight), "Rejoin server", title);
                y += titleHeight + 6f * nativeScale;
                GUI.Label(new Rect(padding, y, innerWidth, textHeight), message, label);
                y += textHeight + 10f * nativeScale;
                if (GUI.Button(new Rect(padding, y, innerWidth, buttonHeight), action, GUI.skin.button))
                {
                    Util.PlayUISound(PlayerCamera.UISoundType.MiniClick);
                    if (error == null) cancel();
                    else Close();
                }
            }, "", GUIStyle.none);
        }
        finally
        {
            GUI.depth = depth;
            GUI.enabled = enabled;
            GUI.matrix = matrix;
            GUI.skin = skin;
            GUI.color = color;
            GUI.contentColor = contentColor;
            GUI.backgroundColor = backgroundColor;
        }
    }

    private static string ProgressText(RejoinPhase phase) => phase switch
    {
        RejoinPhase.Queued => "Starting rejoin",
        RejoinPhase.WaitingForMenu => "Reconnecting",
        RejoinPhase.Connecting => "Connecting",
        RejoinPhase.Loading => "Loading world",
        _ => "Rejoining"
    };
}
