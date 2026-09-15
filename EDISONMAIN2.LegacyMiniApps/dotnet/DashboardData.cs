using System;
using System.Collections.Generic;
using System.Linq;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>Sekce dashboardu legacy miniaplikace - buď textový label, nebo tabulka.</summary>
public abstract record DashboardSection;

public sealed record DashboardLabel(string Text) : DashboardSection;

/// <summary>
/// Sloupec legacy tabulky. <paramref name="Type"/> je písmeno z deskriptoru sekce
/// <c>ObjectSectionTable</c> (<c>T</c> text, <c>N</c> číslo, <c>D</c> datum),
/// <paramref name="Format"/> hotový .NET numerický formát (jen u <c>N</c>).
/// </summary>
public sealed record DashboardColumn(string Name, char Type, string Format);

/// <summary><paramref name="PrefsKey"/> = HostId pro persist šířek sloupců, unikátní per (appType, pořadí tabulky).</summary>
public sealed record DashboardTable(
    IReadOnlyList<DashboardColumn> Columns,
    IReadOnlyList<DashboardRow> Rows,
    string PrefsKey) : DashboardSection;

/// <summary>Řádek tabulky: buňky + volitelný styl (HTML hex barva textu, typ ikony z legacy IconTypeEnum).</summary>
public sealed record DashboardRow(IReadOnlyList<string> Cells, string? ForeColorHtml, int IconType);

/// <summary>
/// Data jedné legacy miniaplikace (labely + tabulky) + parser delimited výstupu
/// <c>ALVA.EDISON.MiniAplikaceAPI.LoadData</c>. Mirror parsování v legacy
/// <c>EDMiniApplication.LoadData</c>; kódy sekcí z <c>ALVAMINIAPLIKACE.inc</c>.
/// </summary>
public sealed record DashboardData(string? LastUpdated, IReadOnlyList<DashboardSection> Sections)
{
    public static readonly DashboardData Empty = new(null, []);

    // ALVAMINIAPLIKACE.inc: typ parametru a sekce objektu.
    private const int ParamTimeStamp = 1;
    private const int SectionLabel = 1, SectionTable = 2, SectionCols = 3, SectionRows = 4, SectionRowStyles = 5;

    /// <summary>
    /// Naparsuje delimited výstup <c>LoadData</c> pro daný <paramref name="appType"/>.
    /// Chybějící blok (firma agendu nemá / nejsou data) = <see cref="Empty"/>.
    /// </summary>
    public static DashboardData Parse(object[]? output, char delimiter, string appType) =>
        ParseAll(output, delimiter).GetValueOrDefault(appType, Empty);

