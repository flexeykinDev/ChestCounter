using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>
/// Ajustes con el mismo aspecto que la ventana principal: dos columnas de tarjetas, títulos pequeños en gris y
/// cifras grandes de color. Cada cambio se aplica y se guarda al instante.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SettingsForm : Form
{
    // Misma rejilla que la ventana principal: columna izquierda y derecha.
    private const int LeftX = 15, LeftW = 267, RightX = 297, RightW = 280;
    private const int MaxInitial = 1_000_000;

    private readonly Monitor _monitor;
    private readonly ToggleSwitch _fortnite;
    private readonly ToggleSwitch _report;
    private readonly ToggleSwitch _obsToggle;
    private Label _obsState = null!;
    private TextBox _obsField = null!;
    private PillButton _obsCopy = null!;
    private readonly TextBox _folder;
    private readonly TextBox _initial;
    private bool _loading;

    public SettingsForm(Monitor monitor, Store store)
    {
        _monitor = monitor;
        var config = monitor.Config;

        Text = L.T("Настройки", "Settings");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = Theme.Text();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(592, 341);
        Icon = AppIcon.Load();

        // Cabecera, como en la ventana principal.
        Lbl(this, L.T("НАСТРОЙКИ", "SETTINGS"), 8.5f, FontStyle.Bold, Theme.Cyan, new Point(LeftX, 14), new Size(200, 18));
        Lbl(this, L.T($"Версия {AppInfo.VersionText}", $"Version {AppInfo.VersionText}"), 8.0f, FontStyle.Regular, Theme.Muted,
            new Point(RightX, 13), new Size(RightW - 96, 18), ContentAlignment.TopRight);

        // Language: RU / EN, applied at the next start.
        foreach (var (code, x) in new[] { ("ru", RightX + RightW - 88), ("en", RightX + RightW - 42) })
        {
            var on = L.En == (code == "en");
            var pill = new PillButton
            {
                Text = code.ToUpperInvariant(), Location = new Point(x, 9), Size = new Size(42, 24), Radius = 9, Font = Theme.Text(8f, FontStyle.Bold),
                BackColor = on ? Theme.Accent : Theme.Panel, HoverColor = on ? Theme.AccentHover : Theme.PanelHover, ForeColor = on ? Theme.AccentInk : Theme.Ink,
            };
            pill.Click += (_, _) =>
            {
                if (L.En == (code == "en"))
                {
                    return;
                }
                _monitor.Change(c => c.Language = code);
                MessageBox.Show(this, code == "en" ? "The language changes after a restart of the counter." : "Язык сменится после перезапуска счётчика.",
                    "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            Controls.Add(pill);
        }

        // ---- Columna izquierda ---------------------------------------------------------------

        // Inicio: dos interruptores.
        var start = AddCard(LeftX, 40, LeftW, 100, 16);
        CardTitle(start, L.T("Запуск", "Startup"));
        _fortnite = AddToggleRow(start, L.T("Открывать с Fortnite", "Open with Fortnite"),
            L.T("Считает матчи без лишних действий", "Counts your matches without thinking about it"), 24, config.StartWithFortnite);
        _fortnite.CheckedChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }
            if (!_monitor.SetStartWithFortnite(_fortnite.Checked))
            {
                _loading = true;
                _fortnite.Checked = !_fortnite.Checked;
                _loading = false;
                MessageBox.Show(this,
                    L.T("Отсюда нельзя: откройте счётчик через ChestCounter.exe.", "Not possible from here: open the counter with ChestCounter.exe."),
                    L.T("Настройки", "Settings"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        _report = AddToggleRow(start, L.T("Открывать отчёт при запуске", "Open the report at startup"), null, 64, config.OpenReportOnStart);
        _report.CheckedChanged += (_, _) => { if (!_loading) _monitor.Change(c => c.OpenReportOnStart = _report.Checked); };

        // Local OBS page: off by default, only reachable from this PC.
        var net = AddCard(LeftX, 149, LeftW, 74, 14);
        CardTitle(net, L.T("Сеть", "Network"));
        _obsToggle = AddToggleRow(net, L.T("Страница для OBS", "OBS page"),
            L.T("Только на этом ПК (127.0.0.1)", "This PC only (127.0.0.1)"), 28, config.ObsEnabled);
        _obsToggle.CheckedChanged += (_, _) =>
        {
            if (!_loading)
            {
                _monitor.SetObsEnabled(_obsToggle.Checked);
                RefreshObs();
            }
        };

        // Carpeta de replays y datos.
        var folderCard = AddCard(LeftX, 232, LeftW, 94, 14);
        CardTitle(folderCard, L.T("Папка реплеев", "Replays folder"));
        _folder = AddField(folderCard, new Point(13, 26), LeftW - 26 - 72, config.DemosFolder);
        var browse = new PillButton { Text = L.T("Изменить", "Change"), Location = new Point(LeftW - 13 - 66, 26), Size = new Size(66, 30), Radius = 10, Font = Theme.Text(8.5f, FontStyle.Bold) };
        browse.Click += (_, _) => ChooseFolder();
        folderCard.Controls.Add(browse);
        Lbl(folderCard, L.T("Данные хранятся в ", "Your data is saved in ") + store.Folder,
            7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 62), new Size(LeftW - 26, 28));

        // ---- Columna derecha -----------------------------------------------------------------

        // Cofres de antes del contador: cifra grande en ámbar, editable, con − / +.
        var initial = AddCard(RightX, 40, RightW, 100, 16);
        CardTitle(initial, L.T("Сундуки до счётчика", "Chests before the counter"));
        _initial = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Card,
            ForeColor = Theme.Amber,
            Font = Theme.Text(24f, FontStyle.Bold),
            Location = new Point(15, 26),
            Width = 160,
            MaxLength = 7,
            Text = Math.Min(MaxInitial, monitor.InitialChests).ToString(L.Culture),
        };
        initial.Controls.Add(_initial);
        var plus = new PillButton { Text = "+", Location = new Point(RightW - 13 - 34, 22), Size = new Size(34, 28), Radius = 9, Font = Theme.Text(11f, FontStyle.Bold) };
        var minus = new PillButton { Text = "−", Location = new Point(RightW - 13 - 34, 54), Size = new Size(34, 28), Radius = 9, Font = Theme.Text(11f, FontStyle.Bold) };
        plus.Click += (_, _) => StepInitial(10);
        minus.Click += (_, _) => StepInitial(-10);
        initial.Controls.Add(plus);
        initial.Controls.Add(minus);
        _initial.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar);
        _initial.TextChanged += (_, _) =>
        {
            if (!_loading)
            {
                _monitor.SetInitialChests(ParseInitial());
            }
        };
        var profileName = monitor.ShownProfileName;
        Lbl(initial, profileName != null
                ? L.T($"оценка · добавляется к итогу {profileName}", $"estimate · added to {profileName}'s total")
                : L.T("оценка · добавляется к вашему итогу", "estimate · added to your total"),
            8.0f, FontStyle.Regular, Theme.Muted, new Point(16, 74), new Size(RightW - 70, 16));

        // OBS: dirección y estado.
        var obsCard = AddCard(RightX, 149, RightW, 74, 14);
        CardTitle(obsCard, L.T("Оверлей для OBS", "Overlay for OBS"));
        var stateCard = new Card { Location = new Point(RightW - 13 - 70, 6), Size = new Size(70, 18), Radius = 9, BackColor = Theme.Panel };
        _obsState = Lbl(stateCard, "", 7.0f, FontStyle.Bold, Theme.Red, new Point(0, 0), new Size(70, 18), ContentAlignment.MiddleCenter);
        obsCard.Controls.Add(stateCard);
        _obsField = AddField(obsCard, new Point(13, 30), RightW - 26 - 72, "");
        _obsCopy = new PillButton { Text = L.T("Копировать", "Copy"), Location = new Point(RightW - 13 - 66, 30), Size = new Size(66, 30), Radius = 10, Font = Theme.Text(8.5f, FontStyle.Bold) };
        _obsCopy.Click += (_, _) =>
        {
            if (ObsServer.Url is not { } url)
            {
                return;
            }
            try
            {
                Clipboard.SetText(url);
                _obsCopy.Text = "✓";
            }
            catch
            {
                _obsField.Focus();
                _obsField.SelectAll();
            }
        };
        obsCard.Controls.Add(_obsCopy);
        RefreshObs();

        // Atajos del overlay.
        var keys = AddCard(RightX, 232, RightW, 45, 12);
        Lbl(keys, L.T("Горячие клавиши оверлея", "Overlay shortcuts"), 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 6), new Size(RightW - 26, 13));
        Lbl(keys, L.T("Ctrl+Shift+O показать  ·  Ctrl+Shift+L закрепить", "Ctrl+Shift+O show  ·  Ctrl+Shift+L lock"),
            8.0f, FontStyle.Bold, Theme.Ink, new Point(13, 21), new Size(RightW - 26, 18));

        // Cerrar, en el sitio de los botones de la ventana principal.
        var close = new PillButton
        {
            Text = L.T("Закрыть", "Close"),
            Location = new Point(RightX, 286),
            Size = new Size(RightW, 40),
            Radius = 12,
            BackColor = Theme.Accent,
            HoverColor = Theme.AccentHover,
            ForeColor = Theme.AccentInk,
            DialogResult = DialogResult.OK,
        };
        Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
        ActiveControl = close; // sin anillo de foco en el primer interruptor al abrir
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.Chrome(Handle);
    }

    private void RefreshObs()
    {
        var url = ObsServer.Url;
        _obsState.Text = url != null ? L.T("● ВКЛ", "● ON") : L.T("● ВЫКЛ", "● OFF");
        _obsState.ForeColor = url != null ? Theme.Mint : Theme.Red;
        _obsField.Text = url ?? (_obsToggle.Checked ? L.T("Порт занят", "Port in use") : L.T("Выключено в «Сеть»", "Off under Network"));
        _obsCopy.Enabled = url != null;
        _obsCopy.Text = L.T("Копировать", "Copy");
    }

    private int ParseInitial() =>
        int.TryParse(_initial.Text, System.Globalization.NumberStyles.None, L.Culture, out var n) ? Math.Clamp(n, 0, MaxInitial) : 0;

    private void StepInitial(int delta)
    {
        var value = Math.Clamp(ParseInitial() + delta, 0, MaxInitial);
        _initial.Text = value.ToString(L.Culture); // TextChanged guarda el valor
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = L.T("Папка реплеев Fortnite", "Fortnite replays folder"),
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_folder.Text) ? _folder.Text : "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _monitor.ChangeDemosFolder(dialog.SelectedPath);
            _folder.Text = dialog.SelectedPath;
        }
    }

    // ---- Piezas de la ventana ----------------------------------------------------------------

    private Card AddCard(int x, int y, int w, int h, int radius)
    {
        var card = new Card { Location = new Point(x, y), Size = new Size(w, h), Radius = radius, BackColor = Theme.Card };
        Controls.Add(card);
        return card;
    }

    /// <summary>Título pequeño en gris, como "Hoy" o "Mejor día" en la ventana principal.</summary>
    private static void CardTitle(Card card, string text) =>
        Lbl(card, text, 8.0f, FontStyle.Regular, Theme.Muted, new Point(13, 7), new Size(card.Width - 100, 15));

    /// <summary>Texto (y una línea gris opcional) a la izquierda y un interruptor a la derecha; clicar el texto también lo cambia.</summary>
    private static ToggleSwitch AddToggleRow(Card card, string text, string? hint, int top, bool value)
    {
        var toggle = new ToggleSwitch { Location = new Point(card.Width - 13 - 40, top + 5), Checked = value };
        var label = Lbl(card, text, 9.5f, FontStyle.Bold, Theme.Ink, new Point(13, top), new Size(card.Width - 26 - 48, 18));
        label.Cursor = Cursors.Hand;
        label.Click += (_, _) => toggle.Checked = !toggle.Checked;
        if (hint != null)
        {
            var small = Lbl(card, hint, 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, top + 18), new Size(card.Width - 26 - 48, 14));
            small.Cursor = Cursors.Hand;
            small.Click += (_, _) => toggle.Checked = !toggle.Checked;
        }
        card.Controls.Add(toggle);
        return toggle;
    }

    /// <summary>Campo de solo lectura sin borde dentro de un fondo redondeado.</summary>
    private static TextBox AddField(Card card, Point at, int width, string text)
    {
        var holder = new Card { Location = at, Size = new Size(width, 30), Radius = 10, BackColor = Theme.Panel };
        var box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            Font = Theme.Text(9f),
            ReadOnly = true,
            Text = text,
            Width = width - 20,
        };
        box.Location = new Point(10, (holder.Height - box.Height) / 2);
        holder.Controls.Add(box);
        card.Controls.Add(holder);
        return box;
    }

    private static Label Lbl(Control parent, string text, float size, FontStyle style, Color color, Point at, Size sz, ContentAlignment align = ContentAlignment.TopLeft)
    {
        var label = new Label
        {
            Text = text,
            Font = Theme.Text(size, style),
            ForeColor = color,
            BackColor = parent.BackColor,
            Location = at,
            Size = sz,
            TextAlign = align,
            AutoEllipsis = true,
        };
        parent.Controls.Add(label);
        return label;
    }
}
