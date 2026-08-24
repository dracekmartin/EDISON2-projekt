using System.Text.Json.Nodes;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Prvek, který drží <b>vlastní definici sloupců</b> pro celou šablonu - dnes vývěskové odjezdy
/// a linková tabulka.
/// </summary>
/// <remarks>
/// <para>Definice je jediný kus šablony, na kterém závisí <b>data</b>: posílá se serveru jako
/// <c>DEFINICE_SLOUPCU</c> a ten podle ní skládá skupiny. Proto ji drží prvek (tedy šablona),
/// a parametr ji jen zrcadlí.</para>
/// <para><b>Význam definice je pro obě rodiny agend tentýž</b> - drží ho server v <c>BaseAgenda</c>.
/// Liší se až spotřeba: vývěsce je skupina <b>jeden sloupec</b>, linkovému JŘ <b>pás nad několika
/// sloupci spojů</b>, a spoj patřící do dvou skupin se vypíše dvakrát.</para>
/// </remarks>
public interface IColumnLayoutOwner
{
    /// <summary>Vlastní definice sloupců. Null nebo prázdné = server složí skupiny sám podle
    /// nastavení linky, jako dosud.</summary>
    List<DepartureColumnDefinition>? CustomColumns { get; set; }

    /// <summary>Definice ve wire tvaru pro <c>DEFINICE_SLOUPCU</c>; null = žádná definice.</summary>
    string? BuildColumnsDefinitionWireJson();
}

/// <summary>Převod definice sloupců do wire tvaru - <b>jedno místo pro všechny prvky</b>, aby
/// se tvar nerozešel podle toho, kdo ho zrovna skládá.</summary>
public static class ColumnLayoutWire
{
    private static readonly System.Text.Json.JsonSerializerOptions Options =
        new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Wire JSON definice sloupců (klíče čte <c>ParseDefiniceSloupcu</c> v Caché).
    /// Null = prázdná definice → server zůstane na automatickém rozvržení.</summary>
    public static string? Build(List<DepartureColumnDefinition>? cols)
    {
        if (cols is not { Count: > 0 }) return null;
        var arr = new JsonArray();
        foreach (var c in cols)
        {
            arr.Add(new JsonObject
            {
                ["id"] = c.Id,
                ["nadpis"] = c.Header,
                ["nadpis2"] = c.Header2,
                ["dny"] = c.Days,
                ["znackyJen"] = c.IncludeMarks,
                ["znackyBez"] = c.ExcludeMarks,
                ["nevypisovatZnacky"] = c.SuppressedMarks,
                ["obdobi"] = c.Period
            });
        }
        return new JsonObject { ["sloupce"] = arr }.ToJsonString(Options);
    }
}