    /// <summary>
    /// Naparsuje dávkovou odpověď <c>LoadData</c> (N miniaplikací v jednom requestu) na bloky
    /// podle markeru <c>appType</c>. Primární oddělovač polí = <paramref name="delimiter"/>
    /// (z MLRest), sekundární <c>~</c> (buňky/sloupce), styl <c>:</c> (ikona:barva). Řádky výstupu:
    /// <c>appType{D}id</c>, <c>param{D}i{D}value</c>, <c>data{D}objType{D}key{D}section{D}value</c>.
    /// Klíč v odpovědi chybí, když pro appType nejsou data - server pro něj blok vůbec nevrátí.
    /// </summary>
    public static IReadOnlyDictionary<string, DashboardData> ParseAll(object[]? output, char delimiter)
    {
        var result = new Dictionary<string, DashboardData>();
        if (output is null) return result;

        string? currentApp = null;
        string? lastUpdated = null;
        var sections = new List<DashboardSection>();
        List<DashboardColumn>? cols = null;
        List<DashboardRow>? rows = null;
        // Deskriptor typů nese sekce SectionTable, názvy sloupců přijdou až v SectionCols.
        string? colTypes = null;
        int tableIndex = 0;

        // Rozpracovaná tabulka se přidá do sekcí až při dalším objektu / konci bloku (zachová pořadí).
        void FlushTable()
        {
            if (cols is null) return;
            sections.Add(new DashboardTable(cols, rows!, $"EDISONMAIN2.LegacyMiniApp.{currentApp}.{tableIndex++}"));
            cols = null;
            rows = null;
            colTypes = null;
        }

        void FlushApp()
        {
            FlushTable();
            if (currentApp is not null) result[currentApp] = new DashboardData(lastUpdated, sections);
            // Nový seznam, ne Clear - hotový blok si drží ten svůj.
            sections = [];
            lastUpdated = null;
            tableIndex = 0;
        }

        foreach (var raw in output)
        {
            if (raw is not string line || line.Length == 0) continue;
            var t = line.Split(delimiter);

            switch (t[0])
            {
                case "appType":
                    FlushApp();
                    currentApp = t.Length > 1 ? t[1] : null;
                    break;

                case "param" when currentApp is not null && t.Length > 2:
                    if (int.TryParse(t[1], out var pi) && pi == ParamTimeStamp) lastUpdated = t[2];
                    break;

                case "data" when currentApp is not null && t.Length > 4:
                    int section = int.TryParse(t[3], out var s) ? s : 0;
                    // Hodnota smí obsahovat primární oddělovač jen teoreticky - rejoin pro jistotu.
                    string value = string.Join(delimiter, t.Skip(4));
                    switch (section)
                    {
                        case SectionLabel:
                            FlushTable();
                            sections.Add(new DashboardLabel(value));
                            break;
                        case SectionTable:
                            FlushTable();
                            cols = [];
                            rows = [];
                            colTypes = value;
                            break;
                        case SectionCols when cols is not null:
                            cols.AddRange(BuildColumns(value, colTypes));
                            break;
                        case SectionRows when rows is not null:
                            rows.Add(new DashboardRow(value.Split('~'), null, 0));
                            break;
                        case SectionRowStyles when rows is { Count: > 0 }:
                            var st = value.Split(':');
                            int icon = st.Length > 0 && int.TryParse(st[0], out var ic) ? ic : 0;
                            string? color = st.Length > 1 && st[1].Length > 0 ? st[1] : null;
                            rows[^1] = rows[^1] with { ForeColorHtml = color, IconType = icon };
                            break;
                    }
                    break;
            }
        }

        FlushApp();
        return result;
    }

    /// <summary>
    /// Spáruje názvy sloupců (<c>~</c>) s deskriptorem typů (<c>:</c>) pozičně, např.
    /// <c>"Rz~Celkové MTH"</c> + <c>"T:N2#"</c>. Kratší nebo chybějící deskriptor = TEXT.
    /// </summary>
    private static IEnumerable<DashboardColumn> BuildColumns(string names, string? types)
    {
        string[] typeDefs = types is null ? [] : types.Split(':');
        return names.Split('~').Select((name, i) =>
        {
            var def = i < typeDefs.Length ? typeDefs[i] : "";
            char type = def.Length > 0 ? char.ToUpperInvariant(def[0]) : 'T';
            return new DashboardColumn(name, type, type == 'N' ? NumberFormat(def) : "");
        });
    }

    /// <summary>
    /// Numerický formát z deskriptoru (port <c>EDMiniApplication.GetFormat</c>): druhý znak =
    /// počet desetinných míst, třetí <c>#</c> = oddělovat tisíce. <c>"N2#"</c> → <c>"#,##0.00"</c>.
    /// Samotné <c>"N"</c> = bez formátování, hodnota zůstane raw jako v legacy.
    /// </summary>
    internal static string NumberFormat(string def)
    {
        if (def.Length < 2) return "";
        int digits = char.IsDigit(def[1]) ? def[1] - '0' : 0;
        string fmt = def.Length > 2 && def[2] == '#' ? "#,##0" : "###0";
        return digits > 0 ? fmt + "." + new string('0', digits) : fmt;
    }
}
