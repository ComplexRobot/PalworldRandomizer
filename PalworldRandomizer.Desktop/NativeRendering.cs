using Avalonia;

namespace PalworldRandomizer.Desktop;

/// <summary>The public native option groups selected from the saved rendering preference.</summary>
/// <param name="Win32">Windows options; GPU preferred with an ordered software fallback.</param>
/// <param name="X11">Linux options; GPU preferred with an ordered software fallback.</param>
/// <param name="AvaloniaNative">macOS options; GPU preferred with an ordered software fallback.</param>
public sealed record NativeRenderingOptions(
    Win32PlatformOptions Win32,
    X11PlatformOptions X11,
    AvaloniaNativePlatformOptions AvaloniaNative);

/// <summary>Maps the saved renderer choice onto Avalonia's public platform option types.</summary>
public static class NativeRendering {
    /// <summary>The saved preference is either an explicit software request or the GPU-preferred default.</summary>
    public static NativeRenderingOptions OptionsFor(string renderer) {
        bool software = string.Equals(renderer, "software", StringComparison.Ordinal);
        return new NativeRenderingOptions(
            new Win32PlatformOptions {
                RenderingMode = software
                    ? [Win32RenderingMode.Software]
                    : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
            },
            new X11PlatformOptions {
                RenderingMode = software
                    ? [X11RenderingMode.Software]
                    : [X11RenderingMode.Glx, X11RenderingMode.Software],
            },
            new AvaloniaNativePlatformOptions {
                RenderingMode = software
                    ? [AvaloniaNativeRenderingMode.Software]
                    : [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software],
            });
    }
}
