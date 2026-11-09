using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>
/// Ventana principal, horizontal 16:9 (592×332): total y partida actual, última partida, gráfico de 7 días,
/// estadísticas en tarjetas y los botones de acción.
/// Todo lo demás (escaneo, overlay, actualizaciones) lo hace <see cref="Monitor"/> en segundo plano.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MainForm : Form
{
    private readonly Monitor _monitor;
    private readonly Store _store;
    private readonly Card _badgeCard;
    private readonly Label _badge;
    private readonly Label _profileName;
    private readonly ComboBox _profile;
    private readonly DotLabel _total;
    private readonly Label _totalCap;
    private readonly DotLabel _current;
    private readonly Label _currentCap;
    private readonly Label _session;
    private readonly DotLabel _statToday, _statBestDay, _statAverage, _statBestMatch, _statMatches, _statTreasures;
    private readonly Label _statTodayCap, _statBestDayCap, _statAverageCap, _statBestMatchCap, _statMatchesCap, _statTreasuresCap;
    private readonly Card _noticeCard;
    private readonly Label _notice;
    private readonly PillButton _overlayButton, _reportButton, _settingsButton;
    private readonly DailyChart _daily;
    private readonly Label _lastValue, _lastDetail;
    private MonitorState? _last;
    private string? _profileSig;
    private bool _ignoreProfileChange;
    private bool _autoCloseOffer;

    // Disposición: 592×332 (16:9). Columna izquierda (total, última partida, gráfico) y derecha (estadísticas y botones).
    private const int LeftX = 15, LeftW = 267, RightX = 297, RightW = 278;

    public MainForm(Monitor monitor, Store store)
    {
        _monitor = monitor;
        _store = store;

        Text = L.T("Счётчик сундуков", "Chest Counter") + " v" + AppInfo.VersionText;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = Theme.Text();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(420, 236);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(592, 332);
        Icon = AppIcon.Load();
        DoubleBuffered = true;

        // Cabecera: título, perfil y distintivo.
        Pixel.Marker(this, LeftX + 1, 20);
        Lbl(this, L.T("СЧЁТЧИК СУНДУКОВ", "CHEST COUNTER"), 8.5f, FontStyle.Bold, Theme.Ink, new Point(LeftX + 12, 14), new Size(200, 18));
        _badgeCard = new Card { Location = new Point(474, 9), Size = new Size(106, 22), BackColor = Theme.ActiveBg, Edge = Theme.AccentDeep, Grain = false, Visible = false };
        _badge = Lbl(_badgeCard, "", 7.5f, FontStyle.Bold, Theme.Accent, new Point(1, 1), new Size(104, 20), ContentAlignment.MiddleCenter);
        Controls.Add(_badgeCard);

        _profileName = Lbl(this, "", 8.0f, FontStyle.Regular, Theme.Muted, new Point(230, 11), new Size(240, 18), ContentAlignment.TopRight);
        _profile = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Field,
            ForeColor = Theme.Ink,
            Font = Theme.Text(),
            Location = new Point(306, 8),
            Size = new Size(164, 24),
            Visible = false,
        };
        _profile.SelectedIndexChanged += (_, _) =>
        {
            if (!_ignoreProfileChange && _profile.SelectedItem is ProfileItem item)
            {
                _monitor.SetProfile(item.Key);
            }
        };
        Controls.Add(_profile);

        // Tarjeta principal: total y partida actual.
        var hero = new Card { Location = new Point(LeftX, 40), Size = new Size(LeftW, 100), Radius = 16 };
        Controls.Add(hero);
        _total = Dots(hero, "0", Theme.Accent, new Point(16, 16), new Size(118, 36));
        _totalCap = Lbl(hero, L.T("сундуков всего", "chests in total"), 8.0f, FontStyle.Regular, Theme.Muted, new Point(16, 59), new Size(120, 16));
        hero.Controls.Add(new Panel { BackColor = Theme.CardEdge, Location = new Point(138, 15), Size = new Size(1, 70) });
        _current = Dots(hero, "—", Theme.Ink, new Point(153, 16), new Size(104, 36));
        _currentCap = Lbl(hero, L.T("последний матч", "last match"), 8.0f, FontStyle.Regular, Theme.Muted, new Point(153, 59), new Size(110, 16));
        _session = Lbl(hero, "", 8.0f, FontStyle.Regular, Theme.Ink, new Point(15, 77), new Size(243, 16));

        // Última partida: cofres abiertos, con los detalles (rotos, tesoros, puesto, eliminaciones).
        var last = new Card { Location = new Point(LeftX, 149), Size = new Size(LeftW, 50), Radius = 12 };
        Pixel.Marker(last, 13, 10);
        Lbl(last, L.T("Последний матч", "Last match"), 7.5f, FontStyle.Regular, Theme.Muted, new Point(24, 6), new Size(92, 13));
        _lastDetail = Lbl(last, "", 7.5f, FontStyle.Regular, Theme.Muted, new Point(101, 5), new Size(153, 15), ContentAlignment.MiddleRight);
        _lastValue = Lbl(last, "—", 11.5f, FontStyle.Bold, Theme.Ink, new Point(13, 22), new Size(243, 22));
        Controls.Add(last);

        // Gráfico de los últimos 7 días.
        var daily = new Card { Location = new Point(LeftX, 206), Size = new Size(LeftW, 120), Radius = 14 };
        Pixel.Marker(daily, 13, 11);
        Lbl(daily, L.T("Последние 7 дней", "Last 7 days"), 8.0f, FontStyle.Regular, Theme.Muted, new Point(24, 7), new Size(184, 13));
        _daily = new DailyChart { Location = new Point(9, 26), Size = new Size(249, 88) };
        daily.Controls.Add(_daily);
        Controls.Add(daily);

        // Rejilla de estadísticas: dos columnas, tres filas (columna derecha).
        _statToday = Stat(L.T("Сегодня", "Today"), RightX, 40, Theme.Accent, out _statTodayCap);
        _statBestDay = Stat(L.T("Лучший день", "Best day"), RightX + 142, 40, Theme.Ink, out _statBestDayCap);
        _statAverage = Stat(L.T("В среднем за матч", "Average per match"), RightX, 117, Theme.Ink, out _statAverageCap);
        _statBestMatch = Stat(L.T("Лучший матч", "Best match"), RightX + 142, 117, Theme.Ink, out _statBestMatchCap);
        _statMatches = Stat(L.T("История", "History"), RightX, 196, Theme.Ink, out _statMatchesCap);
        _statTreasures = Stat(L.T("Сокровища", "Treasures"), RightX + 142, 196, Theme.Ink, out _statTreasuresCap);

        // Aviso (versión nueva, cierre con Fortnite) o botón de actualizar: ocupa el sitio de los botones.
        _noticeCard = new Card { Location = new Point(RightX, 277), Size = new Size(RightW, 40), BackColor = Theme.ActiveBg, Edge = Theme.AccentDeep, Visible = false };
        _notice = Lbl(_noticeCard, "", 8.5f, FontStyle.Regular, Theme.Accent, new Point(13, 0), new Size(RightW - 26, 40), ContentAlignment.MiddleLeft);
        _noticeCard.Click += (_, _) => { if (_autoCloseOffer) _monitor.KeepOpen(); };
        _notice.Click += (_, _) => { if (_autoCloseOffer) _monitor.KeepOpen(); };
        Controls.Add(_noticeCard);

        // Botones de acción.
        _overlayButton = Action(L.T("Оверлей", "Overlay"), RightX);
        _overlayButton.Click += (_, _) => _monitor.ToggleOverlay();
        _reportButton = Action(L.T("Отчёт", "Report"), RightX + 95);
        _reportButton.Click += (_, _) => Open(_store.ReportPath);
        _settingsButton = Action(L.T("Настройки", "Settings"), RightX + 190);
        _settingsButton.Click += (_, _) =>
        {
            using var dialog = new SettingsForm(_monitor, _store);
            dialog.ShowDialog(this);
        };

        // Diseño base (592×332): al redimensionar o maximizar, todos los controles y fuentes escalan proporcionalmente.
        CaptureLayout();
        _scaleTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _scaleTimer.Tick += (_, _) => { _scaleTimer.Stop(); ApplyLayoutScale(); };

        monitor.Changed += OnChanged;
        Shown += (_, _) =>
        {
            if (_monitor.AutoMode)
            {
                WindowState = FormWindowState.Minimized; // abierto con Fortnite: el juego conserva la pantalla
            }
        };
    }

    private const int DesignW = 592, DesignH = 332;
    private readonly Dictionary<Control, LayoutEntry> _layout = new();
    private System.Windows.Forms.Timer? _scaleTimer;
    private float _lastFx = 1, _lastFy = 1;

    private sealed class LayoutEntry
    {
        public Point Location;
        public Size Size;
        public Font? Font;
        public Font? Scaled;
    }

    private void CaptureLayout()
    {
        Capture(this);
        void Capture(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                _layout[c] = new LayoutEntry { Location = c.Location, Size = c.Size, Font = c.Font };
                Capture(c);
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            return;
        }
        _scaleTimer?.Stop();
        _scaleTimer?.Start();
    }

    /// <summary>Escala posiciones, tamaños y fuentes desde el diseño base. Las fuentes van en puntos: se dividen por el DPI para no contarlas dos veces.</summary>
    private void ApplyLayoutScale()
    {
        if (_layout.Count == 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }
        var fx = ClientSize.Width / (float)DesignW;
        var fy = ClientSize.Height / (float)DesignH;
        if (Math.Abs(fx - _lastFx) < 0.005 && Math.Abs(fy - _lastFy) < 0.005)
        {
            return;
        }
        _lastFx = fx;
        _lastFy = fy;
        // Durante restaurar/maximizar el tamaño puede ser transitorio: sin factor válido no se tocan las fuentes.
        var k = Math.Min(fx / (DeviceDpi / 96f), fy / (DeviceDpi / 96f));
        var scaleFonts = float.IsFinite(k) && k > 0.2 && k < 10;
        foreach (var (c, l) in _layout)
        {
            c.Location = new Point((int)Math.Round(l.Location.X * fx), (int)Math.Round(l.Location.Y * fy));
            c.Size = new Size((int)Math.Round(l.Size.Width * fx), (int)Math.Round(l.Size.Height * fy));
            if (scaleFonts && l.Font != null)
            {
                var nf = new Font(l.Font.FontFamily, l.Font.SizeInPoints * k, l.Font.Style);
                l.Scaled?.Dispose();
                l.Scaled = nf;
                c.Font = nf;
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.Chrome(Handle); // esquinas redondeadas de Windows 11 (el sistema las dibuja sin tocar botones ni bordes)
        if (_last != null)
        {
            Render(_last);
        }
    }

    private void OnChanged(MonitorState state)
    {
        _last = state;
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }
        try
        {
            BeginInvoke(new Action(() => Render(state)));
        }
        catch (ObjectDisposedException)
        {
            // la ventana se está cerrando
        }
        catch (InvalidOperationException)
        {
            // todavía sin ventana
        }
    }

    private void Render(MonitorState s)
    {
        var v = s.View;
        _total.Text = v.Total;
        _current.Text = v.Current ?? "—";
        _current.ForeColor = v.CurrentLive ? Theme.Accent : v.Current == null ? Theme.Dim : Theme.Ink;
        // El overlay usa textos cortos ("anterior" / "actual"); aquí hay sitio para los largos.
        _currentCap.Text = v.CurrentCaption == OverlayModel.LastMatchCaption ? L.T("последний матч", "last match")
            : v.CurrentLive && v.Current != "…" ? L.T("текущий матч", "current match")
            : v.CurrentCaption;
        _badge.Text = v.Badge ?? "";
        _badgeCard.Visible = !string.IsNullOrEmpty(v.Badge);

        // Línea de sesión: la primera fila del modelo (cofres y, detrás, partidas y media).
        var session = v.Rows.FirstOrDefault();
        var sessionChests = session != null && int.TryParse(session.Value, System.Globalization.NumberStyles.None, L.Culture, out var n)
            ? L.Count(n, "сундук", "сундука", "сундуков", "chest", "chests")
            : session?.Value;
        _session.Text = session == null ? "" : $"{L.T("Сессия", "Session")}: {sessionChests}" + (session.Detail != null ? $"  ·  {session.Detail}" : "");

        var st = s.Stats;
        _statToday.Text = L.N(st.Today);
        // La fecha va en la línea de arriba: "1.409 · 06/11" a 15 pt no cabe en la tarjeta y se cortaba.
        _statBestDay.Text = st.BestDay is { } bestDay ? L.N(st.BestDayChests) : "—";
        _statBestDayCap.Text = st.BestDay is { } bd
            ? $"{L.T("Лучший день", "Best day")} · {bd:dd/MM}"
            : L.T("Лучший день", "Best day");
        _statAverage.Text = st.Matches > 0 ? st.Average.ToString("0.0", L.Culture) : "—";
        _statBestMatch.Text = st.Matches > 0 ? L.N(st.BestMatch) : "—";
        _statMatches.Text = L.N(st.HistoryMatches);
        _statMatchesCap.Text = st.HistorySince is { } since
            ? L.T($"Матчей · с {since:dd.MM.yy}", $"Matches · since {since:dd/MM/yy}")
            : L.T("Матчей", "Matches");
        _statTreasures.Text = L.N(st.Treasures);
        _overlayButton.Enabled = _monitor.OverlayEnabled;
        _overlayButton.Indicator = _monitor.OverlayVisible ? Theme.Accent : Theme.Dim;

        if (st.LastMatch is { } lm)
        {
            _lastValue.Text = L.Count(lm.Chests, "сундук открыт", "сундука открыто", "сундуков открыто", "chest opened", "chests opened");
            var parts = new List<string>();
            if (lm.Destroyed > 0) parts.Add(L.T($"сломано: {lm.Destroyed}", $"{lm.Destroyed} destroyed"));
            if (lm.Treasures > 0) parts.Add(L.Count(lm.Treasures, "сокровище", "сокровища", "сокровищ", "treasure", "treasures"));
            if (lm.Placement is { } place) parts.Add(lm.Players is { } players ? $"{L.Ordinal(place)} {L.T("из", "of")} {players}" : L.Ordinal(place));
            if (lm.Eliminations is { } elim) parts.Add(L.T($"{elim} элим.", $"{elim} elim."));
            _lastDetail.Text = string.Join("  ·  ", parts);
        }
        else
        {
            _lastValue.Text = "—";
            _lastDetail.Text = "";
        }
        _daily.SetDays(st.LastDays);

        // Perfil: nombre fijo con una sola cuenta, selector con varias.
        var signature = string.Join("|", s.Profiles.Select(p => p.Key));
        var several = s.Profiles.Count >= 2;
        _profile.Visible = several;
        _profileName.Visible = !several;
        _profileName.Text = s.Profiles.FirstOrDefault(p => p.Key == s.CurrentKey) is { } current
            ? $"{L.T("Профиль", "Profile")}: {current.Name}"
            : "";
        if (signature != _profileSig)
        {
            _profileSig = signature;
            _ignoreProfileChange = true;
            _profile.Items.Clear();
            foreach (var p in s.Profiles)
            {
                _profile.Items.Add(new ProfileItem(p.Key, Profiles.Describe(p)));
            }
            _ignoreProfileChange = false;
        }
        _ignoreProfileChange = true;
        for (var i = 0; i < _profile.Items.Count; i++)
        {
            if (_profile.Items[i] is ProfileItem item && item.Key == s.CurrentKey)
            {
                _profile.SelectedIndex = i;
                break;
            }
        }
        _ignoreProfileChange = false;

        // Aviso o actualización: uno u otro, en el mismo sitio.
        _autoCloseOffer = s.AutoCloseOffer;
        _notice.Text = s.Notice ?? "";
        _notice.Cursor = s.AutoCloseOffer ? Cursors.Hand : Cursors.Default;
        _noticeCard.Visible = !string.IsNullOrEmpty(s.Notice);

        // Los botones ocupan el mismo sitio que el aviso o la actualización: cuando hay aviso, se ocultan.
        var notice = _noticeCard.Visible;
        _overlayButton.Visible = _reportButton.Visible = _settingsButton.Visible = !notice;
    }

    /// <summary>Tarjeta de estadística: título arriba y cifra debajo. Devuelve la etiqueta de la cifra.</summary>
    private DotLabel Stat(string caption, int x, int y, Color valueColor, out Label captionLabel)
    {
        var card = new Card { Location = new Point(x, y), Size = new Size(136, 70) };
        Controls.Add(card);
        captionLabel = Lbl(card, caption, 8.0f, FontStyle.Regular, Theme.Muted, new Point(12, 9), new Size(116, 15));
        return Dots(card, "—", valueColor, new Point(13, 33), new Size(114, 27));
    }

    private static DotLabel Dots(Control parent, string text, Color color, Point at, Size size)
    {
        var d = new DotLabel { Text = text, ForeColor = color, Location = at, Size = size };
        parent.Controls.Add(d);
        return d;
    }

    protected override void OnPaintBackground(PaintEventArgs e) => Pixel.Backdrop(e.Graphics, ClientSize);

    private PillButton Action(string text, int x)
    {
        var b = new PillButton { Text = text, Location = new Point(x, 277), Size = new Size(88, 40), Radius = 11 };
        Controls.Add(b);
        return b;
    }

    private static Label Lbl(Control parent, string text, float size, FontStyle style, Color color, Point at, Size sz, ContentAlignment align = ContentAlignment.TopLeft)
    {
        var l = new FitLabel
        {
            Text = text,
            Font = Theme.Text(size, style),
            ForeColor = color,
            BackColor = Color.Transparent,
            Location = at,
            Size = sz,
            TextAlign = align,
        };
        parent.Controls.Add(l);
        return l;
    }

    private static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // el informe aún no existe o no hay navegador: nada que hacer
        }
    }

    private sealed record ProfileItem(string Key, string Text)
    {
        public override string ToString() => Text;
    }
}
