using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace ChestCounter;

/// <summary>5×7 dot-matrix font for figures: digits and a few signs. Shared by the window, the overlay and the HTML.</summary>
public static class DotFont
{
    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
        ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
        ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
        ['3'] = new[] { "11111", "00010", "00100", "00010", "00001", "10001", "01110" },
        ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
        ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
        ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
        ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
        ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
        ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" },
        ['-'] = new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" },
        ['…'] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "10101" },
        ['.'] = new[] { "0", "0", "0", "0", "0", "0", "1" },
        [','] = new[] { "00", "00", "00", "00", "00", "01", "10" },
        [':'] = new[] { "0", "0", "1", "0", "0", "1", "0" },
    };

    /// <summary>Glyph rows for a character; spaces give null (a blank gap), unknown signs a dash.</summary>
    public static string[]? Glyph(char c)
    {
        if (char.IsWhiteSpace(c))
        {
            return null;
        }
        if (c == '—' || c == '–')
        {
            c = '-';
        }
        return Glyphs.TryGetValue(c, out var g) ? g : Glyphs['-'];
    }

    private static int Cols(string[]? g) => g?[0].Length ?? 2;

    /// <summary>Size of a text drawn with dots of <paramref name="d"/> px and <paramref name="gap"/> px between dots.</summary>
    public static Size Measure(string text, int d, int gap = 1)
    {
        if (text.Length == 0)
        {
            return Size.Empty;
        }
        var w = 0;
        foreach (var c in text)
        {
            var cols = Cols(Glyph(c));
            w += cols * d + (cols - 1) * gap;
        }
        w += (text.Length - 1) * (d + gap);
        return new Size(w, 7 * d + 6 * gap);
    }

    /// <summary>Largest dot size (1..max) for which the text fits in the box.</summary>
    public static int Fit(string text, Size box, int max = 12)
    {
        for (var d = max; d > 1; d--)
        {
            var s = Measure(text, d);
            if (s.Width <= box.Width && s.Height <= box.Height)
            {
                return d;
            }
        }
        return 1;
    }

    /// <summary>Calls <paramref name="dot"/> for every dot (lit or not) of the text drawn at x, y.</summary>
    public static void Render(string text, int x, int y, int d, int gap, Action<Rectangle, bool> dot)
    {
        foreach (var c in text)
        {
            var g = Glyph(c);
            if (g != null)
            {
                for (var row = 0; row < 7; row++)
                {
                    for (var col = 0; col < g[row].Length; col++)
                    {
                        dot(new Rectangle(x + col * (d + gap), y + row * (d + gap), d, d), g[row][col] == '1');
                    }
                }
            }
            var cols = Cols(g);
            x += cols * d + (cols - 1) * gap + d + gap;
        }
    }

    /// <summary>HTML version (spans), coloured by CSS: <c>.dots .dg i</c> off, <c>i.on</c> lit.</summary>
    public static string Html(string text)
    {
        var sb = new System.Text.StringBuilder("<span class=\"dots\" aria-hidden=\"true\">");
        foreach (var c in text)
        {
            var g = Glyph(c);
            if (g == null)
            {
                sb.Append("<span class=\"sp\"></span>");
                continue;
            }
            sb.Append($"<span class=\"dg\" style=\"--c:{g[0].Length}\">");
            foreach (var row in g)
            {
                foreach (var bit in row)
                {
                    sb.Append(bit == '1' ? "<i class=\"on\"></i>" : "<i></i>");
                }
            }
            sb.Append("</span>");
        }
        return sb.Append("</span>").ToString();
    }
}

/// <summary>Pixel look: stepped corners, film grain and dust specks.</summary>
[SupportedOSPlatform("windows")]
internal static class Pixel
{
    /// <summary>Corner step in px (two steps per corner).</summary>
    public const int Step = 3;

