using System.Globalization;
using System.Net;
using System.Text;

namespace ChestCounter;

public static class Pretty
{
    public static string Playlist(string? playlist)
    {
        if (string.IsNullOrEmpty(playlist))
        {
            return L.T("неизвестный режим", "unknown mode");
        }

        var p = playlist.ToLowerInvariant();
        var parts = new List<string>();
        if (p.Contains("habanero") || p.Contains("ranked")) parts.Add(L.T("Рейтинг", "Ranked"));
        if (p.Contains("nobuild")) parts.Add(L.T("Без строительства", "Zero Build"));
        if (p.Contains("solo")) parts.Add("Solo");
        else if (p.Contains("duo")) parts.Add(L.T("Дуо", "Duos"));
        else if (p.Contains("trio")) parts.Add(L.T("Трио", "Trios"));
        else if (p.Contains("squad")) parts.Add(L.T("Отряд", "Squads"));
        return parts.Count > 0 ? string.Join(" · ", parts) : playlist.Replace("Playlist_", "");
    }

    public static string Placement(MatchRecord m)
    {
        if (m.Placement is not int place || place <= 0)
        {
            return L.T("место неизвестно", "unknown placement");
        }
        if (place == 1)
        {
            return L.T("Королевская победа!", "Victory Royale!");
        }
        var ordinal = L.Ordinal(place);
        return m.TotalPlayers is int total && total > 0
            ? (L.En ? $"{ordinal} of {total}" : $"{ordinal} из {total}")
            : ordinal;
    }

    public static string Clock(float seconds)
    {
        var s = (int)seconds;
        return $"{s / 60}:{s % 60:00}";
    }

    /// <summary>"a las 7:16 · Cofre raro" (seguro) o "hacia las 5:04 · Cofre raro (probable)".</summary>
    public static string Obtained(TreasureFind f)
    {
        if (f.ObtainedAtSeconds is not float t)
        {
            return L.T("время и источник неизвестны", "time and source unknown");
        }
        var when = f.TimeExact ? $"{L.T("в", "at")} {Clock(t)}" : $"{L.T("около", "around")} {Clock(t)}";
        return f.SourceCertain ? $"{when} · {f.SourceLabel}" : $"{when} · {f.SourceLabel} ({Likely})";
    }

    public static string Likely => L.T("вероятно", "likely");

    public static string ItemName(string item)
    {
        var name = item;
        foreach (var prefix in new[] { "WID_", "Athena", "AmmoData" })
        {
            name = name.Replace(prefix, "");
        }
        return name.Replace("_", " ").Trim();
    }
}

/// <summary>Replays analyzed so far / to analyze, shown as a loading card in the report.</summary>
public sealed record ScanProgress(int Done, int Total);

public static class ReportWriter
{
    public static readonly DateTime SessionStartUtc = DateTime.UtcNow;

    /// <summary>Set while several replays are being analyzed: the report shows a loading card and refreshes faster.</summary>
    public static volatile ScanProgress? Progress;

    /// <summary>A match belongs to this session if it ended after the tool was started (backlog replays do not count).</summary>
    public static bool IsThisSession(MatchRecord m)
    {
        var start = m.MatchStartUtc ?? m.AnalyzedAtUtc;
        if (start.Kind == DateTimeKind.Unspecified)
        {
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        }
        var end = start.ToUniversalTime().AddSeconds(m.DurationSeconds ?? 0);
        return end >= SessionStartUtc.AddMinutes(-2);
    }

    private static CultureInfo Loc => L.Culture;

