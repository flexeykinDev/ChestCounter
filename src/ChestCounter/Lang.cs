using System.Globalization;

namespace ChestCounter;

/// <summary>
/// Interface language: Russian or English. Texts are written inline: <c>L.T("русский", "English")</c>.
/// Choice order: CHESTCOUNTER_LANG, then --lang / the settings, then Windows (English Windows = English, else Russian).
/// Names stored in data.json (chest kinds, treasure items) stay in English and are translated when shown (<see cref="Name"/>).
/// </summary>
public static class L
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>True when the interface is in English.</summary>
    public static bool En { get; private set; } = Detect(null);

    /// <summary>Applies the saved or command-line choice ("ru", "en"; anything else = automatic).</summary>
    public static void Init(string? choice) => En = Detect(choice);

    private static bool Detect(string? choice)
    {
        var forced = Environment.GetEnvironmentVariable("CHESTCOUNTER_LANG") is { Length: > 0 } env ? env : choice;
        if (forced != null && forced.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (forced != null && forced.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
    }

    public static CultureInfo Culture => En ? EnUs : Ru;

    /// <summary>Language code for the lang attribute of the HTML pages.</summary>
    public static string Code => En ? "en" : "ru";

    public static string T(string ru, string en) => En ? en : ru;

    /// <summary>Integer without thousands separator ("1409"): figures are counts, not amounts.</summary>
    public static string N(int n) => n.ToString("0", Culture);

    /// <summary>Russian plural form: 1 сундук, 2 сундука, 5 сундуков.</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        var n10 = Math.Abs(n) % 10;
        var n100 = Math.Abs(n) % 100;
        if (n10 == 1 && n100 != 11)
        {
            return one;
        }
        return n10 is >= 2 and <= 4 && n100 is < 12 or > 14 ? few : many;
    }

    /// <summary>"3 сундука" / "1 chest" (plural for the number, in the current language).</summary>
    public static string Count(int n, string ruOne, string ruFew, string ruMany, string enOne, string enMany) =>
        $"{N(n)} {(En ? (n == 1 ? enOne : enMany) : Plural(n, ruOne, ruFew, ruMany))}";

    /// <summary>"12-й" / "12th".</summary>
    public static string Ordinal(int n)
    {
        if (!En)
        {
            return $"{n}-й";
        }
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    /// <summary>Short date + time of a match ("05.11 22:54" / "Nov 5 22:54").</summary>
    public static string DateTimeShort(DateTime d) => d.ToString(En ? "MMM d HH:mm" : "dd.MM HH:mm", Culture);

    // Internal names stored in data.json (English) -> shown name.
    private static readonly Dictionary<string, (string Ru, string En)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Chest"] = ("Сундук", "Chest"),
        ["Rare chest"] = ("Редкий сундук", "Rare chest"),
        ["Buried chest"] = ("Закопанный сундук", "Buried chest"),
        ["Extraction / relic chest"] = ("Сундук извлечения / реликвий", "Extraction / relic chest"),
        ["Spirit poster chest"] = ("Сундук с плакатами духов", "Spirit poster chest"),
        ["Other chest"] = ("Другой сундук", "Other chest"),
        ["Loot cannon"] = ("Пушка с добычей", "Loot cannon"),
        ["Ammo box"] = ("Ящик с патронами", "Ammo box"),
        ["Food box"] = ("Ящик с едой", "Food box"),
        ["Other container"] = ("Другой контейнер", "Other container"),
        ["Treasure"] = ("Сокровище", "Treasure"),
        ["Treasure (unknown type)"] = ("Сокровище (неизвестный тип)", "Treasure (unknown type)"),
        ["Treasure gizmo"] = ("Устройство сокровищ", "Treasure gizmo"),
        ["Golden firework"] = ("Золотой фейерверк", "Golden firework"),
        ["Portable extractor"] = ("Переносной экстрактор", "Portable extractor"),
        ["Spirit locator"] = ("Локатор духов", "Spirit locator"),
        ["Lucky locator"] = ("Счастливый локатор", "Lucky locator"),
        ["Extraction accelerator"] = ("Ускоритель извлечения", "Extraction accelerator"),
        ["unknown"] = ("неизвестно", "unknown"),
    };

    /// <summary>
    /// Nombres internos de versiones anteriores a la 2.0.9 (estaban en francés) -> nombre interno actual.
    /// Solo se usa para convertir el historial antiguo al abrirlo.
    /// </summary>
    private static readonly Dictionary<string, string> Legacy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Coffre"] = "Chest",
        ["Coffre rare"] = "Rare chest",
        ["Coffre enterr\u00e9"] = "Buried chest",
        ["Coffre d'extraction / reliques"] = "Extraction / relic chest",
        ["Coffre des affiches d'esprit"] = "Spirit poster chest",
        ["Autre coffre"] = "Other chest",
        ["Canon \u00e0 butin"] = "Loot cannon",
        ["Caisse de munitions"] = "Ammo box",
        ["Caisse de nourriture"] = "Food box",
        ["Autre conteneur"] = "Other container",
        ["Tr\u00e9sor"] = "Treasure",
        ["Tr\u00e9sor (type inconnu)"] = "Treasure (unknown type)",
        ["Gizmo de tr\u00e9sor"] = "Treasure gizmo",
        ["Feu d'artifice dor\u00e9"] = "Golden firework",
        ["Extracteur portable"] = "Portable extractor",
        ["Localisateur d'esprit"] = "Spirit locator",
        ["Localisateur chanceux"] = "Lucky locator",
        ["Acc\u00e9l\u00e9rateur d'extraction"] = "Extraction accelerator",
        ["inconnu"] = "unknown",
    };

    /// <summary>Nombre interno actual de un nombre guardado por una versión anterior (o el mismo si no cambió).</summary>
    public static string Upgrade(string stored) => Legacy.TryGetValue(stored, out var current) ? current : stored;

    /// <summary>Nombre mostrado de un tipo de cofre u objeto guardado en data.json.</summary>
    public static string Name(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return stored ?? "";
        }
        return Names.TryGetValue(Upgrade(stored), out var pair) ? (En ? pair.En : pair.Ru) : stored;
    }
}
