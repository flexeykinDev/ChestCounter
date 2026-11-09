using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>Panel with a 1 px border, stepped pixel corners and faint grain. Child labels are transparent over it.</summary>
[SupportedOSPlatform("windows")]
public sealed class Card : Panel
{
    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }

    /// <summary>Kept for the layout code; corners are always stepped.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; }

    /// <summary>Border colour.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Edge { get; set; } = Theme.CardEdge;

    /// <summary>Film grain inside the panel.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Grain { get; set; } = true;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Pixel.Panel(e.Graphics, ClientRectangle, Edge, BackColor, Grain && Width > 20);
    }
}

/// <summary>Button with stepped corners. Accent fill = primary button; anything else = secondary.</summary>
[SupportedOSPlatform("windows")]
public sealed class PillButton : Button
{
    private bool _hover;

    public PillButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Panel;
        ForeColor = Theme.Ink;
        Font = Theme.Text(9f, FontStyle.Bold);
        Cursor = Cursors.Hand;
    }

    /// <summary>Fill when the mouse is over it.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = Theme.PanelHover;

    /// <summary>Kept for the layout code; corners are always stepped.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; }

    private Color? _indicator;

    /// <summary>Square state marker before the text (null = none). An accent marker also lights the border.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? Indicator
    {
        get => _indicator;
        set
        {
            if (_indicator != value)
            {
                _indicator = value;
                Invalidate();
            }
        }
    }

    private bool Primary => BackColor.ToArgb() == Theme.Accent.ToArgb();

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // OnPaint draws everything.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        var lit = _indicator is { } ind && ind.ToArgb() == Theme.Accent.ToArgb();
        var edge = Primary ? Theme.AccentEdge : lit ? Theme.AccentDeep : _hover ? Theme.Dim : Theme.Line;
        var fill = _hover && Enabled ? HoverColor : BackColor;
        if (lit && !Primary)
        {
            fill = _hover ? Theme.PanelHover : Theme.ActiveBg;
        }
        Pixel.Panel(g, ClientRectangle, edge, fill);
        var color = Enabled ? ForeColor : Theme.Dim;
        if (_indicator is not { } dot)
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            return;
        }

        // Square marker, then the text, centred together.
        var flags = TextFormatFlags.SingleLine;
        var textSize = TextRenderer.MeasureText(g, Text, Font, Size, flags);
        var d = Math.Max(6, (int)(Font.Height * 0.4f));
        var gap = d;
        var left = (Width - (d + gap + textSize.Width)) / 2;
        using (var brush = new SolidBrush(dot))
        {
            g.FillRectangle(brush, left, (Height - d) / 2, d, d);
        }
        var textRect = new Rectangle(left + d + gap, 0, textSize.Width + 2, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, color, flags | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Pixel switch: a stepped track with a square knob.</summary>
[SupportedOSPlatform("windows")]
public sealed class ToggleSwitch : Control
{
    private bool _checked;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Size = new Size(40, 22);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public event EventHandler? CheckedChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        Focus();
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        var track = new Rectangle(0, 1, Width - 1, Height - 3);
        var edge = _checked ? Theme.AccentDeep : Focused ? Theme.Dim : Theme.Line;
        Pixel.Panel(g, track, edge, _checked ? Theme.ActiveBg : Theme.Field, s: 2);
        var k = track.Height - 8;
        var x = _checked ? track.Right - k - 4 : track.Left + 4;
        using var knob = new SolidBrush(_checked ? Theme.Accent : Theme.Dim);
        g.FillRectangle(knob, x, track.Top + 4, k, k);
    }
}

