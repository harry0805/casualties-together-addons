using System;
using System.IO;
using UnityEngine;

namespace CasualtiesTogetherAddons.Features;

internal sealed class VoiceIndicatorSprites : IDisposable
{
    private readonly byte[][] pngs = new byte[10][];
    private readonly Texture2D[] textures = new Texture2D[10];

    internal VoiceIndicatorSprites()
    {
        // Validate/load embedded files before installing patches. Decode textures lazily
        // on the first GUI repaint, on Unity's main thread.
        for (int i = 0; i < pngs.Length; i++)
        {
            string name = i == 9 ? "microphone-muted.png" : i == 0 ? "microphone-idle.png" : $"microphone-level-{i}.png";
            using var stream = typeof(VoiceIndicatorSprites).Assembly.GetManifestResourceStream(
                "CasualtiesTogetherAddons.Assets.VoiceIndicator." + name)
                ?? throw new FileNotFoundException("Missing voice indicator image: " + name);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            pngs[i] = buffer.ToArray();
        }
    }

    internal Texture2D Get(bool open, float fill)
    {
        int frame = open ? Math.Max(0, Math.Min(8, (int)Math.Ceiling(fill * 8))) : 9;
        if (textures[frame] == null)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, pngs[frame], true))
                    throw new InvalidDataException("Could not decode voice indicator image.");
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.hideFlags = HideFlags.HideAndDontSave;
                textures[frame] = texture;
            }
            catch
            {
                UnityEngine.Object.Destroy(texture);
                throw;
            }
        }
        return textures[frame];
    }

    public void Dispose()
    {
        for (int i = 0; i < textures.Length; i++)
        {
            // Reference comparison also lets non-Unity fixture tests dispose unloaded assets.
            if (ReferenceEquals(textures[i], null)) continue;
            UnityEngine.Object.Destroy(textures[i]);
            textures[i] = null;
        }
    }
}
