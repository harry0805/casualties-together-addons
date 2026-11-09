using System;
using CasualtiesTogetherAddons.UI;

internal static class RejoinPopupLayoutTests
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            checks++;
            Console.WriteLine("PASS: " + description);
        }
        // Measured native content, including button text/padding, must remain inside
        // the dialog at high UI scales and on a window smaller than the native dialog.
        foreach (var sample in new[] {
            (Width: 420f, Height: 180f, ScreenW: 1920, ScreenH: 1080),
            (Width: 840f, Height: 380f, ScreenW: 1920, ScreenH: 1080),
            (Width: 1680f, Height: 760f, ScreenW: 1280, ScreenH: 720),
            (Width: 840f, Height: 380f, ScreenW: 320, ScreenH: 200) })
        {
            var layout = RejoinPopupLayout.Fit(sample.Width, sample.Height, sample.ScreenW, sample.ScreenH);
            Check(layout.Scale > 0 && layout.Scale <= 1, "Dialog fit preserves or uniformly reduces the native scale");
            Check(layout.Window.width == sample.Width && layout.Window.height == sample.Height,
                "Measured native content is not clipped to a fixed dialog height");
            float left = layout.Window.x * layout.Scale;
            float top = layout.Window.y * layout.Scale;
            float right = layout.Window.xMax * layout.Scale;
            float bottom = layout.Window.yMax * layout.Scale;
            Check(left >= 11.99f && top >= 11.99f && right <= sample.ScreenW - 11.99f && bottom <= sample.ScreenH - 11.99f,
                "Whole dialog, including its bottom action, fits within screen margins");
            Check(Math.Abs((left + right) * 0.5f - sample.ScreenW * 0.5f) < 0.01f
                && Math.Abs((top + bottom) * 0.5f - sample.ScreenH * 0.5f) < 0.01f,
                "Dialog stays centered when the complete native layout is scaled");
        }
        Console.WriteLine("All " + checks + " popup layout checks passed.");
    }
}