    public static void WriteAll(Store store, Config config, TrackerData data)
    {
        try
        {
            var view = Profiles.View(data, config);
            WriteAtomic(store.ReportPath, Report(config, view, Profiles.List(data), Profiles.InitialChests(data, config)));
            WriteAtomic(store.OverlayPath, Overlay(OverlayModel.Build(view, null, false, Profiles.InitialChests(data, config))));
            WriteAtomic(store.CsvPath, Csv(data));
        }
        catch (Exception ex)
        {
            Console.WriteLine(L.T($"Не удалось записать отчёт: {ex.Message}", $"Cannot write the report: {ex.Message}"));
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static DateTime When(MatchRecord m) => (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime();

    // Misma paleta que la ventana (azul noche, cian y ámbar). Oscuro por defecto; claro si el sistema lo pide.
    private const string Style = """
        :root{--bg:#0b0e14;--bg2:#0f131b;--card:#151a24;--card2:#1a2030;--ink:#e6eaf2;--muted:#7a8496;--faint:#4a5468;--line:#232b3a;--line2:#2e3850;
          --cyan:#38d6f0;--cyan-soft:rgba(56,214,240,.12);--amber:#ffb84d;--amber-soft:rgba(255,184,77,.13);--mint:#3ddc97;--mint-soft:rgba(61,220,151,.13);
          --rose:#ff80aa;--rose-soft:rgba(255,128,170,.12);--blue:#3878dc;--shadow:0 1px 2px rgba(0,0,0,.3),0 8px 24px rgba(0,0,0,.18)}
        @media (prefers-color-scheme:light){:root:not([data-theme="dark"]){--bg:#f4f6fa;--bg2:#eef1f6;--card:#fff;--card2:#f7f9fc;--ink:#151a24;--muted:#5d6678;--faint:#9aa3b5;
          --line:#e3e7ef;--line2:#d4dae6;--cyan:#0b8fb0;--cyan-soft:rgba(11,143,176,.09);--amber:#b8730d;--amber-soft:rgba(184,115,13,.1);--mint:#13895a;--mint-soft:rgba(19,137,90,.1);
          --rose:#c2346a;--rose-soft:rgba(194,52,106,.08);--blue:#2f6fd6;--shadow:0 1px 2px rgba(20,30,50,.06),0 6px 20px rgba(20,30,50,.06)}}
        :root[data-theme="light"]{--bg:#f4f6fa;--bg2:#eef1f6;--card:#fff;--card2:#f7f9fc;--ink:#151a24;--muted:#5d6678;--faint:#9aa3b5;
          --line:#e3e7ef;--line2:#d4dae6;--cyan:#0b8fb0;--cyan-soft:rgba(11,143,176,.09);--amber:#b8730d;--amber-soft:rgba(184,115,13,.1);--mint:#13895a;--mint-soft:rgba(19,137,90,.1);
          --rose:#c2346a;--rose-soft:rgba(194,52,106,.08);--blue:#2f6fd6;--shadow:0 1px 2px rgba(20,30,50,.06),0 6px 20px rgba(20,30,50,.06)}
        *{box-sizing:border-box}
        html{background:var(--bg)}
        body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.5 "Segoe UI Variable Text","Segoe UI",system-ui,-apple-system,sans-serif;-webkit-font-smoothing:antialiased}
        main{max-width:1120px;margin:0 auto;padding:28px 16px 56px}
        .num{font-variant-numeric:tabular-nums}
        .muted{color:var(--muted)}

        /* Cabecera */
        .masthead{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:14px 24px;padding-bottom:20px;margin-bottom:24px;border-bottom:1px solid var(--line)}
        .brand{display:flex;align-items:center;gap:14px;min-width:0}
        .logo{width:44px;height:44px;flex:none;border-radius:12px;display:grid;place-items:center;background:var(--cyan-soft);color:var(--cyan)}
        .eyebrow{font-size:11px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:var(--cyan)}
        h1{margin:0;font-size:24px;line-height:1.2;font-weight:700;letter-spacing:-.015em;overflow-wrap:anywhere}
        .upd{display:inline-flex;align-items:center;gap:8px;padding:6px 12px;border:1px solid var(--line);border-radius:99px;background:var(--card);color:var(--muted);font-size:12.5px}
        .dot{width:8px;height:8px;border-radius:50%;background:var(--mint);box-shadow:0 0 0 0 var(--mint);animation:pulse 2s ease-out infinite}
        @keyframes pulse{0%{box-shadow:0 0 0 0 rgba(61,220,151,.5)}70%{box-shadow:0 0 0 7px rgba(61,220,151,0)}100%{box-shadow:0 0 0 0 rgba(61,220,151,0)}}
        .profiles{margin:-12px 0 22px;font-size:13px;color:var(--muted)}

        /* Cifras clave */
        .kpis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px}
        .kpi{position:relative;background:var(--card);border:1px solid var(--line);border-radius:16px;padding:16px 18px;box-shadow:var(--shadow);min-width:0}
        .kpi .l{font-size:12px;font-weight:600;letter-spacing:.06em;text-transform:uppercase;color:var(--muted)}
        .kpi .v{margin-top:6px;font-size:30px;font-weight:700;line-height:1.1;letter-spacing:-.02em;font-variant-numeric:tabular-nums;overflow-wrap:anywhere}
        .kpi .s{margin-top:4px;font-size:12.5px;color:var(--muted)}
        .kpi.hero{grid-column:span 2;grid-row:span 2;display:flex;flex-direction:column;justify-content:space-between;overflow:hidden;
          background:linear-gradient(135deg,var(--cyan-soft),transparent 65%),var(--card)}
        .kpi.hero .v{font-size:64px;color:var(--cyan);margin-top:10px}
        .kpi.hero .s{font-size:13.5px}
        .kpi .acc{color:var(--amber)} .kpi .ros{color:var(--rose)} .kpi .mnt{color:var(--mint)}
        .split{display:flex;gap:28px;margin-top:18px;padding-top:14px;border-top:1px solid var(--line)}
        .split b{display:block;font-size:20px;font-variant-numeric:tabular-nums}
        .split span{font-size:12px;color:var(--muted)}

        /* Secciones */
        section{margin-top:28px}
        .sh{display:flex;flex-wrap:wrap;align-items:baseline;justify-content:space-between;gap:4px 16px;margin:0 0 12px}
        h2{margin:0;font-size:16px;font-weight:700;letter-spacing:-.005em}
        .sh .meta{font-size:12.5px;color:var(--muted)}
        .card{background:var(--card);border:1px solid var(--line);border-radius:16px;box-shadow:var(--shadow)}
        .pad{padding:18px 20px}
        .grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px 16px}
        .grid2 section{margin-top:0}

        /* Gráfico de 30 días */
        .chart{position:relative;display:grid;grid-template-columns:auto 1fr;gap:0 10px}
        .yaxis{display:flex;flex-direction:column;justify-content:space-between;height:200px;font-size:11px;color:var(--faint);text-align:right;font-variant-numeric:tabular-nums}
        .yaxis span{transform:translateY(-50%)} .yaxis span:last-child{transform:translateY(50%)}
        .plot{position:relative;height:200px;display:flex;align-items:flex-end;gap:3px;
          background:linear-gradient(var(--line) 1px,transparent 1px) 0 0/100% 50%;border-bottom:1px solid var(--line2)}
        .avg{position:absolute;left:0;right:0;z-index:2;border-top:1px dashed var(--amber);pointer-events:none}
        .avg span{position:absolute;left:0;top:-19px;font-size:11px;font-weight:600;color:var(--amber);background:var(--card);padding:0 4px;border-radius:4px}
        .col{flex:1;min-width:4px;height:100%;display:flex;align-items:flex-end;position:relative}
        .bar{width:100%;background:var(--blue);border-radius:4px 4px 1px 1px;transition:filter .12s,background .12s}
        .col.today .bar{background:var(--cyan)}
        .bar.zero{background:var(--line2);height:2px!important;border-radius:1px}
        .col:hover .bar{filter:brightness(1.25)}
        .tip{position:absolute;left:50%;transform:translateX(-50%);white-space:nowrap;background:var(--ink);color:var(--bg);
          padding:4px 9px;border-radius:8px;font-size:12px;font-weight:600;opacity:0;pointer-events:none;transition:opacity .12s;z-index:3}
        .col:nth-child(-n+4) .tip{left:0;transform:none}
        .col:nth-last-child(-n+4) .tip{left:auto;right:0;transform:none}
        .col:hover .tip{opacity:1}
        .xaxis{grid-column:2;display:flex;gap:3px;margin-top:8px;font-size:11px;color:var(--faint)}
        .xaxis span{flex:1;min-width:4px;text-align:center;white-space:nowrap;overflow:visible}
        .xaxis span:empty{visibility:hidden}
        .xaxis .t{color:var(--cyan);font-weight:600}

        /* Listas con barra de proporción */
        .rows{display:flex;flex-direction:column}
        .row{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:4px 12px;padding:10px 0;border-bottom:1px solid var(--line)}
        .row:last-child{border-bottom:none}
        .row .n{font-weight:700;font-variant-numeric:tabular-nums;text-align:right}
        .row .n small{font-weight:500;color:var(--muted);margin-left:6px}
        .track{grid-column:1/-1;height:6px;border-radius:3px;background:var(--bg2);overflow:hidden}
        .track i{display:block;height:100%;border-radius:3px;background:var(--cyan)}
        .row.sub{color:var(--muted)} .row.sub .n{color:var(--muted);font-weight:600}
        .row.warn .track i{background:var(--amber)}
        .note{margin:12px 0 0;font-size:12.5px;color:var(--muted)}

        /* Tablas */
        .tw{overflow-x:auto}
        table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}
        th,td{text-align:left;padding:11px 16px;border-bottom:1px solid var(--line);vertical-align:middle}
        th{position:sticky;top:0;background:var(--card2);font-size:11.5px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:var(--muted);white-space:nowrap}
        thead tr th:first-child{border-top-left-radius:15px} thead tr th:last-child{border-top-right-radius:15px}
        tbody tr:last-child td{border-bottom:none}
        tbody tr:hover td{background:var(--bg2)}
        .r{text-align:right} .nw{white-space:nowrap}
        .sm{font-size:12.5px;color:var(--muted)}
        .badge{display:inline-flex;align-items:center;gap:6px;padding:2px 10px;border-radius:99px;font-size:12.5px;font-weight:600;white-space:nowrap;background:var(--bg2);color:var(--muted)}
        .badge.win{background:var(--amber-soft);color:var(--amber)}
        .badge.top{background:var(--mint-soft);color:var(--mint)}
        .badge.mode{background:transparent;border:1px solid var(--line2);color:var(--ink);font-weight:500}
        .chests{display:flex;align-items:center;justify-content:flex-end;gap:10px}
        .chests .mini{width:70px;height:6px;border-radius:3px;background:var(--bg2);overflow:hidden}
        .chests .mini i{display:block;height:100%;background:var(--amber);border-radius:3px}
        .chests b{min-width:28px;text-align:right}
        .star{color:var(--rose);font-weight:600}
        .tlist{display:flex;flex-direction:column;gap:2px}
        tr.unusual td{background:var(--amber-soft)}
        .flag{display:flex;gap:12px;align-items:flex-start;margin-top:28px;padding:14px 18px;border:1px solid var(--amber);border-radius:14px;background:var(--amber-soft)}
        .flag .ic{color:var(--amber);font-size:18px;line-height:1.2}
        .empty{padding:28px 20px;text-align:center;color:var(--muted)}

        /* Carga */
        .loading{display:flex;gap:16px;align-items:center;background:var(--card);border:1px solid var(--cyan);border-radius:16px;padding:16px 18px;margin-bottom:22px;box-shadow:var(--shadow)}
        .spin{width:30px;height:30px;flex:none;border:3px solid var(--line2);border-top-color:var(--cyan);border-radius:50%;animation:spin .8s linear infinite}
        @keyframes spin{to{transform:rotate(360deg)}}
        .loading .grow{flex:1;min-width:0}
        .pbar{height:8px;background:var(--line);border-radius:4px;overflow:hidden;margin:8px 0 6px}
        .pbar i{display:block;height:100%;background:var(--cyan);border-radius:4px}

        /* Pie */
        footer{margin-top:36px;padding-top:18px;border-top:1px solid var(--line);color:var(--muted);font-size:12.5px;line-height:1.65}
        footer h3{margin:0 0 6px;font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:var(--muted)}
        footer p{margin:0 0 6px;max-width:860px}

        @media (max-width:860px){.kpis{grid-template-columns:repeat(2,minmax(0,1fr))}.grid2{grid-template-columns:1fr}.kpi.hero .v{font-size:52px}}
        @media (max-width:520px){main{padding:20px 16px 40px}.kpis{grid-template-columns:1fr 1fr}.kpi.hero{grid-row:auto}.kpi .v{font-size:24px}
          .kpi.hero .v{font-size:44px}th,td{padding:10px 12px}.chests .mini{display:none}.yaxis,.plot{height:150px}}
        @media (prefers-reduced-motion:reduce){.dot,.spin{animation:none}}
        """;

    private const string ChestIcon = """<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 10V8a4 4 0 0 1 4-4h8a4 4 0 0 1 4 4v2"/><rect x="3" y="10" width="18" height="10" rx="2"/><path d="M3 14h18"/><rect x="10" y="12" width="4" height="4" rx="1"/></svg>""";

    public static string Report(Config config, TrackerData data, IReadOnlyList<Profile> profiles, int initialChests = 0)
    {
        var progress = Progress;
        var matches = data.Matches.OrderByDescending(When).ToList();
        var countedChests = matches.Sum(m => m.ChestsOpened);
        var totalChests = countedChests + Math.Max(0, initialChests);
        var avg = matches.Count > 0 ? (double)countedChests / matches.Count : 0;
        var best = matches.Count > 0 ? matches.Max(m => m.ChestsOpened) : 0;
        var treasures = matches.Sum(m => m.TreasureFinds.Count);
        var sessionMatches = matches.Where(IsThisSession).ToList();
        var session = sessionMatches.Sum(m => m.ChestsOpened);
        var wins = matches.Count(m => m.Placement == 1);
        var player = matches.Select(m => m.PlayerName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "";
        string N(int n) => n.ToString("#,0", Loc);

        var sb = new StringBuilder();
        var title = L.T("Счётчик сундуков", "Chest Counter");
        var updated = L.T($"Обновлено {DateTime.Now.ToString("dd.MM.yyyy HH:mm", Loc)} · в реальном времени",
            $"Updated {DateTime.Now.ToString("MMM d, yyyy HH:mm", Loc)} · live");
        var heading = player.Length > 0 ? H(player) : title;
        sb.Append($"""
            <!doctype html><html lang="{L.Code}"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="dark light">
            <meta http-equiv="refresh" content="{(progress != null ? 2 : 15)}">
            <title>{title}{(player.Length > 0 ? " · " + H(player) : "")}</title><style>{Style}</style></head><body><main>
            <header class="masthead">
              <div class="brand"><div class="logo">{ChestIcon}</div>
                <div><div class="eyebrow">{(player.Length > 0 ? title : L.T("Отчёт", "Report"))}</div><h1>{heading}</h1></div></div>
              <div class="upd"><span class="dot"></span>{updated}</div>
            </header>
            {LoadingCard(progress)}{ProfilesLine(profiles, player)}
            """);

        // Cifras clave: el total destaca; al lado, el resto.
        var initialNote = initialChests > 0
            ? L.T($"Включая {N(initialChests)} (оценка) до начала подсчёта", $"Includes {N(initialChests)} estimated from before the tool")
            : L.T("Сундуки, открытые или разрушенные вами, по вашим реплеям", "Chests you opened or destroyed, from your replays");
        sb.Append($"""
            <div class="kpis">
              <div class="kpi hero">
                <div><div class="l">{L.T("Всего сундуков", "Total chests")}</div><div class="v">{N(totalChests)}</div><div class="s">{initialNote}</div></div>
                <div class="split">
                  <div><b>{N(session)}</b><span>{L.T("за эту сессию", "this session")}{(sessionMatches.Count > 0 ? " · " + L.Count(sessionMatches.Count, "матч", "матча", "матчей", "match", "matches") : "")}</span></div>
                  <div><b>{N(wins)}</b><span>{L.T(L.Plural(wins, "королевская победа", "королевские победы", "королевских побед"), wins == 1 ? "Victory Royale" : "Victory Royales")}</span></div>
                </div>
              </div>
              <div class="kpi"><div class="l">{L.T("Матчи", "Matches")}</div><div class="v">{N(matches.Count)}</div><div class="s">{L.T("разобрано", "analyzed")}</div></div>
              <div class="kpi"><div class="l">{L.T("Среднее", "Average")}</div><div class="v acc">{avg.ToString("0.0", Loc)}</div><div class="s">{L.T("сундуков за матч", "chests per match")}</div></div>
              <div class="kpi"><div class="l">{L.T("Рекорд", "Best match")}</div><div class="v">{N(best)}</div><div class="s">{L.T("сундуков в одном матче", "chests in one match")}</div></div>
              <div class="kpi"><div class="l">{L.T("Сокровища", "Treasures")}</div><div class="v ros">{N(treasures)}</div><div class="s">{L.T("найдено", "found")}</div></div>
            </div>
            """);

        // Últimos 30 días: una barra por día, con escala y la media diaria.
        var today = DateTime.Now.Date;
        var days = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29)).ToList();
        var perDay = days.Select(d => matches.Where(m => When(m).Date == d).Sum(m => m.ChestsOpened)).ToList();
        var top = NiceMax(perDay.Max());
        var sum30 = perDay.Sum();
        var activeDays = perDay.Count(v => v > 0);
        var dayAvg = activeDays > 0 ? (double)sum30 / activeDays : 0;
        sb.Append($"""
            <section><div class="sh"><h2>{L.T("Сундуки по дням", "Chests per day")}</h2>
            <span class="meta">{L.T("Последние 30 дней", "Last 30 days")} · {N(sum30)} {L.T(L.Plural(sum30, "сундук", "сундука", "сундуков"), "chests")} · {L.Count(activeDays, "игровой день", "игровых дня", "игровых дней", "day played", "days played")}</span></div>
            <div class="card pad"><div class="chart">
            <div class="yaxis"><span>{N(top)}</span><span>{N(top / 2)}</span><span>0</span></div>
            <div class="plot">
            """);
        if (dayAvg > 0)
        {
            var avgPct = 100.0 * dayAvg / top;
            sb.Append($"""<div class="avg" style="bottom:{avgPct.ToString("0.##", CultureInfo.InvariantCulture)}%"><span>{L.T("среднее", "avg")} {dayAvg.ToString("0", Loc)}</span></div>""");
        }
        for (var i = 0; i < days.Count; i++)
        {
            var v = perDay[i];
            var h = 100.0 * v / top;
            var tip = $"{days[i].ToString(L.En ? "ddd MMM d" : "ddd dd.MM", Loc)} · {L.Count(v, "сундук", "сундука", "сундуков", "chest", "chests")}";
            var hs = h.ToString("0.##", CultureInfo.InvariantCulture);
            var tipAt = Math.Max(h, 1.5).ToString("0.##", CultureInfo.InvariantCulture);
            sb.Append($"<div class=\"col{(i == days.Count - 1 ? " today" : "")}\" aria-label=\"{H(tip)}\"><span class=\"tip\" style=\"bottom:calc({tipAt}% + 6px)\">{H(tip)}</span><div class=\"bar{(v == 0 ? " zero" : "")}\" style=\"height:{hs}%\"></div></div>");
        }
        sb.Append("</div><div class=\"xaxis\">");
        for (var i = 0; i < days.Count; i++)
        {
            var label = i == days.Count - 1 ? $"<span class=\"t\">{L.T("сегодня", "today")}</span>"
                : (days.Count - 1 - i) % 7 == 0 ? $"<span>{days[i].ToString(L.En ? "MMM d" : "dd.MM", Loc)}</span>"
                : "<span></span>";
            sb.Append(label);
        }
        sb.Append("</div></div></div></section>");

        // Por tipo de cofre y, si hay tesoros, de dónde vienen: lado a lado.
        var byKind = matches.SelectMany(m => m.Chests).GroupBy(c => c.Kind).Select(g => (Kind: g.Key, Count: g.Count())).OrderByDescending(g => g.Count).ToList();
        var otherKinds = matches
            .SelectMany(m => m.OtherInteractions.Count > 0
                ? m.OtherInteractions
                : new Dictionary<string, int> { [ChestKinds.AmmoBox] = m.AmmoBoxesOpened, [ChestKinds.OtherContainer] = m.OtherContainersOpened })
            .GroupBy(kv => kv.Key)
            .Select(g => (Kind: g.Key, Count: g.Sum(kv => kv.Value)))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ToList();
        var kindTotal = Math.Max(1, byKind.Sum(k => k.Count));
        var kindMax = Math.Max(1, byKind.Select(k => k.Count).DefaultIfEmpty(0).Max());
        var treasureRows = matches.SelectMany(m => m.TreasureFinds.Select(f => (m, f))).ToList();

        sb.Append("<div class=\"grid2\" style=\"margin-top:28px\">");
        sb.Append($"<section><div class=\"sh\"><h2>{L.T("По типу сундука", "By chest type")}</h2><span class=\"meta\">{L.T("открыто", "opened")}</span></div><div class=\"card pad\"><div class=\"rows\">");
        if (byKind.Count == 0)
        {
            sb.Append($"<div class=\"empty\">{L.T("Сундуков пока нет.", "No chests recorded yet.")}</div>");
        }
        foreach (var (kind, count) in byKind)
        {
            var share = 100.0 * count / kindTotal;
            sb.Append($"<div class=\"row\"><span>{H(L.Name(kind))}</span><span class=\"n\">{N(count)}<small>{share.ToString("0", Loc)} %</small></span><div class=\"track\"><i style=\"width:{(100.0 * count / kindMax).ToString("0.##", CultureInfo.InvariantCulture)}%\"></i></div></div>");
        }
        var destroyedTotal = matches.Sum(m => m.Chests.Count(c => c.Destroyed));
        if (destroyedTotal > 0)
        {
            sb.Append($"<div class=\"row sub\"><span>↳ {L.T("из них разрушено, а не открыто", "of which destroyed instead of opened")}</span><span class=\"n\">{N(destroyedTotal)}</span></div>");
        }
        foreach (var (kind, count) in otherKinds)
        {
            sb.Append($"<div class=\"row sub\"><span>{H(L.Name(kind))} <span class=\"sm\">· {L.T("не входят в итог", "not in the total")}</span></span><span class=\"n\">{N(count)}</span></div>");
        }
        sb.Append("</div></div></section>");

        if (treasureRows.Count > 0)
        {
            var bySource = treasureRows.GroupBy(r => r.f.SourceLabel).OrderByDescending(g => g.Count()).ToList();
            var srcMax = Math.Max(1, bySource.Max(g => g.Count()));
            sb.Append($"<section><div class=\"sh\"><h2>{L.T("Откуда сокровища", "Where the treasures come from")}</h2><span class=\"meta\">{L.T("точно / всего", "certain / total")}</span></div><div class=\"card pad\"><div class=\"rows\">");
            foreach (var g in bySource)
            {
                var isChest = g.First().f.SourceIsChest;
                sb.Append($"<div class=\"row{(isChest ? "" : " warn")}\"><span>{(isChest ? "" : "⚑ ")}{H(g.Key)}</span><span class=\"n\">{g.Count(r => r.f.SourceCertain)}<small>/ {g.Count()}</small></span><div class=\"track\"><i style=\"width:{(100.0 * g.Count() / srcMax).ToString("0.##", CultureInfo.InvariantCulture)}%;{(isChest ? "background:var(--rose)" : "")}\"></i></div></div>");
            }
            sb.Append("</div></div></section>");
        }
        else
        {
            sb.Append($"<section><div class=\"sh\"><h2>{L.T("Сокровища", "Treasures")}</h2></div><div class=\"card\"><div class=\"empty\">{L.T("Сокровищ пока нет. Они появятся здесь со временем и сундуком, из которого выпали.", "No treasures yet. They will show up here with the time and the chest they came from.")}</div></div></section>");
        }
        sb.Append("</div>");

        // Tesoros, uno por fila.
        if (treasureRows.Count > 0)
        {
            var unusual = treasureRows.Where(r => !r.f.SourceIsChest).ToList();
            if (unusual.Count > 0)
            {
                var kinds = H(string.Join(", ", unusual.Select(r => L.Name(r.f.SourceKind)).Distinct()));
                sb.Append(L.En
                    ? $"<div class=\"flag\"><span class=\"ic\">⚑</span><div><b>{L.Count(unusual.Count, "", "", "", "treasure", "treasures")} obtained other than from a chest</b>: {kinds}. Highlighted in the table below.</div></div>"
                    : $"<div class=\"flag\"><span class=\"ic\">⚑</span><div><b>{L.Count(unusual.Count, "сокровище получено", "сокровища получено", "сокровищ получено", "", "")} не из сундука</b>: {kinds}. Выделены в таблице ниже.</div></div>");
            }

            sb.Append($"""
                <section><div class="sh"><h2>{L.T("Полученные сокровища", "Treasures obtained")}</h2><span class="meta">{L.Count(treasureRows.Count, "сокровище", "сокровища", "сокровищ", "treasure", "treasures")}</span></div>
                <div class="card tw"><table><thead><tr><th>{L.T("Матч", "Match")}</th><th>{L.T("Сокровище", "Treasure")}</th><th>{L.T("Время", "Time")}</th><th>{L.T("Источник", "Source")}</th></tr></thead><tbody>
                """);
            foreach (var (m, f) in treasureRows)
            {
                var id = f.PrizeId != null ? $"<div class=\"sm\">{H(f.PrizeId)}</div>" : (f.RawItem.StartsWith("WID_") ? $"<div class=\"sm\">{H(f.RawItem)}</div>" : "");
                var when = f.ObtainedAtSeconds is float t ? (f.TimeExact ? Pretty.Clock(t) : $"<span class=\"sm\">{L.T("около", "around")}</span> {Pretty.Clock(t)}") : "<span class=\"muted\">—</span>";
                var source = $"{(f.SourceIsChest ? "" : "⚑ ")}{H(f.SourceLabel)}"
                    + (f.SourceCertain ? "" : $" <span class=\"sm\">({Pretty.Likely})</span>")
                    + (!f.SourceIsChest && !string.IsNullOrEmpty(f.SourceBlueprint) ? $"<div class=\"sm\">{H(f.SourceBlueprint)}</div>" : "")
                    + (f.AlsoPossible.Count > 0 ? $"<div class=\"sm\">{L.T("также возможно: ", "also possible: ")}{H(string.Join(", ", f.AlsoPossible.Select(L.Name)))}</div>" : "");
                sb.Append($"<tr{(f.SourceIsChest ? "" : " class=\"unusual\"")}><td class=\"nw\">{L.DateTimeShort(When(m))}</td><td><span class=\"star\">{(f.FromGoldenChest ? "★★" : "★")} {H(f.DisplayName)}</span>{id}</td><td class=\"nw num\">{when}</td><td>{source}</td></tr>");
            }
            sb.Append("</tbody></table></div></section>");
        }

        // Partidas: los tesoros solo aparecen cuando salió alguno.
        var shown = matches.Take(100).ToList();
        var shownMax = Math.Max(1, shown.Select(m => m.ChestsOpened).DefaultIfEmpty(0).Max());
        sb.Append($"""
            <section><div class="sh"><h2>{L.T("Последние матчи", "Latest matches")}</h2><span class="meta">{(matches.Count > shown.Count ? L.T($"последние {shown.Count} из {N(matches.Count)}", $"latest {shown.Count} of {N(matches.Count)}") : L.Count(matches.Count, "матч", "матча", "матчей", "match", "matches"))}</span></div>
            <div class="card tw"><table><thead><tr><th>{L.T("Дата", "Date")}</th><th>{L.T("Режим", "Mode")}</th><th>{L.T("Место", "Placement")}</th><th class="r">{L.T("Сундуки", "Chests")}</th><th>{L.T("Сокровища", "Treasures")}</th></tr></thead><tbody>
            """);
        if (shown.Count == 0)
        {
            sb.Append($"<tr><td colspan=\"5\" class=\"empty\">{L.T("Разобранных матчей пока нет. Сыграйте один, и он появится здесь.", "No matches analyzed yet. Play one and it will show up here.")}</td></tr>");
        }
        foreach (var m in shown)
        {
            var treasure = m.TreasureFinds.Count == 0 ? "<span class=\"muted\">—</span>"
                : "<div class=\"tlist\">" + string.Concat(m.TreasureFinds.Select(f => $"<span class=\"star\">{(f.FromGoldenChest ? "★★" : "★")} {H(f.DisplayName)}</span>")) + "</div>";
            var placeClass = m.Placement switch { 1 => " win", > 0 and <= 10 => " top", _ => "" };
            var placeText = m.Placement == 1 ? "🏆 " + H(Pretty.Placement(m)) : H(Pretty.Placement(m));
            var barW = (100.0 * m.ChestsOpened / shownMax).ToString("0.##", CultureInfo.InvariantCulture);
            sb.Append($"<tr><td class=\"nw\">{L.DateTimeShort(When(m))}</td><td><span class=\"badge mode\">{H(Pretty.Playlist(m.Playlist))}</span></td><td><span class=\"badge{placeClass}\">{placeText}</span></td><td><div class=\"chests\"><span class=\"mini\"><i style=\"width:{barW}%\"></i></span><b>{m.ChestsOpened}</b></div></td><td>{treasure}</td></tr>");
        }
        sb.Append("</tbody></table></div></section>");

        sb.Append(L.T(
            """
            <footer><h3>Как считается</h3>
            <p>Считаются сундуки, которые вы открыли или разрушили сами, по вашим файлам реплеев (разбираются после каждого матча). Ящики с патронами и едой не считаются.</p>
            <p><span class="star">★★</span> золотой сундук охоты за сокровищами (косметика, доступ к магазину…), определён точно. <span class="star">★</span> сокровище из одного из ваших сундуков (точное время), с названием, если предмет удалось определить.</p>
            <p>Названия призов можно поменять в <code>treasures.json</code>. Страница обновляется сама.</p></footer>
            </main></body></html>
            """,
            """
            <footer><h3>How it counts</h3>
            <p>Counts the chests you opened or destroyed yourself, from your replay files (analyzed after each match). Ammo boxes and food boxes are not counted.</p>
            <p><span class="star">★★</span> golden treasure hunt chest (cosmetic, shop access…), detected for sure. <span class="star">★</span> treasure received from one of your own chests (exact moment), named when the item can be identified.</p>
            <p>Prize names can be edited in <code>treasures.json</code>. This page refreshes by itself.</p></footer>
            </main></body></html>
            """));
        return sb.ToString();
    }

    /// <summary>Tope "redondo" para la escala del gráfico (1, 2, 2.5, 5 × 10ⁿ), siempre ≥ el valor.</summary>
    private static int NiceMax(int value)
    {
        if (value <= 4)
        {
            return 4;
        }
        var mag = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var step in new[] { 1, 2, 2.5, 5, 10 })
        {
            if (step * mag >= value)
            {
                var nice = (int)Math.Round(step * mag);
                return nice % 2 == 0 ? nice : nice + 1;
            }
        }
        return value;
    }

    private static string LoadingCard(ScanProgress? p)
    {
        if (p == null)
        {
            return "";
        }
        var pct = p.Total > 0 ? (int)Math.Round(100.0 * p.Done / p.Total) : 0;
        var head = L.T($"Разбираю реплеи… {p.Done} / {p.Total}", $"Analyzing your replays… {p.Done} / {p.Total}");
        var note = L.T("Страница обновляется сама: цифры ниже растут по мере чтения реплеев.",
            "This page updates by itself: the numbers below grow as the replays are read.");
        return $"""<div class="loading"><div class="spin"></div><div class="grow"><b>{head}</b><div class="pbar"><i style="width:{pct}%"></i></div><span class="muted">{note}</span></div></div>""";
    }

    private static string ProfilesLine(IReadOnlyList<Profile> profiles, string player)
    {
        var others = profiles.Where(p => p.Name != player).Select(p => H(p.Name)).ToList();
        if (others.Count == 0)
        {
            return "";
        }
        return $"""<div class="profiles">{L.T("Другие профили на этом ПК: ", "Other profiles on this PC: ")}{string.Join(", ", others)} · {L.T("профиль меняется в настройках программы", "switch profile from the tool's settings")}</div>""";
    }

    /// <summary>Rewrites only the OBS overlay page (called on every live update).</summary>
    public static void WriteOverlay(Store store, OverlayView view)
    {
        try
        {
            WriteAtomic(store.OverlayPath, Overlay(view));
        }
        catch
        {
            // the page is a convenience: never stop the tracker for it
        }
    }

    private const string OverlayStyle = """
        html,body{margin:0;background:transparent;color:#e6eaf2;font:500 16px/1.25 "Segoe UI",system-ui,sans-serif}
        .box{position:relative;display:inline-block;min-width:230px;padding:10px 14px 10px 18px;border-radius:14px;overflow:hidden;
          background:rgba(15,19,27,.9);border:1px solid #2a3344;box-shadow:0 6px 24px rgba(0,0,0,.35)}
        .box:before{content:"";position:absolute;left:0;top:0;bottom:0;width:4px;background:#ffb84d}
        .box.live:before{background:#3ddc97}
        .head{display:flex;align-items:center;justify-content:space-between;gap:16px;font:700 12px "Segoe UI",sans-serif;letter-spacing:.12em;color:#7a8496}
        .pill{display:inline-flex;align-items:center;gap:6px;padding:3px 10px;border-radius:99px;background:#143a2c;color:#3ddc97;letter-spacing:.06em}
        .pill:before{content:"";width:8px;height:8px;border-radius:50%;background:#3ddc97;animation:pulse 1.6s ease-in-out infinite}
        @keyframes pulse{50%{opacity:.35}}
        .main{display:flex;align-items:center;justify-content:space-between;gap:16px;margin:6px 0 8px}
        .big{display:block;font-size:34px;font-weight:800;line-height:1.05;font-variant-numeric:tabular-nums}
        .total .big{color:#ffb84d}
        .cap{display:block;font-size:11px;font-weight:700;letter-spacing:.1em;color:#7a8496;text-transform:uppercase}
        .chip{min-width:84px;padding:5px 12px;border-radius:11px;background:#1a202d;text-align:center}
        .chip.live .big,.chip.live .cap{color:#3ddc97}.chip.none .big{color:#505868}
        .stats{display:flex;justify-content:space-between;gap:14px;border-top:1px solid #222938;padding-top:7px}
        .stat .cap{font-size:10px}.stat b{display:block;font-size:16px;font-weight:700}
        .stat.t b{color:#ff80aa}.stat small{display:block;font-size:11px;font-weight:600;color:#7a8496}
        """;

    /// <summary>The overlay card (shared by overlay.html and the OBS page served by the tool).</summary>
    public static string OverlayBox(OverlayView v)
    {
        var stats = new StringBuilder();
        foreach (var r in v.Rows)
        {
            stats.Append($"<div class=\"stat{(r.Treasure ? " t" : "")}\"><span class=\"cap\">{H(r.Label)}</span><b>{H(r.Value)}</b></div>");
        }
        var badge = v.Badge != null ? $"<span class=\"pill\">{H(v.Badge.TrimStart('●', ' '))}</span>" : "";
        var chip = v.Current == null ? "none" : v.CurrentLive ? "live" : "";
        return $"""
            <div class="box{(v.CurrentLive ? " live" : "")}">
            <div class="head"><span>{L.T("СУНДУКИ", "CHESTS")}</span>{badge}</div>
            <div class="main">
              <div class="total"><span class="big">{H(v.Total)}</span><span class="cap">{L.T("всего", "total")}</span></div>
              <div class="chip {chip}"><span class="big">{H(v.Current ?? "—")}</span><span class="cap">{H(v.CurrentCaption)}</span></div>
            </div>
            <div class="stats">{stats}</div>
            </div>
            """;
    }

    /// <summary>overlay.html (a file: reloads itself every 10 s).</summary>
    public static string Overlay(OverlayView v) => $"""
        <!doctype html><html lang="{L.Code}"><head><meta charset="utf-8"><meta http-equiv="refresh" content="10">
        <title>{L.T("Оверлей сундуков", "Chest overlay")}</title><style>{OverlayStyle}</style></head>
        <body>{OverlayBox(v)}</body></html>
        """;

    /// <summary>OBS page served by the tool: fetches the card every 2 s and swaps it only when it changed (no flicker).</summary>
    public static string OverlayLivePage(string box) => $$"""
        <!doctype html><html lang="{{L.Code}}"><head><meta charset="utf-8">
        <title>{{L.T("Оверлей сундуков", "Chest overlay")}}</title><style>{{OverlayStyle}}</style></head>
        <body><div id="o">{{box}}</div>
        <script>
        let last = document.getElementById("o").innerHTML;
        setInterval(async () => {
          try {
            const r = await fetch("/view", { cache: "no-store" });
            if (!r.ok) return;
            const html = await r.text();
            if (html !== last) { document.getElementById("o").innerHTML = html; last = html; }
          } catch (e) { /* tool closed: keep the last figures */ }
        }, 2000);
        </script></body></html>
        """;

    public static string Csv(TrackerData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine(L.T("дата;режим;игрок;место;игроков;сундуки;ящики_патронов;сокровища;файл",
            "date;mode;player;placement;players;chests;ammo_boxes;treasures;file"));

        foreach (var m in data.Matches.OrderBy(When))
        {
            var t = string.Join(" + ", m.TreasureFinds.Select(f => f.DisplayName));
            sb.AppendLine(string.Join(";",
                When(m).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Pretty.Playlist(m.Playlist), m.PlayerName, m.Placement, m.TotalPlayers,
                m.ChestsOpened, m.AmmoBoxesOpened, t, m.ReplayFile));
        }
        return sb.ToString();
    }
}
