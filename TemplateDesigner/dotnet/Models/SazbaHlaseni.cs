using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Models;

/// <summary>
/// Hlášky o tom, co sazba sama upravila nebo nesplnila (`B178`): zmenšená tabulka, Počet stran
/// a podmínky viditelnosti, které nejde přečíst.
/// </summary>
/// <remarks>
/// Pořadí priority nastavení (<c>architecture.md</c> §Nastavení sekcí) říká, že přání pro celou
/// sestavu může prohrát - a tohle je místo, kde se to uživatel dozví. Úprava, která se povedla, je
/// informace (zákazníci o zmenšování sami žádali); nesplněný cíl a nečitelná podmínka jsou upozornění.
/// </remarks>
public static class SazbaHlaseni
{
    public static List<Hlaseni> Seznam(Template template, LayoutResult sazba)
    {
        var hlaseni = new List<Hlaseni>();
        var tabulka = template.Sections.SelectMany(s => s.Elements).OfType<LineTableElement>().FirstOrDefault();

        if (sazba.CilStranNesplnen is { } cil)
            hlaseni.Add(new Hlaseni(ZavaznostHlaseni.Upozorneni, "Sestava",
                $"Počet stran {cil} nejde splnit - {sazba.DuvodNesplneni ?? "nevejde se ani po zmenšení na mez"}",
                sazba.DuvodNesplneni is null ? "zvýšit Počet stran" : "vypnout Začít na nové stránce, nebo zvýšit Počet stran",
                tabulka?.Id));
        else if (sazba.CilStranSplnen is { } splnen)
            hlaseni.Add(new Hlaseni(ZavaznostHlaseni.Informace, "Sestava",
                $"vešla se na {PocetStran(splnen)}{Zmenseni(sazba)}", null, tabulka?.Id));
        else if (sazba.ZmenseniTabulky < 0.999f)
            hlaseni.Add(new Hlaseni(ZavaznostHlaseni.Informace,
                tabulka is null ? "Tabulka" : ElementNames.ForUser(tabulka),
                $"zmenšena na {Procent(sazba.ZmenseniTabulky)} %, aby se nedělila na další stránku", null, tabulka?.Id));

        foreach (var sekce in template.Sections)
        {
            if (ExpressionEvaluator.Validate(sekce.VisibilityExpression) is { } chybaSekce)
                hlaseni.Add(new Hlaseni(ZavaznostHlaseni.Upozorneni, $"Sekce „{sekce.Name}“",
                    $"podmínku viditelnosti nejde přečíst ({chybaSekce}), sekce se tiskne vždy", "opravit podmínku"));
            foreach (var prvek in sekce.Elements)
                if (ExpressionEvaluator.Validate(prvek.VisibilityExpression) is { } chybaPrvku)
                    hlaseni.Add(new Hlaseni(ZavaznostHlaseni.Upozorneni, ElementNames.SPopisem(prvek),
                        $"podmínku viditelnosti nejde přečíst ({chybaPrvku}), prvek se tiskne vždy", "opravit podmínku",
                        prvek.Id));
        }
        return hlaseni;
    }

    private static string Zmenseni(LayoutResult r)
    {
        var casti = new List<string>();
        if (r.ZmenseniPoznamek < 0.999f) casti.Add($"poznámky na {Procent(r.ZmenseniPoznamek)} %");
        if (r.ZmenseniTabulky < 0.999f) casti.Add($"tabulka na {Procent(r.ZmenseniTabulky)} %");
        return casti.Count == 0 ? " (části tabulky přisunuty k sobě)" : $" (zmenšeno: {string.Join(", ", casti)})";
    }

    private static int Procent(float mira) => (int)MathF.Round(mira * 100f);

    private static string PocetStran(int n) => n switch
    {
        1 => "1 stranu",
        >= 2 and <= 4 => $"{n} strany",
        _ => $"{n} stran",
    };
}