/// <summary>
/// Figure drawn with the 5×7 dot font. The dot size follows the box (largest that fits), unlit dots stay faintly
/// visible. Transparent: the panel behind it (with its grain) shows through.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DotLabel : Control
{
    public DotLabel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }

    /// <summary>Horizontal placement inside the box.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HorizontalAlignment Align { get; set; } = HorizontalAlignment.Left;

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var text = Text ?? "";
        if (text.Length == 0)
        {
            return;
        }
        var d = DotFont.Fit(text, ClientSize);
        var size = DotFont.Measure(text, d);
        var x = Align switch
        {
            HorizontalAlignment.Center => (Width - size.Width) / 2,
            HorizontalAlignment.Right => Width - size.Width,
            _ => 0,
        };
        var y = (Height - size.Height) / 2;
        var under = Parent?.BackColor ?? Theme.Card;
        using var on = new SolidBrush(ForeColor);
        using var off = new SolidBrush(Pixel.Blend(under, Color.White, 0.055f));
        var g = e.Graphics;
        DotFont.Render(text, x, y, d, 1, (r, lit) => g.FillRectangle(lit ? on : off, r));
    }
}

/// <summary>
/// Etiqueta de una sola línea que nunca se corta: si el texto no cabe en su caja (ancho o alto),
/// se dibuja con la letra justa para que quepa. Su fuente es el tamaño máximo.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FitLabel : Label
{
    private Font? _fit;       // fuente reducida propia (null: se usa la del control)
    private Font? _fitBase;   // fuente del control con la que se calculó
    private string? _fitKey;

    public FitLabel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var text = Text ?? "";
        if (text.Length == 0)
        {
            return;
        }
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | Align(TextAlign);
        var font = FitFont(e.Graphics, text);
        TextRenderer.DrawText(e.Graphics, text, font, ClientRectangle, Enabled ? ForeColor : SystemColors.GrayText, flags);
    }

    private Font FitFont(IDeviceContext dc, string text)
    {
        var key = $"{text}|{ClientSize}";
        if (key == _fitKey && ReferenceEquals(_fitBase, Font))
        {
            return _fit ?? Font;
        }
        var size = Font.Size;
        var box = ClientSize;
        const TextFormatFlags measure = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        var fit = Font;
        for (var i = 0; i < 12; i++)
        {
            var m = TextRenderer.MeasureText(dc, text, fit, new Size(int.MaxValue, int.MaxValue), measure);
            if (m.Width <= box.Width && m.Height <= box.Height || size <= 5f)
            {
                break;
            }
            // Reduce lo que falte (en ancho o alto) y prueba otra vez: el redondeo de GDI puede pedir un paso más.
            var ratio = Math.Min(box.Width / (float)Math.Max(1, m.Width), box.Height / (float)Math.Max(1, m.Height));
            size = Math.Max(5f, size * Math.Min(ratio, 0.97f));
            if (!ReferenceEquals(fit, Font)) fit.Dispose();
            fit = new Font(Font.FontFamily, size, Font.Style, Font.Unit);
        }
        _fit?.Dispose();
        _fit = ReferenceEquals(fit, Font) ? null : fit;
        _fitBase = Font;
        _fitKey = key;
        return fit;
    }

    private static TextFormatFlags Align(ContentAlignment a) => a switch
    {
        ContentAlignment.TopLeft => TextFormatFlags.Top | TextFormatFlags.Left,
        ContentAlignment.TopCenter => TextFormatFlags.Top | TextFormatFlags.HorizontalCenter,
        ContentAlignment.TopRight => TextFormatFlags.Top | TextFormatFlags.Right,
        ContentAlignment.MiddleLeft => TextFormatFlags.VerticalCenter | TextFormatFlags.Left,
        ContentAlignment.MiddleCenter => TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
        ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter | TextFormatFlags.Right,
        ContentAlignment.BottomLeft => TextFormatFlags.Bottom | TextFormatFlags.Left,
        ContentAlignment.BottomCenter => TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter,
        _ => TextFormatFlags.Bottom | TextFormatFlags.Right,
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fit?.Dispose();
            _fit = null;
        }
        base.Dispose(disposing);
    }
}
