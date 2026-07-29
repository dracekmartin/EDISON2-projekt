using TemplateDesigner;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Držení logické stránky náhledu při re-fetchi dat
/// (<see cref="TemplateDesignerViewModel.FindMatchingPageIndex"/>): po změně server parametru
/// (např. „Samostatná stránka pro každý označník") se má zůstat na téže zastávce×směru, ne na
/// pořadovém čísle - rozdělení posouvá čísla stránek.
/// </summary>
public class PreviewPageMatchTests
{
    private static DataContext Page(string zastavka, string smer, string oznacnik = "") =>
        new(new Dictionary<string, object?>
        {
            ["CISLO_ZASTAVKA"] = zastavka,
            ["SMER"] = smer,
            ["NASTUPISTE"] = oznacnik,
        });

    [Fact]
    public void PosunutaCisla_NajdeTutezZastavku()
    {
        // Před: [ČB, Dasný]; po rozdělení: [ČB-11, ČB-22, Dasný] - Dasný z indexu 1 na 2.
        var current = Page("100", "T");
        var nove = new List<DataContext> { Page("50", "T", "11"), Page("50", "T", "22"), Page("100", "T") };
        Assert.Equal(2, TemplateDesignerViewModel.FindMatchingPageIndex(nove, current, oldIndex: 1));
    }

    [Fact]
    public void ShodaVcetneOznacniku_MaPrednost()
    {
        var current = Page("50", "T", "22");
        var nove = new List<DataContext> { Page("50", "T", "11"), Page("50", "T", "22") };
        Assert.Equal(1, TemplateDesignerViewModel.FindMatchingPageIndex(nove, current, oldIndex: 0));
    }

    [Fact]
    public void BezShodyOznacniku_PadaNaPrvniStrankuZastavky()
    {
        // Vypnutí rozdělení: z „ČB ozn. 22" na sloučenou stránku téže zastávky.
        var current = Page("50", "T", "22");
        var nove = new List<DataContext> { Page("40", "T"), Page("50", "T", "11") };
        Assert.Equal(1, TemplateDesignerViewModel.FindMatchingPageIndex(nove, current, oldIndex: 5));
    }

    [Fact]
    public void RozlisujeSmer()
    {
        var current = Page("50", "Z");
        var nove = new List<DataContext> { Page("50", "T"), Page("50", "Z") };
        Assert.Equal(1, TemplateDesignerViewModel.FindMatchingPageIndex(nove, current, oldIndex: 0));
    }

    [Fact]
    public void AgendaBezIdentifikacnichPoli_DrziPoradoveCislo()
    {
        var current = new DataContext(new Dictionary<string, object?> { ["CISLO_FAKTURY"] = "42" });
        var nove = new List<DataContext> { current, current, current };
        Assert.Equal(2, TemplateDesignerViewModel.FindMatchingPageIndex(nove, current, oldIndex: 2));
        Assert.Equal(2, TemplateDesignerViewModel.FindMatchingPageIndex(nove, current, oldIndex: 9));
        Assert.Equal(0, TemplateDesignerViewModel.FindMatchingPageIndex(nove, null, oldIndex: 0));
    }
}
