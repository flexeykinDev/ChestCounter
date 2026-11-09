using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>
/// Chests per day as columns of pixel blocks: unlit blocks show the full scale, lit ones the day's chests.
/// Today is in the accent colour, the other days grey.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DailyChart : Control
{
    private IReadOnlyList<DayChests> _days = Array.Empty<DayChests>();

    public DailyChart()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }

    public void SetDays(IReadOnlyList<DayChests> days)
    {
        _days = days;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (_days.Count == 0)
        {
            return;
        }

        // Text bands come from the font height, so they grow with the window.
        using var fontValue = new Font(Font.FontFamily, Font.Size * 0.85f, FontStyle.Bold);
        using var fontDay = new Font(Font.FontFamily, Font.Size * 0.85f, FontStyle.Regular);
        var dayH = (int)Math.Ceiling(fontDay.GetHeight(g));
        var valueH = (int)Math.Ceiling(fontValue.GetHeight(g));
        var max = _days.Max(d => d.Chests);
        var slot = Width / _days.Count;
        var barW = Math.Max(6, Math.Min(slot - 10, (int)(dayH * 1.6f)));
        var baseline = Height - dayH - 4;
        var top = valueH + 3;
        var block = Math.Max(2, (int)Math.Round(barW / 6f));
        var pitch = block + 1;
        var blocks = Math.Max(1, (baseline - top) / pitch);

        var under = Parent?.BackColor ?? Theme.Card;
        using var off = new SolidBrush(Pixel.Blend(under, Color.White, 0.05f));
        using var past = new SolidBrush(Theme.Bar);
        using var today = new SolidBrush(Theme.Accent);

        for (var i = 0; i < _days.Count; i++)
        {
            var d = _days[i];
            var isToday = i == _days.Count - 1;
            var x = i * slot + (slot - barW) / 2;
            var lit = max > 0 ? (int)Math.Round(d.Chests / (double)max * blocks) : 0;
            if (d.Chests > 0)
            {
                lit = Math.Max(1, lit);
            }
            for (var b = 0; b < blocks; b++)
            {
                var y = baseline - (b + 1) * pitch + 1;
                g.FillRectangle(b < lit ? (isToday ? today : past) : off, x, y, barW, block);
            }

            // Figure above the column, day name below.
            TextRenderer.DrawText(g, L.N(d.Chests), fontValue, new Rectangle(i * slot - 4, 0, slot + 8, valueH),
                d.Chests > 0 ? Theme.Ink : Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom | TextFormatFlags.SingleLine);
            var name = d.Day.ToString("ddd", L.Culture);
            if (name.Length > 3)
            {
                name = name[..3];
            }
            TextRenderer.DrawText(g, name, fontDay, new Rectangle(i * slot, baseline + 4, slot, dayH),
                isToday ? Theme.Accent : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.SingleLine);
        }
    }
}
