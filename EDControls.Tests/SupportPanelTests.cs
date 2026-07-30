using System.Windows;
using EDControls.EDSupportPanel;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Testy panelu Podpora: skládání URL/textů (<see cref="SupportActions"/>), geometrie
/// vysouvání (<see cref="SupportPanelGeometry"/>) a napojení na okno přes
/// <c>EDViewModel.AttachToWindow</c>.
/// </summary>
public class SupportPanelTests
{
    private static EDServices MockServices(IMLRest? mlRest = null) =>
        new(mlRest, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    private static IMLRest MockMLRest()
    {
        var ml = Substitute.For<IMLRest>();
        ml.Server.Returns("cache-dev");
        ml.Port.Returns(57770);
        ml.DataNamespace.Returns("MLINEDATA");
        ml.Idfa.Returns("001");
        ml.FullName.Returns("Martin Hubata");
        ml.LoginName.Returns("mhubata");
        return ml;
    }

    // ---------- Feedback URL ----------

    [Fact]
    public void FeedbackUrl_ContainsKeyAndEscapedParams()
    {
        var url = SupportActions.BuildFeedbackUrl("00 1", "jan novák", "EDISONMAIN2", "form=X");

        Assert.StartsWith($"{SupportActions.WidgetBaseUrl}feedback?key={SupportActions.WidgetKey}", url);
        Assert.Contains("&idfa=00%201", url);
        Assert.Contains("&user=jan%20nov%C3%A1k", url);
        Assert.Contains("&agenda=EDISONMAIN2", url);
        Assert.Contains("&context=form%3DX", url);
    }

    [Fact]
    public void FeedbackUrl_EmptyIdentity_OmitsParams()
    {
        // Pre-login / chybějící identita — widget se má načíst bez personalizace, ne s prázdnými parametry.
        var url = SupportActions.BuildFeedbackUrl(null, "", "EDISONMAIN2", null);

        Assert.DoesNotContain("idfa=", url);
        Assert.DoesNotContain("user=", url);
        Assert.DoesNotContain("context=", url);
        Assert.Contains("&agenda=EDISONMAIN2", url);
    }

    // ---------- Context ----------

    [Fact]
    public void Context_JoinsPartsWithPipe()
    {
        var ctx = SupportActions.BuildContext("UserProfileWindow", "Uživatelský profil",
            "EDISONMAIN2.Windows.UserProfileWindow", "1.2.3.4");

        Assert.Equal("form=UserProfileWindow|title=Uživatelský profil|" +
                     "ns=EDISONMAIN2.Windows.UserProfileWindow|v=1.2.3.4", ctx);
    }

    [Fact]
    public void Context_OmitsEmptyOptionalParts()
    {
        Assert.Equal("form=Okno", SupportActions.BuildContext("Okno", null, null, null));
        Assert.Equal("form=Okno", SupportActions.BuildContext("Okno", "   ", "", ""));
    }

    [Fact]
    public void Context_TitlePipeReplacedBySlash()
    {
        // Svislítko je oddělovač polí — v titulku by rozbilo parsování na straně widgetu.
        var ctx = SupportActions.BuildContext("Okno", "Firma | Agenda", null, null);

        Assert.Equal("form=Okno|title=Firma / Agenda", ctx);
    }

    [Fact]
    public void Context_TruncatedTo255Chars()
    {
        var ctx = SupportActions.BuildContext("Okno", new string('x', 400), null, null);

        Assert.Equal(255, ctx.Length);
    }

    // ---------- Informace o verzi ----------

    [Fact]
    public void VersionInfo_WithMLRest_ListsSessionIdentity()
    {
        var info = SupportActions.BuildVersionInfo(MockMLRest(), "M-line EDISON 2", "25.7.30");

        Assert.StartsWith("M-line EDISON 2\n\nVerze: 25.7.30", info);
        Assert.Contains("Server: cache-dev:57770", info);
        Assert.Contains("Namespace: MLINEDATA", info);
        Assert.Contains("Firma: 001", info);
        Assert.Contains("Uživatel: Martin Hubata (mhubata)", info);
    }

    [Fact]
    public void VersionInfo_NullMLRest_OnlyAppAndVersion()
    {
        // Pre-login: identita ještě neexistuje, dialog nesmí padnout ani lhát.
        var info = SupportActions.BuildVersionInfo(null, "M-line EDISON 2", "25.7.30");

        Assert.Equal("M-line EDISON 2\n\nVerze: 25.7.30", info);
    }

    [Fact]
    public void VersionInfo_MissingVersion_FallsBackToAssembly()
    {
        var info = SupportActions.BuildVersionInfo(null, "TemplateDesigner", null);

        Assert.StartsWith("TemplateDesigner\n\nVerze: ", info);
        Assert.DoesNotContain("Verze: \n", info);
        Assert.DoesNotMatch(@"Verze:\s*$", info);
    }

    [Fact]
    public void VersionInfo_MissingAppName_FallsBackToAssemblyName()
    {
        var info = SupportActions.BuildVersionInfo(null, null, "1.0");

        Assert.False(info.StartsWith('\n'));
        Assert.Contains("Verze: 1.0", info);
    }

    // ---------- Geometrie vysouvání ----------

    private const double Tab = SupportPanelGeometry.TabWidth;
    private const double Card = SupportPanelGeometry.CardWidth;

    [Fact]
    public void Geometry_Closed_ShowsOnlyTabAtRightEdge()
    {
        var p = SupportPanelGeometry.Compute(
            clientLeft: 100, clientTop: 50, clientWidth: 1000, clientHeight: 800,
            panelHeight: 160, slideOffset: 0);

        Assert.Equal(Tab, p.Width);
        Assert.Equal(100 + 1000 - Tab, p.Left);      // úchyt drží pravou hranu klientské oblasti
    }

    [Fact]
    public void Geometry_Open_ShowsWholePanel()
    {
        var p = SupportPanelGeometry.Compute(100, 50, 1000, 800, 160, Card);

        Assert.Equal(Tab + Card, p.Width);
        Assert.Equal(100 + 1000 - (Tab + Card), p.Left);
    }

    [Fact]
    public void Geometry_RightEdgeStaysAnchoredWhileOpening()
    {
        // Panel roste doleva — pravá hrana (Left+Width) musí zůstat na hraně klientské oblasti,
        // jinak by se úchyt při otevírání odsouval od okraje.
        foreach (var offset in new[] { 0d, 40d, 120d, Card })
        {
            var p = SupportPanelGeometry.Compute(100, 0, 1000, 600, 160, offset);
            Assert.Equal(1100, p.Left + p.Width);
        }
    }

    [Fact]
    public void Geometry_CentersVerticallyInClientArea()
    {
        var p = SupportPanelGeometry.Compute(0, 50, 1000, 800, 160, 0);

        Assert.Equal(50 + (800 - 160) / 2, p.Top);
    }

    [Fact]
    public void Geometry_PanelTallerThanOwner_TopClampedToClientTop()
    {
        var p = SupportPanelGeometry.Compute(0, 50, 1000, 100, 160, 0);

        Assert.Equal(50, p.Top);
    }

    [Fact]
    public void Geometry_NeverOverhangsBelowClientArea()
    {
        // Karta žije ve vlastním okně, takže by přečnívala mimo okno ownera — třeba přes
        // footer s tlačítky. Spodní hrana panelu musí zůstat v klientské oblasti.
        var p = SupportPanelGeometry.Compute(0, 100, 800, 200, 160, 0);

        Assert.True(p.Top + 160 <= 100 + 200);
    }

    [Theory]
    [InlineData(900, 600, true)]     // běžné okno
    [InlineData(182, 167, true)]     // přesně na hranici
    [InlineData(181, 600, false)]    // užší než úchyt + karta
    [InlineData(900, 166, false)]    // nižší než karta
    public void Geometry_FitsOnlyWhenWholeCardFitsInClientArea(double w, double h, bool expected)
    {
        Assert.Equal(expected, SupportPanelGeometry.Fits(w, h, panelHeight: 167));
    }

    [Fact]
    public void Geometry_OwnerNarrowerThanPanel_WidthClampedToClientWidth()
    {
        // Panel nikdy nepřeteče vlevo mimo klientskou oblast ownera.
        var p = SupportPanelGeometry.Compute(200, 0, 100, 600, 160, Card);

        Assert.Equal(100, p.Width);
        Assert.Equal(200, p.Left);
    }

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(9999, Card)]
    public void Geometry_ClampsOffsetToValidRange(double offset, double expectedVisibleCard)
    {
        var p = SupportPanelGeometry.Compute(0, 0, 1000, 600, 160, offset);

        Assert.Equal(Tab + expectedVisibleCard, p.Width);
    }

