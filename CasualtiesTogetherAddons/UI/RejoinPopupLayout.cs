using System;
using UnityEngine;

namespace CasualtiesTogetherAddons.UI;

internal readonly struct RejoinPopupLayout
{
    public readonly float Scale;
    public readonly Rect Window;

    private RejoinPopupLayout(float scale, Rect window) { Scale = scale; Window = window; }

    // Content is measured in the native skin's coordinates. Fit the entire dialog,
    // including fonts and borders, instead of clamping its dimensions independently.
    public static RejoinPopupLayout Fit(float width, float height, int screenWidth, int screenHeight)
    {
        float scale = Math.Min(1f, Math.Min(Math.Max(1, screenWidth - 24) / width,
            Math.Max(1, screenHeight - 24) / height));
        return new RejoinPopupLayout(scale, new Rect((screenWidth / scale - width) * 0.5f,
            (screenHeight / scale - height) * 0.5f, width, height));
    }
}
