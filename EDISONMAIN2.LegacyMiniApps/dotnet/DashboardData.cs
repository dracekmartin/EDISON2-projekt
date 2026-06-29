using System.Collections.Generic;
using System.Linq;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>Sekce dashboardu legacy miniaplikace — buď textový label, nebo tabulka.</summary>
public abstract record DashboardSection;

public sealed record DashboardLabel(string Text) : DashboardSection;

public sealed record DashboardTable(IReadOnlyList<string> Columns, IReadOnlyList<DashboardRow> Rows) : DashboardSection;

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
    /// Naparsuje delimited výstup <c>LoadData</c> pro daný <paramref name="appType"/>. Primární oddělovač
    /// polí = <paramref name="delimiter"/> (z MLRest), sekundární <c>~</c> (buňky/sloupce), styl <c>:</c>
    /// (ikona:barva). Řádky výstupu: <c>appType{D}id</c>, <c>param{D}i{D}value</c>,
    /// <c>data{D}objType{D}key{D}section{D}value</c>.
    /// </summary>
    public static DashboardData Parse(object[]? output, char delimiter, string appType)
    {
        if (output is null) return Empty;

        string? lastUpdated = null;
        var sections = new List<DashboardSection>();
        List<string>? cols = null;
        List<DashboardRow>? rows = null;
        bool inTargetApp = false;

        // Rozpracovaná tabulka se přidá do sekcí až při dalším objektu / konci bloku (zachová pořadí).
        void FlushTable()
        {
            if (cols is null) return;
            sections.Add(new DashboardTable(cols, rows!));
            cols = null;
            rows = null;
        }

        foreach (var raw in output)
        {
            if (raw is not string line || line.Length == 0) continue;
            var t = line.Split(delimiter);

            switch (t[0])
            {
                case "appType":
                    FlushTable();
                    inTargetApp = t.Length > 1 && t[1] == appType;
                    break;

                case "param" when inTargetApp && t.Length > 2:
                    if (int.TryParse(t[1], out var pi) && pi == ParamTimeStamp) lastUpdated = t[2];
                    break;

                case "data" when inTargetApp && t.Length > 4:
                    int section = int.TryParse(t[3], out var s) ? s : 0;
                    // Hodnota smí obsahovat primární oddělovač jen teoreticky — rejoin pro jistotu.
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
                            break;
                        case SectionCols when cols is not null:
                            cols.AddRange(value.Split('~'));
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

        FlushTable();
        return new DashboardData(lastUpdated, sections);
    }
}
