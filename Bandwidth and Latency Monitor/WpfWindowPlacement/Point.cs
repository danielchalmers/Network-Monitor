using System;
using System.Runtime.InteropServices;

namespace WpfWindowPlacement;

/// <summary>
/// Defines the x- and y- coordinates of a point.
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct Point
{
    /// <summary>
    /// The x-coordinate of the point.
    /// </summary>
    public int X;

    /// <summary>
    /// The y-coordinate of the point.
    /// </summary>
    public int Y;

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}