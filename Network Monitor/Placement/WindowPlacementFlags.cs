using System;

namespace Network_Monitor.Placement;

/// <summary>
/// The flags that control the position of the minimized window and the method by which the window is restored.
/// </summary>
[Flags]
// https://github.com/danielchalmers/WpfWindowPlacement.
public enum WindowPlacementFlags : uint
{
    /// <summary>
    /// <para>If the calling thread and the thread that owns the window are attached to different input queues, the system posts the request to the thread that owns the window.</para>
    /// <para>This prevents the calling thread from blocking its execution while other threads process the request.</para>
    /// </summary>
    AsyncWindowPlacement = 0x0004,

    /// <summary>
    /// <para>The restored window will be maximized, regardless of whether it is maximized or minimized.</para>
    /// <para>This setting is only valid next time the window is restored. It does not change the default restoration behavior.</para>
    /// </summary>
    RestoreToMaximized = 0x0002,

    /// <summary>
    /// <para>The coordinates of the minimized window may be specified.</para>
    /// <para>This flag must be specified in the <see cref="WindowPlacement.MinimizedPosition"/> member.</para>
    /// </summary>
    SetMinimizedPosition = 0x0001,
}