using System;
using System.Diagnostics;
using System.IO;
using AgentWpf.Application.Ports;
using AgentWpf.Domain;

namespace AgentWpf.Infrastructure.FlaUI;

/// <summary>
/// Captures windows with <c>PrintWindow(PW_RENDERFULLCONTENT)</c>, which works for covered windows
/// and needs no focus, and saves them as PNG.
/// </summary>
public sealed class PrintWindowCapture : IScreenCapture
{
    /// <inheritdoc />
    public (int Width, int Height) SavePng(nint windowHandle, Bounds? crop, string path)
    {
        Debug.Assert(windowHandle != 0, "Precondition: window handle must be valid.");
        Debug.Assert(Path.IsPathRooted(path), "Precondition: path must be absolute.");

        if (!NativeMethods.GetWindowRect(windowHandle, out var rect))
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, "The window is gone.", "run windows to list open windows");
        }

        var window = new Bounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        var pixels = Grab(windowHandle, window.Width, window.Height);
        var area = crop is Bounds c ? Intersect(window, c) : window;
        if (area.IsEmpty)
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, "The element is outside the window.", "scroll it into view first");
        }

        var cropped = Cut(pixels, window, area);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, PngEncoder.Encode(cropped, area.Width, area.Height));

        Debug.Assert(File.Exists(path), "Postcondition: the file must exist.");
        return (area.Width, area.Height);
    }

    private static unsafe byte[] Grab(nint hwnd, int width, int height)
    {
        var screen = NativeMethods.GetDC(0);
        var memory = NativeMethods.CreateCompatibleDC(screen);
        var bitmap = NativeMethods.CreateCompatibleBitmap(screen, width, height);
        var previous = NativeMethods.SelectObject(memory, bitmap);
        try
        {
            if (!NativeMethods.PrintWindow(hwnd, memory, NativeMethods.PrintWindowRenderFullContent))
            {
                throw new AgentWpfException(ErrorCode.ActionFailed, "PrintWindow failed for this window.", "the window may be minimized; restore it first");
            }

            NativeMethods.SelectObject(memory, previous);
            var header = new NativeMethods.BitmapInfoHeader
            {
                Size = (uint)sizeof(NativeMethods.BitmapInfoHeader),
                Width = width,
                Height = -height, // negative height: top-down rows
                Planes = 1,
                BitCount = 32,
            };
            var pixels = new byte[width * height * 4];
            fixed (byte* p = pixels)
            {
                var lines = NativeMethods.GetDIBits(memory, bitmap, 0, (uint)height, p, ref header, NativeMethods.DibRgbColors);
                if (lines != height)
                {
                    throw new AgentWpfException(ErrorCode.ActionFailed, "Reading the captured pixels failed.", null);
                }
            }

            return pixels;
        }
        finally
        {
            NativeMethods.DeleteObject(bitmap);
            NativeMethods.DeleteDC(memory);
            _ = NativeMethods.ReleaseDC(0, screen);
        }
    }

    private static Bounds Intersect(Bounds a, Bounds b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return new Bounds(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static byte[] Cut(byte[] pixels, Bounds window, Bounds area)
    {
        if (area == window)
        {
            return pixels;
        }

        var result = new byte[area.Width * area.Height * 4];
        for (var y = 0; y < area.Height; y++)
        {
            var source = (((area.Y - window.Y + y) * window.Width) + (area.X - window.X)) * 4;
            Buffer.BlockCopy(pixels, source, result, y * area.Width * 4, area.Width * 4);
        }

        return result;
    }
}
