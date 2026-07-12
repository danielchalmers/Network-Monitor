namespace Network_Monitor;

/// <summary>
/// How the app chooses between the light and dark themes.
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// Follow the operating system's light/dark setting and accent color.
    /// </summary>
    Auto,

    /// <summary>
    /// Always use the light theme.
    /// </summary>
    Light,

    /// <summary>
    /// Always use the dark theme.
    /// </summary>
    Dark
}