    /// <summary>Outline of a rectangle with stepped corners (same shape as the CSS clip-path).</summary>
    public static Point[] Notch(Rectangle r, int s = Step)
    {
        int l = r.Left, t = r.Top, rr = r.Right, b = r.Bottom, s2 = s * 2;
        return new[]
        {
            new Point(l, t + s2), new Point(l + s, t + s2), new Point(l + s, t + s), new Point(l + s2, t + s), new Point(l + s2, t),
            new Point(rr - s2, t), new Point(rr - s2, t + s), new Point(rr - s, t + s), new Point(rr - s, t + s2), new Point(rr, t + s2),
            new Point(rr, b - s2), new Point(rr - s, b - s2), new Point(rr - s, b - s), new Point(rr - s2, b - s), new Point(rr - s2, b),
            new Point(l + s2, b), new Point(l + s2, b - s), new Point(l + s, b - s), new Point(l + s, b - s2), new Point(l, b - s2),
        };
    }

    /// <summary>Panel with a 1 px border: border colour outside, fill 1 px inside, both with stepped corners.</summary>
    public static void Panel(Graphics g, Rectangle r, Color edge, Color fill, bool grain = false, int s = Step)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using (var b = new SolidBrush(edge))
        {
            g.FillPolygon(b, Notch(r, s));
        }
        var inner = Rectangle.Inflate(r, -1, -1);
        var pts = Notch(inner, s);
        using (var b = new SolidBrush(fill))
        {
            g.FillPolygon(b, pts);
        }
        if (grain)
        {
            using var path = new GraphicsPath();
            path.AddPolygon(pts);
            var clip = g.Clip;
            g.SetClip(path, CombineMode.Intersect);
            g.FillRectangle(Grain, inner);
            g.Clip = clip;
        }
        g.SmoothingMode = old;
    }

    private static TextureBrush? _grain;

    /// <summary>Faint noise tile (a few levels of light and dark), barely visible on purpose.</summary>
    public static TextureBrush Grain => _grain ??= MakeGrain();

    private static TextureBrush MakeGrain()
    {
        var bmp = new Bitmap(160, 160, PixelFormat.Format32bppArgb);
        var rnd = new Random(7);
        for (var y = 0; y < bmp.Height; y++)
        {
            for (var x = 0; x < bmp.Width; x++)
            {
                var n = rnd.Next(100);
                var c = n < 45 ? Color.FromArgb(rnd.Next(3, 11), 255, 255, 255)
                    : n < 80 ? Color.FromArgb(rnd.Next(4, 14), 0, 0, 0)
                    : Color.Transparent;
                bmp.SetPixel(x, y, c);
            }
        }
        return new TextureBrush(bmp, WrapMode.Tile);
    }

    /// <summary>Window background: flat colour, grain and a few 2×2 specks at fixed spots.</summary>
    public static void Backdrop(Graphics g, Size size, int seed = 3)
    {
        g.Clear(Theme.Bg);
        g.FillRectangle(Grain, new Rectangle(Point.Empty, size));
        var rnd = new Random(seed);
        using var a = new SolidBrush(Color.FromArgb(44, 44, 51));
        using var b = new SolidBrush(Color.FromArgb(59, 53, 80));
        for (var i = 0; i < 26; i++)
        {
            var x = (int)(rnd.NextDouble() * size.Width);
            var y = (int)(rnd.NextDouble() * size.Height);
            g.FillRectangle(i % 3 == 0 ? b : a, x, y, 2, 2);
        }
    }

    /// <summary>6×6 square that starts section titles.</summary>
    public static System.Windows.Forms.Panel Marker(System.Windows.Forms.Control parent, int x, int y, Color? color = null)
    {
        var m = new System.Windows.Forms.Panel { BackColor = color ?? Theme.Accent, Location = new Point(x, y), Size = new Size(6, 6) };
        parent.Controls.Add(m);
        return m;
    }

    public static Color Blend(Color under, Color over, float alpha) => Color.FromArgb(
        (int)Math.Round(under.R + (over.R - under.R) * alpha),
        (int)Math.Round(under.G + (over.G - under.G) * alpha),
        (int)Math.Round(under.B + (over.B - under.B) * alpha));
}
