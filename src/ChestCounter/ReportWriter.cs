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

    // Same look as the window: near-black, stepped pixel corners, dot-matrix figures, grain, one lavender accent. Dark only.
    private const string Style = """
        :root{--bg:#09090b;--panel:#0f0f12;--panel-2:#131317;--line:#2a2a31;--line-hi:#3a3a44;--text:#ececf1;--muted:#85858f;--dim:#55555e;
          --accent:#b9a8ff;--accent-deep:#7a68d6;--accent-bg:rgba(150,128,255,.16);--warn:#e2683a;--warn-bg:rgba(226,104,58,.12);--bar:#4a4a55;
          --dot-off:rgba(255,255,255,.055);--s:3px;
          --notch:polygon(0 calc(var(--s)*2),var(--s) calc(var(--s)*2),var(--s) var(--s),calc(var(--s)*2) var(--s),calc(var(--s)*2) 0,
            calc(100% - var(--s)*2) 0,calc(100% - var(--s)*2) var(--s),calc(100% - var(--s)) var(--s),calc(100% - var(--s)) calc(var(--s)*2),100% calc(var(--s)*2),
            100% calc(100% - var(--s)*2),calc(100% - var(--s)) calc(100% - var(--s)*2),calc(100% - var(--s)) calc(100% - var(--s)),calc(100% - var(--s)*2) calc(100% - var(--s)),calc(100% - var(--s)*2) 100%,
            calc(var(--s)*2) 100%,calc(var(--s)*2) calc(100% - var(--s)),var(--s) calc(100% - var(--s)),var(--s) calc(100% - var(--s)*2),0 calc(100% - var(--s)*2));
          --rose:var(--accent);color-scheme:dark}
        *{box-sizing:border-box}
        html{background:var(--bg)}
        body{margin:0;background:var(--bg);color:var(--text);font:14px/1.5 Inter,"Segoe UI Variable Text","Segoe UI",system-ui,sans-serif;-webkit-font-smoothing:antialiased}
        body::after{content:"";position:fixed;inset:0;pointer-events:none;z-index:50;opacity:.13;mix-blend-mode:overlay;
          background-image:url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='160' height='160'><filter id='n'><feTurbulence type='fractalNoise' baseFrequency='.9' numOctaves='2' stitchTiles='stitch'/><feColorMatrix values='0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  0 0 0 .9 0'/></filter><rect width='100%' height='100%' filter='url(%23n)'/></svg>")}
        .dust{position:fixed;inset:0;pointer-events:none;z-index:0}.dust i{position:absolute;width:2px;height:2px;background:#2c2c33}.dust i.l{background:#3b3550}
        main{position:relative;z-index:1;max-width:1120px;margin:0 auto;padding:28px 16px 56px}
        .num{font-variant-numeric:tabular-nums}
        .muted{color:var(--muted)}

        /* Pixel panels: border colour outside, fill 1px inside, both cut by the same stepped outline */
        .kpi,.card,.flag,.loading,.upd,.badge,.logo,.tip{position:relative;clip-path:var(--notch);background:var(--line);isolation:isolate;border:0}
        .kpi::before,.card::before,.flag::before,.loading::before,.upd::before,.badge::before,.logo::before,.tip::before{content:"";position:absolute;inset:1px;clip-path:var(--notch);background:var(--panel);z-index:-1}

        /* Dot-matrix figures */
        .dots{--d:4px;display:inline-flex;gap:calc(var(--d) + 1px);vertical-align:-.08em}
        .dots .dg{display:grid;grid-template-columns:repeat(var(--c),var(--d));grid-auto-rows:var(--d);gap:1px}
        .dots i{background:var(--dot-off)}.dots i.on{background:currentColor}
        .dots .sp{width:calc(var(--d)*2)}

        /* Header */
        .masthead{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:14px 24px;padding-bottom:20px;margin-bottom:24px;border-bottom:1px solid var(--line)}
        .brand{display:flex;align-items:center;gap:14px;min-width:0}
        .logo{width:48px;height:48px;flex:none;display:grid;place-items:center;color:var(--accent)}
        .logo svg{shape-rendering:crispEdges}
        .eyebrow{display:flex;align-items:center;gap:8px;font-size:11px;font-weight:700;letter-spacing:.18em;text-transform:uppercase;color:var(--muted)}
        .eyebrow::before,h2::before,footer h3::before{content:"";width:6px;height:6px;background:var(--accent);flex:none}
        h1{margin:2px 0 0;font-size:24px;line-height:1.2;font-weight:700;letter-spacing:-.01em;overflow-wrap:anywhere}
        .upd{display:inline-flex;align-items:center;gap:9px;padding:6px 12px;color:var(--muted);font-size:12.5px}
        .upd::before{background:var(--panel-2)}
        .dot{width:6px;height:6px;background:var(--accent);animation:blink 1.6s steps(2,jump-none) infinite}
        @keyframes blink{50%{opacity:.25}}
        .profiles{margin:-12px 0 22px;font-size:13px;color:var(--muted)}

        /* Key figures */
        .kpis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:8px}
        .kpi{padding:16px 18px;min-width:0}
        .kpi .l{font-size:11px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:var(--muted)}
        .kpi .v{margin-top:10px;line-height:1;color:var(--text)}
        .kpi .s{margin-top:8px;font-size:12.5px;color:var(--muted)}
        .kpi.hero{grid-column:span 2;grid-row:span 2;display:flex;flex-direction:column;justify-content:space-between;overflow:hidden}
        .kpi.hero .v{color:var(--accent);margin-top:16px}.kpi.hero .v .dots{--d:9px}
        .kpi.hero .s{font-size:13.5px}
        .kpi .acc,.kpi .ros,.kpi .mnt{color:var(--text)}
        .split{display:flex;gap:28px;margin-top:18px;padding-top:14px;border-top:1px solid var(--line)}
        .split b{display:block;font-size:20px;font-variant-numeric:tabular-nums}
        .split span{font-size:12px;color:var(--muted)}

        /* Sections */
        section{margin-top:28px}
        .sh{display:flex;flex-wrap:wrap;align-items:baseline;justify-content:space-between;gap:4px 16px;margin:0 0 10px}
        h2{display:flex;align-items:center;gap:9px;margin:0;font-size:15px;font-weight:700}
        .sh .meta{font-size:12.5px;color:var(--muted)}
        .pad{padding:18px 20px}
        .grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px 16px}
        .grid2 section{margin-top:0}

        /* 30-day chart: columns of pixel blocks */
        .chart{position:relative;display:grid;grid-template-columns:auto 1fr;gap:0 10px}
        .yaxis{display:flex;flex-direction:column;justify-content:space-between;height:200px;font-size:11px;color:var(--dim);text-align:right;font-variant-numeric:tabular-nums}
        .yaxis span{transform:translateY(-50%)} .yaxis span:last-child{transform:translateY(50%)}
        .plot{position:relative;height:200px;display:flex;align-items:flex-end;gap:3px;border-bottom:1px solid var(--line-hi)}
        .avg{position:absolute;left:0;right:0;z-index:2;border-top:1px dashed var(--accent-deep);pointer-events:none}
        .avg span{position:absolute;left:0;top:-19px;font-size:11px;font-weight:700;letter-spacing:.06em;color:var(--accent);background:var(--panel);padding:0 4px}
        .col{flex:1;min-width:4px;height:100%;display:flex;align-items:flex-end;position:relative;
          background:repeating-linear-gradient(to top,var(--dot-off) 0 4px,transparent 4px 6px)}
        .bar{width:100%;background:repeating-linear-gradient(to top,var(--bar) 0 4px,transparent 4px 6px)}
        .col.today .bar{background:repeating-linear-gradient(to top,var(--accent) 0 4px,transparent 4px 6px)}
        .bar.zero{height:0!important}
        .col:hover .bar{filter:brightness(1.35)}
        .tip{position:absolute;left:50%;transform:translateX(-50%);white-space:nowrap;color:var(--text);
          padding:4px 9px;font-size:12px;font-weight:600;opacity:0;pointer-events:none;z-index:3;background:var(--line-hi)}
        .tip::before{background:var(--panel-2)}
        .col:nth-child(-n+4) .tip{left:0;transform:none}
        .col:nth-last-child(-n+4) .tip{left:auto;right:0;transform:none}
        .col:hover .tip{opacity:1}
        .xaxis{grid-column:2;display:flex;gap:3px;margin-top:8px;font-size:11px;color:var(--dim)}
        .xaxis span{flex:1;min-width:4px;text-align:center;white-space:nowrap;overflow:visible}
        .xaxis span:empty{visibility:hidden}
        .xaxis .t{color:var(--accent);font-weight:700}

        /* Lists with a segmented share bar */
        .rows{display:flex;flex-direction:column}
        .row{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:6px 12px;padding:10px 0;border-bottom:1px solid var(--line)}
        .row:last-child{border-bottom:none}
        .row .n{font-weight:700;font-variant-numeric:tabular-nums;text-align:right}
        .row .n small{font-weight:500;color:var(--muted);margin-left:6px}
        .track{grid-column:1/-1;height:6px;background:repeating-linear-gradient(90deg,var(--dot-off) 0 6px,transparent 6px 8px)}
        .track i{display:block;height:100%;background:repeating-linear-gradient(90deg,var(--accent) 0 6px,transparent 6px 8px)!important}
        .row.sub{color:var(--muted)} .row.sub .n{color:var(--muted);font-weight:600}
        .row.warn .track i{background:repeating-linear-gradient(90deg,var(--warn) 0 6px,transparent 6px 8px)!important}
        .note{margin:12px 0 0;font-size:12.5px;color:var(--muted)}

        /* Tables */
        .tw{overflow-x:auto}
        table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}
        th,td{text-align:left;padding:10px 16px;border-bottom:1px solid var(--line);vertical-align:middle}
        th{position:sticky;top:0;background:var(--panel-2);font-size:11px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:var(--muted);white-space:nowrap}
        tbody tr:last-child td{border-bottom:none}
        tbody tr:hover td{background:var(--panel-2)}
        .r{text-align:right} .nw{white-space:nowrap}
        .sm{font-size:12.5px;color:var(--muted)}
        .badge{--s:2px;display:inline-flex;align-items:center;gap:6px;padding:2px 9px;font-size:11.5px;font-weight:600;white-space:nowrap;color:var(--muted);background:var(--line-hi)}
        .badge::before{background:var(--panel-2)}
        .badge.win{color:var(--accent);background:var(--accent-deep)}.badge.win::before{background:#221e36}
        .badge.top{color:var(--text)}
        .badge.mode{color:var(--text);font-weight:500;background:var(--line)}.badge.mode::before{background:var(--panel)}
        .chests{display:flex;align-items:center;justify-content:flex-end;gap:10px}
        .chests .mini{width:70px;height:6px;background:repeating-linear-gradient(90deg,var(--dot-off) 0 4px,transparent 4px 5px)}
        .chests .mini i{display:block;height:100%;background:repeating-linear-gradient(90deg,var(--bar) 0 4px,transparent 4px 5px)}
        .chests b{min-width:28px;text-align:right}
        .star{color:var(--accent);font-weight:600}
        .tlist{display:flex;flex-direction:column;gap:2px}
        tr.unusual td{background:var(--warn-bg)}
        .flag{display:flex;gap:12px;align-items:flex-start;margin-top:28px;padding:14px 18px;background:var(--warn)}
        .flag::before{background:#1a110d}
        .flag .ic{color:var(--warn);font-size:18px;line-height:1.2}
        .empty{padding:28px 20px;text-align:center;color:var(--muted)}

        /* Loading */
        .loading{display:flex;gap:16px;align-items:center;padding:16px 18px;margin-bottom:22px;background:var(--accent-deep)}
        .spin{display:grid;grid-template-columns:repeat(3,6px);gap:3px;flex:none}
        .spin::before,.spin::after,.spin i{content:"";width:6px;height:6px;background:var(--accent);animation:blink 1s steps(2,jump-none) infinite}
        .spin::after{animation-delay:.33s}.spin i{animation-delay:.66s}
        .loading .grow{flex:1;min-width:0}
        .pbar{height:8px;margin:8px 0 6px;background:repeating-linear-gradient(90deg,var(--dot-off) 0 6px,transparent 6px 8px)}
        .pbar i{display:block;height:100%;background:repeating-linear-gradient(90deg,var(--accent) 0 6px,transparent 6px 8px)}

        /* Footer */
        footer{margin-top:36px;padding-top:18px;border-top:1px solid var(--line);color:var(--muted);font-size:12.5px;line-height:1.65}
        footer h3{display:flex;align-items:center;gap:9px;margin:0 0 6px;font-size:11px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:var(--muted)}
        footer p{margin:0 0 6px;max-width:860px}
        code{font:12px Consolas,"Cascadia Mono",monospace;color:var(--text)}

        @media (max-width:860px){.kpis{grid-template-columns:repeat(2,minmax(0,1fr))}.grid2{grid-template-columns:1fr}.kpi.hero .v .dots{--d:7px}}
        @media (max-width:520px){main{padding:20px 16px 40px}.kpis{grid-template-columns:1fr 1fr}.kpi.hero{grid-row:auto}.kpi .v .dots{--d:3px}
          .kpi.hero .v .dots{--d:5px}th,td{padding:9px 12px}.chests .mini{display:none}.yaxis,.plot{height:150px}}
        @media (prefers-reduced-motion:reduce){.dot,.spin::before,.spin::after,.spin i{animation:none}}
        """;

    private const string ChestIcon = """<svg width="30" height="23" viewBox="0 0 12 9" fill="currentColor" aria-hidden="true"><rect x="2" y="0" width="1" height="1"/><rect x="3" y="0" width="1" height="1"/><rect x="4" y="0" width="1" height="1"/><rect x="5" y="0" width="1" height="1"/><rect x="6" y="0" width="1" height="1"/><rect x="7" y="0" width="1" height="1"/><rect x="8" y="0" width="1" height="1"/><rect x="9" y="0" width="1" height="1"/><rect x="1" y="1" width="1" height="1"/><rect x="10" y="1" width="1" height="1"/><rect x="0" y="2" width="1" height="1"/><rect x="11" y="2" width="1" height="1"/><rect x="0" y="3" width="1" height="1"/><rect x="1" y="3" width="1" height="1"/><rect x="2" y="3" width="1" height="1"/><rect x="3" y="3" width="1" height="1"/><rect x="4" y="3" width="1" height="1"/><rect x="5" y="3" width="1" height="1"/><rect x="6" y="3" width="1" height="1"/><rect x="7" y="3" width="1" height="1"/><rect x="8" y="3" width="1" height="1"/><rect x="9" y="3" width="1" height="1"/><rect x="10" y="3" width="1" height="1"/><rect x="11" y="3" width="1" height="1"/><rect x="0" y="4" width="1" height="1"/><rect x="5" y="4" width="1" height="1"/><rect x="6" y="4" width="1" height="1"/><rect x="11" y="4" width="1" height="1"/><rect x="0" y="5" width="1" height="1"/><rect x="5" y="5" width="1" height="1"/><rect x="6" y="5" width="1" height="1"/><rect x="11" y="5" width="1" height="1"/><rect x="0" y="6" width="1" height="1"/><rect x="11" y="6" width="1" height="1"/><rect x="0" y="7" width="1" height="1"/><rect x="11" y="7" width="1" height="1"/><rect x="0" y="8" width="1" height="1"/><rect x="1" y="8" width="1" height="1"/><rect x="2" y="8" width="1" height="1"/><rect x="3" y="8" width="1" height="1"/><rect x="4" y="8" width="1" height="1"/><rect x="5" y="8" width="1" height="1"/><rect x="6" y="8" width="1" height="1"/><rect x="7" y="8" width="1" height="1"/><rect x="8" y="8" width="1" height="1"/><rect x="9" y="8" width="1" height="1"/><rect x="10" y="8" width="1" height="1"/><rect x="11" y="8" width="1" height="1"/></svg>""";

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
            <title>{title}{(player.Length > 0 ? " · " + H(player) : "")}</title><style>{Style}</style></head><body>{Dust()}<main>
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
                <div><div class="l">{L.T("Всего сундуков", "Total chests")}</div><div class="v">{DotFont.Html(totalChests.ToString(CultureInfo.InvariantCulture))}</div><div class="s">{initialNote}</div></div>
                <div class="split">
                  <div><b>{N(session)}</b><span>{L.T("за эту сессию", "this session")}{(sessionMatches.Count > 0 ? " · " + L.Count(sessionMatches.Count, "матч", "матча", "матчей", "match", "matches") : "")}</span></div>
                  <div><b>{N(wins)}</b><span>{L.T(L.Plural(wins, "королевская победа", "королевские победы", "королевских побед"), wins == 1 ? "Victory Royale" : "Victory Royales")}</span></div>
                </div>
              </div>
              <div class="kpi"><div class="l">{L.T("Матчи", "Matches")}</div><div class="v">{DotFont.Html(matches.Count.ToString(CultureInfo.InvariantCulture))}</div><div class="s">{L.T("разобрано", "analyzed")}</div></div>
              <div class="kpi"><div class="l">{L.T("Среднее", "Average")}</div><div class="v acc">{DotFont.Html(avg.ToString("0.0", Loc))}</div><div class="s">{L.T("сундуков за матч", "chests per match")}</div></div>
              <div class="kpi"><div class="l">{L.T("Рекорд", "Best match")}</div><div class="v">{DotFont.Html(best.ToString(CultureInfo.InvariantCulture))}</div><div class="s">{L.T("сундуков в одном матче", "chests in one match")}</div></div>
              <div class="kpi"><div class="l">{L.T("Сокровища", "Treasures")}</div><div class="v ros">{DotFont.Html(treasures.ToString(CultureInfo.InvariantCulture))}</div><div class="s">{L.T("найдено", "found")}</div></div>
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
            var placeText = H(Pretty.Placement(m));
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

    /// <summary>A few 2×2 specks at fixed spots behind the page (the same every time).</summary>
    private static string Dust()
    {
        var rnd = new Random(11);
        var sb = new StringBuilder("<div class=\"dust\" aria-hidden=\"true\">");
        for (var i = 0; i < 26; i++)
        {
            var x = (rnd.NextDouble() * 100).ToString("0.#", CultureInfo.InvariantCulture);
            var y = (rnd.NextDouble() * 100).ToString("0.#", CultureInfo.InvariantCulture);
            sb.Append($"<i{(i % 3 == 0 ? " class=\"l\"" : "")} style=\"left:{x}%;top:{y}%\"></i>");
        }
        return sb.Append("</div>").ToString();
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
        return $"""<div class="loading"><div class="spin"><i></i></div><div class="grow"><b>{head}</b><div class="pbar"><i style="width:{pct}%"></i></div><span class="muted">{note}</span></div></div>""";
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
        html,body{margin:0;background:transparent;color:#ececf1;font:500 16px/1.25 Inter,"Segoe UI",system-ui,sans-serif}
        .box{--s:3px;position:relative;display:inline-block;min-width:230px;padding:11px 15px 11px;isolation:isolate;background:#2a2a31;
          clip-path:polygon(0 6px,3px 6px,3px 3px,6px 3px,6px 0,calc(100% - 6px) 0,calc(100% - 6px) 3px,calc(100% - 3px) 3px,calc(100% - 3px) 6px,100% 6px,100% calc(100% - 6px),calc(100% - 3px) calc(100% - 6px),calc(100% - 3px) calc(100% - 3px),calc(100% - 6px) calc(100% - 3px),calc(100% - 6px) 100%,6px 100%,6px calc(100% - 3px),3px calc(100% - 3px),3px calc(100% - 6px),0 calc(100% - 6px))}
        .box::before{content:"";position:absolute;inset:1px;z-index:-1;background:rgba(15,15,18,.94);clip-path:inherit}
        .head{display:flex;align-items:center;justify-content:space-between;gap:16px;font:700 11px Inter,"Segoe UI",sans-serif;letter-spacing:.18em;color:#85858f}
        .head>span:first-child{display:flex;align-items:center;gap:8px}.head>span:first-child::before{content:"";width:6px;height:6px;background:#b9a8ff}
        .pill{display:inline-flex;align-items:center;gap:7px;padding:3px 9px;color:#b9a8ff;letter-spacing:.08em;background:#221e36;box-shadow:inset 0 0 0 1px #7a68d6;
          clip-path:polygon(0 2px,2px 2px,2px 0,calc(100% - 2px) 0,calc(100% - 2px) 2px,100% 2px,100% calc(100% - 2px),calc(100% - 2px) calc(100% - 2px),calc(100% - 2px) 100%,2px 100%,2px calc(100% - 2px),0 calc(100% - 2px))}
        .pill::before{content:"";width:6px;height:6px;background:#b9a8ff;animation:blink 1.6s steps(2,jump-none) infinite}
        @keyframes blink{50%{opacity:.25}}
        .main{display:flex;align-items:center;justify-content:space-between;gap:16px;margin:9px 0 9px}
        .big{display:block;line-height:1;color:#ececf1}
        .total .big{color:#b9a8ff}
        .dots{--d:4px;display:inline-flex;gap:calc(var(--d) + 1px)}
        .dots .dg{display:grid;grid-template-columns:repeat(var(--c),var(--d));grid-auto-rows:var(--d);gap:1px}
        .dots i{background:rgba(255,255,255,.055)}.dots i.on{background:currentColor}.dots .sp{width:calc(var(--d)*2)}
        .cap{display:block;margin-top:6px;font-size:10px;font-weight:700;letter-spacing:.16em;color:#85858f;text-transform:uppercase}
        .chip{position:relative;isolation:isolate;min-width:84px;padding:7px 12px;text-align:center;background:#2a2a31;
          clip-path:polygon(0 4px,2px 4px,2px 2px,4px 2px,4px 0,calc(100% - 4px) 0,calc(100% - 4px) 2px,calc(100% - 2px) 2px,calc(100% - 2px) 4px,100% 4px,100% calc(100% - 4px),calc(100% - 2px) calc(100% - 4px),calc(100% - 2px) calc(100% - 2px),calc(100% - 4px) calc(100% - 2px),calc(100% - 4px) 100%,4px 100%,4px calc(100% - 2px),2px calc(100% - 2px),2px calc(100% - 4px),0 calc(100% - 4px))}
        .chip::before{content:"";position:absolute;inset:1px;z-index:-1;background:#131317;clip-path:inherit}
        .chip.live .big,.chip.live .cap{color:#b9a8ff}.chip.none .big{color:#55555e}
        .stats{display:flex;justify-content:space-between;gap:14px;border-top:1px solid #1f1f25;padding-top:8px}
        .stat .cap{font-size:9px;margin:0}.stat b{display:block;font-size:16px;font-weight:700}
        .stat small{display:block;font-size:11px;font-weight:600;color:#85858f}
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
              <div class="total"><span class="big">{DotFont.Html(v.Total)}</span><span class="cap">{L.T("всего", "total")}</span></div>
              <div class="chip {chip}"><span class="big">{DotFont.Html(v.Current ?? "—")}</span><span class="cap">{H(v.CurrentCaption)}</span></div>
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
