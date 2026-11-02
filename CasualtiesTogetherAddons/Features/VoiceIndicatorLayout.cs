using System;
using UnityEngine;

namespace CasualtiesTogetherAddons.Features;

internal static class VoiceIndicatorLayout
{
    internal static Rect Place(float width, float height, int pixel, float scale, bool inWorld, float originalY)
    {
        float margin = Math.Max(pixel * 2, 32 * scale);
        // Anchor beside the second bottom-left HUD button, above the heart.
        var rect = new Rect(inWorld ? 150 * scale - 8 * pixel : 20,
            inWorld ? height - 140 * scale - 10 * pixel : originalY, 16 * pixel, 20 * pixel);
        rect.x = (float)Math.Round(Math.Max(margin, Math.Min(width - margin - rect.width, rect.x)));
        rect.y = (float)Math.Round(Math.Max(inWorld ? margin : pixel, Math.Min(height - margin - rect.height, rect.y)));
        return rect;
    }
}
