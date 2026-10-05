using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Pole z dat na víc řádků v textu bez zalamování. Popis dopravce u zastávkového JŘ má od 31. 8.
/// název, sídlo a telefon pod sebou a výchozí šablona ho tiskla jako „a.s.□Oblast…" - zalomení
/// řádku vyšlo jako neznámý znak.
/// </summary>
public class TextViceradkovaDataTests
{
    private static DataContext Dopravce() => new(values: new Dictionary<string, object?>
    {
        ["POPIS_DOPRAVCE"] = "ČSAD AUTOBUSY České Budějovice a.s.\nOblast Český Krumlov\nHálkova 781/9, 370 04\n\ntel: 386100111",
    });

    [Fact]
    public void BezZalamovaniSeRadkySpojiCarkou()
    {
        var text = new TextElement { Text = "{POPIS_DOPRAVCE}", WordWrap = false };

        Assert.Equal("ČSAD AUTOBUSY České Budějovice a.s., Oblast Český Krumlov, Hálkova 781/9, 370 04, tel: 386100111",
            text.TextKKresleni(Dopravce()));
    }

    [Fact]
    public void SeZalamovanimZustanouRadky()
    {
        var text = new TextElement { Text = "{POPIS_DOPRAVCE}", WordWrap = true };

        Assert.Contains('\n', text.TextKKresleni(Dopravce()));
    }
}
