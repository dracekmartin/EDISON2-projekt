namespace TemplateDesigner.Models;

/// <summary>
/// Tlačítka ve formulářích prvků (pole typu tlačítko v <c>*Edit.cls</c>). Formulář tlačítko jen ukáže,
/// akci musí obsloužit každé okno, které formulář hostí, samo. Okno vlastností ukáže jen tlačítka,
/// pro která dostalo akci; panel a vlastní okna tabulek hlídá <c>TlacitkaFormulareTests</c>.
/// </summary>
public static class TlacitkaFormulare
{
    /// <summary>Pravidla zvýraznění řádků (Seznam zastávek, Tabulka z dat, Tabulka spojů).</summary>
    public const string ZvyrazneniRadku = "__highlightRules";

    /// <summary>Druhy poznámek (Poznámky v linkovém JŘ).</summary>
    public const string DruhyPoznamek = "__skupinyPoznamek";

    /// <summary>Z buňky pevné tabulky zpět na nastavení celé tabulky - jen v panelu, kde nahrazuje Esc.</summary>
    public const string NastaveniCeleTabulky = "__tableSettings";
}
