using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>Palette: near-black, thin grey lines and one lavender accent. Orange only for warnings.</summary>
[SupportedOSPlatform("windows")]
internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(9, 9, 11);           // window
    public static readonly Color Card = Color.FromArgb(15, 15, 18);       // panels
    public static readonly Color CardEdge = Color.FromArgb(42, 42, 49);   // panel border
    public static readonly Color Panel = Color.FromArgb(19, 19, 23);      // buttons
    public static readonly Color PanelHover = Color.FromArgb(26, 26, 32);
    public static readonly Color Field = Color.FromArgb(10, 10, 13);      // text fields
    public static readonly Color Line = Color.FromArgb(58, 58, 68);       // brighter border
    public static readonly Color Ink = Color.FromArgb(236, 236, 241);
    public static readonly Color Muted = Color.FromArgb(133, 133, 143);
    public static readonly Color Dim = Color.FromArgb(85, 85, 94);
    public static readonly Color Accent = Color.FromArgb(185, 168, 255);  // the one accent
    public static readonly Color AccentHover = Color.FromArgb(203, 191, 255);
    public static readonly Color AccentEdge = Color.FromArgb(214, 203, 255);
    public static readonly Color AccentDeep = Color.FromArgb(122, 104, 214);
    public static readonly Color AccentInk = Color.FromArgb(23, 19, 42);  // text on the accent
    public static readonly Color ActiveBg = Color.FromArgb(34, 30, 54);   // selected item
    public static readonly Color Bar = Color.FromArgb(74, 74, 85);        // chart bars of past days
    public static readonly Color Warn = Color.FromArgb(226, 104, 58);

    // Older names, mapped onto the palette.
    public static Color AccentSoft => Accent;
    public static Color Cyan => Accent;
    public static Color Mint => Accent;
    public static Color Amber => Ink;
    public static Color Gold => Ink;
    public static Color Green => Accent;
    public static Color Red => Dim;

    public static Font Text(float size = 9f, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Dark title bar in the window colour, square corners (Windows 11; ignored elsewhere).</summary>
    public static void Chrome(IntPtr hwnd)
    {
        try
        {
            int on = 1;
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
            DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int)); // older id of the same option
            int square = 1;                                       // DWMWCP_DONOTROUND
            DwmSetWindowAttribute(hwnd, 33, ref square, sizeof(int));
            int caption = ColorRef(Bg), border = ColorRef(CardEdge), text = ColorRef(Muted);
            DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
            DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int));  // DWMWA_BORDER_COLOR
            DwmSetWindowAttribute(hwnd, 36, ref text, sizeof(int));    // DWMWA_TEXT_COLOR
        }
        catch
        {
            // not supported: normal window frame
        }
    }

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>Botón plano con el estilo de la aplicación (usado en ajustes).</summary>
    public static void Style(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = Panel;
        b.ForeColor = Ink;
        b.Font = Text(9f);
        b.Cursor = Cursors.Hand;
        b.FlatAppearance.BorderColor = Line;
        b.FlatAppearance.MouseOverBackColor = PanelHover;
        b.FlatAppearance.MouseDownBackColor = Line;
    }
}
