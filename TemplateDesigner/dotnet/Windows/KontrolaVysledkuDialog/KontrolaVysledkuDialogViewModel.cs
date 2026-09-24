using EDControls.EDViewModel;
using TemplateDesigner.Models;

namespace TemplateDesigner;

/// <summary>Výsledek kontroly vybraných linek z tiskového okna (`B178`) - hlášky ve vlastním okně,
/// aby nezabíraly místo formuláři parametrů (rozhodnutí Jan 24. 9.).</summary>
public class KontrolaVysledkuDialogViewModel : EDViewModel
{
    public KontrolaVysledkuDialogViewModel(string nadpis, IReadOnlyList<Hlaseni> hlaseni)
    {
        Nadpis = nadpis;
        Hlaseni = hlaseni;
    }

    /// <summary>„Zkontrolováno 12 linek: 1 chyba, 2 upozornění".</summary>
    public string Nadpis { get; }

    public IReadOnlyList<Hlaseni> Hlaseni { get; }
}
