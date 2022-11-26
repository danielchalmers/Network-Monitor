using System;
using System.Runtime.InteropServices;

namespace WpfWindowPlacement;

/// <summary>
/// Defines the coordinates of the upper-left and lower-right corners of a rectangle.
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct Rectangle
{
    /// <summary>
    /// The x-coordinate of the upper-left corner of the rectangle.
    /// </summary>
    public int Left;

    /// <summary>
    /// The y-coordinate of the upper-left corner of the rectangle.
    /// </summary>
    public int Top;

    /// <summary>
    /// The x-coordinate of the lower-right corner of the rectangle.
    /// </summary>
    public int Right;

    /// <summary>
    /// The y-coordinate of the lower-right corner of the rectangle.
    /// </summary>
    public int Bottom;

    public Rectangle(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}