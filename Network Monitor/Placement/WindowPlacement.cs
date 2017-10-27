using System;
using System.Runtime.InteropServices;

namespace Network_Monitor.Placement
{
    /// <summary>
    /// Contains information about the placement of a window on the screen.
    /// </summary>
    // https://github.com/danielchalmers/WpfWindowPlacement.
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPlacement
    {
        /// <summary>
        /// <para>The length of the structure, in bytes.</para>
        /// <para>This is automatically set in <see cref="WindowPlacementFunctions"/> methods.</para>
        /// </summary>
        public int Length;

        /// <summary>
        /// The flags that control the position of the minimized window and the method by which the window is restored.
        /// </summary>
        public WindowPlacementFlags Flags;

        /// <summary>
        /// The current show state of the window.
        /// </summary>
        public WindowPlacementShowCommand ShowCommand;

        /// <summary>
        /// The coordinates of the window's upper-left corner when the window is minimized.
        /// </summary>
        public Point MinimizedPosition;

        /// <summary>
        /// The coordinates of the window's upper-left corner when the window is maximized.
        /// </summary>
        public Point MaximizedPosition;

        /// <summary>
        /// The window's bounds when the window is in the restored position.
        /// </summary>
        public Rectangle NormalBounds;
    }
}