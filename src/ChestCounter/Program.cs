using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using ChestCounter;

/// <summary>
/// Punto de entrada. Main lleva [STAThread]: Windows exige un hilo STA para el portapapeles y los diálogos
/// (con instrucciones de nivel superior el hilo principal arrancaba en MTA y "Copiar" fallaba en silencio).
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // --standby: el proceso invisible que espera a que arranque Fortnite ("abrir con Fortnite"). Sin consola ni ventana.
        if (args.Contains("--standby"))
        {
            var dataIndex = Array.IndexOf(args, "--data");
            return OperatingSystem.IsWindows()
                ? FortniteWatch.RunStandby(dataIndex >= 0 && dataIndex + 1 < args.Length ? args[dataIndex + 1] : Store.DefaultFolder())
                : 1;
        }

        // Estilo de Windows para los controles: debe fijarse antes de crear la primera ventana (también los mensajes).
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Las opciones de línea de comandos escriben en la terminal que lanzó el exe; si no, la aplicación es una ventana
        // y la consola (que el código de análisis usa para sus mensajes) se oculta.
        var cliMode = args.Any(a => a is "--once" or "--help" or "-h" or "--overlay-preview");
        if (OperatingSystem.IsWindows())
        {
            NativeConsole.Ensure(attachToParent: cliMode);
            if (!cliMode)
            {
                NativeConsole.Hide();
            }
        }
        Console.OutputEncoding = Encoding.UTF8;

        // Un fallo no debe cerrar la aplicación sin dejar rastro (el exe normalmente se abre con doble clic).
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            ErrorLog.Write("Unexpected error", e.ExceptionObject);
            if (cliMode)
            {
                Console.WriteLine(L.T("Непредвиденная ошибка: ", "Unexpected error: ") + e.ExceptionObject);
            }
            else
            {
                MessageBox.Show(L.T("Непредвиденная ошибка. Подробности в errors.log в папке данных.",
                        "Unexpected error. The details are in errors.log, in the data folder.") + "\n\n" + e.ExceptionObject,
                    "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // ---- Argumentos ----------------------------------------------------------------------------------
        string? dataDir = null, demosArg = null;
        var once = false;
        var reset = false;
        var auto = false;          // abierto por el proceso en espera cuando Fortnite arranca: se cierra solo al cerrar Fortnite
        string? previewPath = null;
        string? langArg = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data": dataDir = args[++i]; break;
                case "--demos": demosArg = args[++i]; break;
                case "--lang": langArg = args[++i]; L.Init(langArg); break;
                case "--once": once = true; break;
                case "--reset-config": reset = true; break;
                case "--overlay-preview": previewPath = args[++i]; break;
                case "--auto": auto = true; break;
                case "-h":
                case "--help":
                    Console.WriteLine(L.T("""
                        Счётчик сундуков Fortnite

                        Параметры:
                          --demos <папка>     папка реплеев (по умолчанию %LOCALAPPDATA%\FortniteGame\Saved\Demos)
                          --data <папка>      папка данных (по умолчанию %APPDATA%\ChestCounter)
                          --once              разобрать готовые реплеи, показать итог и выйти (без окна)
                          --reset-config      снова показать вопросы первого запуска
                          --lang ru|en        язык интерфейса
                          --overlay-preview <файл.png>   сохранить превью оверлея и выйти
                        """, """
                        Fortnite Chest Counter

                        Options:
                          --demos <folder>    replays folder (default %LOCALAPPDATA%\FortniteGame\Saved\Demos)
                          --data <folder>     data folder (default %APPDATA%\ChestCounter)
                          --once              analyze the finished replays, show the summary and quit (no window)
                          --reset-config      show the first-run questions again
                          --overlay-preview <file.png>   save previews of the overlay and quit
                        """));
                    return 0;
            }
        }

        dataDir ??= Store.DefaultFolder();
        var store = new Store(dataDir);
        L.Init(langArg ?? store.LoadConfig()?.Language);
        ErrorLog.FilePath = store.ErrorLogPath;

        // Una sola instancia por carpeta de datos: dos contarían las mismas partidas.
        using var instance = new Mutex(false, AppInfo.InstanceMutexFor(dataDir));
        if (previewPath == null && !AcquireInstance(instance, TimeSpan.Zero))
        {
            if (auto)
            {
                return 0; // abierto con Fortnite cuando ya estaba abierto: nada que hacer
            }
            var alreadyOpen = L.T("Счётчик сундуков уже открыт: посмотрите на панели задач.",
                "The chest counter is already open: look in the taskbar.");
            if (cliMode)
            {
                Console.WriteLine("  " + alreadyOpen);
            }
            else
            {
                MessageBox.Show(alreadyOpen, "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }

        // ---- Configuración -------------------------------------------------------------------------------
        var config = reset ? null : store.LoadConfig();
        var firstRun = config == null;
        config ??= new Config();

        if (demosArg != null) config.DemosFolder = demosArg;
        var defaultDemos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");
        if (string.IsNullOrWhiteSpace(config.DemosFolder))
        {
            config.DemosFolder = defaultDemos;
        }

        if (previewPath != null)
        {
            // Desarrollo: dibuja el overlay fuera de pantalla y guarda PNG (en espera, partida en curso y en vivo), luego sale.
            store.LoadPrizeNames();
            var previewData = Profiles.View(store.LoadData(), config);
            // Solo en memoria: las 3 últimas partidas cuentan como "esta sesión", para una línea de sesión realista.
            foreach (var m in previewData.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).Take(3))
            {
                m.MatchStartUtc = DateTime.UtcNow;
            }
            if (!OperatingSystem.IsWindows())
            {
                return 1;
            }
            NativeOverlay.Start(-20000, -20000, false, (_, _) => { }, _ => { });
            var fakeLive = new MatchRecord { ChestsOpened = 12, TreasureFinds = { new TreasureFind { Item = "Treasure" } } };
            foreach (var (suffix, live, inProgress) in new[] { ("-espera", (MatchRecord?)null, false), ("-en-curso", null, true), ("-en-vivo", fakeLive, true) })
            {
                NativeOverlay.Update(OverlayModel.Build(previewData, live, inProgress, Profiles.InitialChests(config, previewData.Matches.Select(Profiles.Key).FirstOrDefault())));
                Thread.Sleep(600);
                var file = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(previewPath)) ?? ".", Path.GetFileNameWithoutExtension(previewPath) + suffix + ".png");
                Console.WriteLine(NativeOverlay.Snapshot(file)
                    ? L.T($"Превью: {file}", $"Preview: {file}")
                    : L.T($"Не удалось сделать снимок: {file}", $"Capture failed: {file}"));
            }
            return 0;
        }

        LowerPriority();
        store.LoadPrizeNames();
        var data = store.LoadData();
        var tracker = new Tracker(store, config, data);
        // Antes de cualquier escaneo: si no, los replays se analizarían dos veces.
        var reanalyzing = tracker.UpgradeIfNeeded();

        // ---- Modo de línea de comandos -------------------------------------------------------------------
        if (once)
        {
            tracker.Scan(ignoreQuietPeriod: true);
            ReportWriter.WriteAll(store, config, data);
            tracker.PrintSummary();
            Console.WriteLine(L.T($"  Отчёт: {store.ReportPath}", $"  Report: {store.ReportPath}"));
            return 0;
        }

        // ---- Primer uso: tres preguntas, en ventanas sencillas -------------------------------------------
        if (firstRun)
        {
            MessageBox.Show(
                L.T("Этот счётчик считает ваши сундуки по реплеям Fortnite.\n\n" +
                    "Важно: в Fortnite включите запись повторов (Настройки → Игра → Повторы).\n" +
                    "Без этого Fortnite не сохраняет реплеи и считать нечего.",
                    "This counter counts your chests from the Fortnite replays.\n\n" +
                    "Important: in Fortnite turn on Settings → Game → Replays → Record Replays.\n" +
                    "Without it, Fortnite saves no replays and there is nothing to count."),
                "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Information);

            var existing = tracker.UnprocessedFiles().Where(f => ReplayHeaderProbe.IsLive(f.FullName) != true).ToList();
            if (existing.Count > 0)
            {
                var count = L.Count(existing.Count, "реплей", "реплея", "реплеев", "replay", "replays");
                var answer = MessageBox.Show(
                    L.T($"На вашем ПК {count}. Посчитать их тоже?\n\nДа: они разбираются сейчас.   Нет: считаются только следующие матчи.",
                        $"There are {count} on your PC. Count them too?\n\nYes: they are analyzed now.   No: only your next matches count."),
                    "ChestCounter", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.No)
                {
                    tracker.MarkSkipped(existing);
                }
            }

            if (OperatingSystem.IsWindows() && AppInfo.ExePath != null)
            {
                var withFortnite = MessageBox.Show(
                    L.T("Открывать счётчик автоматически при запуске Fortnite?\n\n" +
                        "Без этого матчи не считаются. Счётчик сам закрывается вместе с Fortnite. Это можно поменять в настройках.",
                        "Open the counter automatically when Fortnite starts?\n\n" +
                        "Without it, your matches are not counted. It closes by itself when you close Fortnite. You can change this in Settings."),
                    "ChestCounter", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
                config.StartWithFortnite = withFortnite && FortniteWatch.Apply(true, dataDir);
            }
        }
        store.SaveConfig(config);

        if (OperatingSystem.IsWindows())
        {
            if (config.StartWithFortnite)
            {
                FortniteWatch.Apply(true, dataDir); // sigue la ruta del exe si se movió o se actualizó
            }
            if (Process.GetProcessesByName("ChestTracker") is { Length: > 0 } oldTool)
            {
                MessageBox.Show(
                    L.T("Старая версия (ChestTracker.exe) всё ещё открыта: закройте её, иначе обе будут считать одновременно.",
                        "The old version (ChestTracker.exe) is still open: close it, otherwise both count at the same time."),
                    "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                foreach (var p in oldTool) p.Dispose();
            }
        }

        // ---- Ventana -------------------------------------------------------------------------------------
        var monitor = new ChestCounter.Monitor(store, config, data, tracker, dataDir) { AutoMode = auto };
        if (reanalyzing)
        {
            monitor.SetNotice(L.T("Новая версия разбора: реплеи читаются заново (без дублей).",
                "New analysis version: the replays are read again (no duplicates)."));
        }

        var form = new MainForm(monitor, store);
        monitor.CloseRequested += () =>
        {
            try { form.BeginInvoke(new Action(form.Close)); }
            catch (InvalidOperationException) { /* la ventana aún no existe */ }
        };

        monitor.Start();
        Application.Run(form);
        monitor.Stop();

        return 0;

        // ---- Ayudantes -----------------------------------------------------------------------------------

        static bool AcquireInstance(Mutex m, TimeSpan wait)
        {
            try
            {
                return m.WaitOne(wait);
            }
            catch (AbandonedMutexException)
            {
                return true; // la instancia anterior se cerró mal: ahora es nuestra
            }
        }

        // Prioridad baja + modo eficiencia de Windows 11 (EcoQoS): el juego siempre va primero.
        static void LowerPriority()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            try { EcoQos.Enable(); } catch { }
        }
    }
}