    // ---------- Napojení na okno ----------

    [Fact]
    public void AttachToWindow_ByDefault_AttachesSupportPanel()
    {
        RunSta(() =>
        {
            var window = new Window();
            new PanelTestViewModel(MockServices()).AttachToWindow(window);

            Assert.True(EDSupportPanel.EDSupportPanel.IsAttached(window));
        });
    }

    [Fact]
    public void AttachToWindow_OptedOut_DoesNotAttach()
    {
        RunSta(() =>
        {
            var window = new Window();
            new PanelTestViewModel(MockServices()) { PanelEnabled = false }.AttachToWindow(window);

            Assert.False(EDSupportPanel.EDSupportPanel.IsAttached(window));
        });
    }

    [Fact]
    public void AttachToWindow_Twice_StaysAttachedWithoutThrowing()
    {
        RunSta(() =>
        {
            var window = new Window();
            var vm = new PanelTestViewModel(MockServices());

            vm.AttachToWindow(window);
            vm.AttachToWindow(window);

            Assert.True(EDSupportPanel.EDSupportPanel.IsAttached(window));
        });
    }

    private class PanelTestViewModel(EDServices services) : EDViewModel.EDViewModel(services)
    {
        public bool PanelEnabled
        {
            get => ShowSupportPanel;
            init => ShowSupportPanel = value;
        }
    }

    // Spustí akci na dedikovaném STA vlákně (Window to vyžaduje) a propaguje výjimku zpět.
    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
