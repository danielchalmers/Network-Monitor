namespace Network_Monitor.Placement
{
    /// <summary>
    /// The current show state of the window.
    /// </summary>
    // https://github.com/danielchalmers/WpfWindowPlacement.
    public enum WindowPlacementShowCommand : uint
    {
        /// <summary>
        /// Hides the window and activates another window.
        /// </summary>
        Hide = 0,

        /// <summary>
        /// Maximizes the specified window.
        /// </summary>
        Maximize = 3,

        /// <summary>
        /// Minimize the specified window and activate the next top-level window in the z-order.
        /// </summary>
        Minimize = 6,

        /// <summary>
        /// <para>Activates and displays the window.</para>
        /// <para>If the window is minimized or maximized, the system restores it to its original size and position.</para>
        /// <para>An application should specify this flag when restoring a minimized window.</para>
        /// </summary>
        Restore = 9,

        /// <summary>
        /// Activates the window and displays it in its current size and position.
        /// </summary>
        Show = 5,

        /// <summary>
        /// Activates the window and displays it as a maximized window.
        /// </summary>
        ShowMaximized = 3,

        /// <summary>
        /// Activates the window and displays it as a minimized window.
        /// </summary>
        ShowMinimized = 2,

        /// <summary>
        /// <para>Displays the window as a minimized window.</para>
        /// <para>This value is relative to <see cref="ShowMinimized"/>, except the window is not activated.</para>
        /// </summary>
        ShowMinimizedNoActivate = 7,

        /// <summary>
        /// <para>Displays the window in its current size and position.</para>
        /// <para>This value is similar to <see cref="Show"/>, except the window is not activated.</para>
        /// </summary>
        ShowNA = 8,

        /// <summary>
        /// <para>Displays a window in its most recent size and position.</para>
        /// <para>This value is only valid for <see cref="ShowNormal"/>, except the window is not activated.</para>
        /// </summary>
        ShowNoActivate = 4,

        /// <summary>
        /// <para>Activates and displays a window.</para>
        /// <para>If the window is minimized or maximized, the system restores it to its original size and position.</para>
        /// <para>An application should specify this flag when displaying the window for the first time.</para>
        /// </summary>
        ShowNormal = 1,
    }
}