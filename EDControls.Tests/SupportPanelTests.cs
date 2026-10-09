using System.Reflection;
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

    [Fact]
    public void PublicUrls_AreAbsoluteHttpsInMlineDomain()
    {
        // Konstanty se nikde jinde nevalidují - rozbitá URL se projeví až chybou prohlížeče
        // po kliknutí (patička přihlašovací obrazovky, tlačítka panelu Podpora).
        var urls = new[] { SupportActions.PortalUrl, SupportActions.WebUrl, SupportActions.WidgetBaseUrl };

        Assert.All(urls, url =>
        {
            var uri = new Uri(url);
            Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
            Assert.EndsWith("m-line.cz", uri.Host);
        });
    }

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
        // Pre-login / chybějící identita - widget se má načíst bez personalizace, ne s prázdnými parametry.
        var url = SupportActions.BuildFeedbackUrl(null, "", "EDISONMAIN2", null);

        Assert.DoesNotContain("idfa=", url);
        Assert.DoesNotContain("user=", url);
        Assert.DoesNotContain("context=", url);
        Assert.Contains("&agenda=EDISONMAIN2", url);
    }

    [Fact]
    public void FeedbackUrlFromWindow_CarriesAppVersionFromServices()
    {
        // Verze assembly okna je všude 1.0.0.0 (žádný csproj nemá <Version>), podpora z ní build nepozná.
        var services = MockServices(MockMLRest());
        services.AppVersion = "25.7.30";

        var url = SupportActions.BuildWindowFeedbackUrl(
            services, typeof(SupportPanelTests), "Titulek", typeof(SupportPanelTests).Assembly);

        Assert.Contains("v%3D25.7.30", url);
        Assert.Contains("&idfa=001", url);
        Assert.Contains("&user=mhubata", url);
        Assert.Contains("&agenda=EDControls.Tests", url);
    }

    [Fact]
    public void FeedbackUrlFromWindow_ServicesWithoutVersion_CarriesEntryAssemblyVersion()
    {
        // TemplateDesigner AppVersion nenastavuje - v= nese verzi exe, ne verzi assembly okna.
        var entry = typeof(SupportPanelTests).Assembly;
        var informational = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        var url = SupportActions.BuildWindowFeedbackUrl(MockServices(), typeof(SupportPanelTests), null, entry);

        Assert.EndsWith(Uri.EscapeDataString("|v=" + AppIdentity.ShortenBuildMetadata(informational)), url);
    }

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
        // Svislítko je oddělovač polí - v titulku by rozbilo parsování na straně widgetu.
        var ctx = SupportActions.BuildContext("Okno", "Firma | Agenda", null, null);

        Assert.Equal("form=Okno|title=Firma / Agenda", ctx);
    }

    [Fact]
    public void Context_TruncatedTo255Chars()
    {
        var ctx = SupportActions.BuildContext("Okno", new string('x', 400), null, null);

        Assert.Equal(255, ctx.Length);
    }

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
    public void VersionInfo_MissingVersion_ShowsEntryAssemblyInformationalVersion()
    {
        // TemplateDesigner verzi nenastavuje. Dřív dialog ukázal AssemblyVersion 1.0.0.0 bez hashe buildu.
        var entry = typeof(SupportPanelTests).Assembly;
        var informational = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        var info = SupportActions.BuildVersionInfo(null, "TemplateDesigner", null, entry);

        Assert.Equal($"TemplateDesigner\n\nVerze: {AppIdentity.ShortenBuildMetadata(informational)}", info);
    }

    [Fact]
    public void VersionInfo_MissingAppName_FallsBackToAssemblyName()
    {
        var info = SupportActions.BuildVersionInfo(null, null, "1.0");

        Assert.False(info.StartsWith('\n'));
        Assert.Contains("Verze: 1.0", info);
    }

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
        // Panel roste doleva - pravá hrana (Left+Width) musí zůstat na hraně klientské oblasti,
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
        // Karta žije ve vlastním okně, takže by přečnívala mimo okno ownera - třeba přes
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

    // Panel připojuje class handler na Window.Loaded (EDAppearance.Initialize); testuje se jeho
    // rozhodnutí, ne event - zobrazovat okna v testech není potřeba.

    [Fact]
    public void ShouldAttach_PlainWindow_True()
    {
        EDTesting.Sta.Run(() => Assert.True(EDSupportPanel.EDSupportPanel.ShouldAttach(new Window())));
    }

    [Fact]
    public void ShouldAttach_Suppressed_False()
    {
        // Opt-out pro okna bez ViewModelu (vlastní popupy EDControls, splash, odpočty).
        EDTesting.Sta.Run(() =>
        {
            var window = new Window();
            EDSupportPanel.EDSupportPanel.SetSuppress(window, true);

            Assert.False(EDSupportPanel.EDSupportPanel.ShouldAttach(window));
        });
    }

    [Fact]
    public void ShouldAttach_OverlayWindow_False()
    {
        // Vlastní overlay okna EDControls (panel, „Akce probíhá") nesmí dostat vlastní panel.
        EDTesting.Sta.Run(() => Assert.False(EDSupportPanel.EDSupportPanel.ShouldAttach(new FakeOverlayWindow())));
    }

    [Fact]
    public void ShouldAttach_DockWindow_False()
    {
        // Chrome AvalonDocku (drop overlay, plovoucí okno widgetu): karta nepatří na drag artefakt
        // a owned okno v jeho Show()/Close() rozbíjí drag docku. Viz WindowOwnershipTests.
        EDTesting.Sta.Run(() => Assert.False(EDSupportPanel.EDSupportPanel.ShouldAttach(new AvalonDock.Controls.FakeDockWindow())));
    }

    [Fact]
    public void ShouldAttach_ViewModelOptedOut_False()
    {
        EDTesting.Sta.Run(() =>
        {
            var window = new Window
            {
                DataContext = new PanelTestViewModel(MockServices()) { PanelEnabled = false }
            };

            Assert.False(EDSupportPanel.EDSupportPanel.ShouldAttach(window));
        });
    }

    [Fact]
    public void ShouldAttach_AlreadyAttached_False()
    {
        // Druhý Loaded téhož okna (re-parent) panel nezdvojí.
        EDTesting.Sta.Run(() =>
        {
            var window = new Window();
            EDSupportPanel.EDSupportPanel.Attach(window, MockServices(), Substitute.For<IPopupDisplayer>());

            Assert.True(EDSupportPanel.EDSupportPanel.IsAttached(window));
            Assert.False(EDSupportPanel.EDSupportPanel.ShouldAttach(window));
        });
    }

    private sealed class FakeOverlayWindow : Window, IOverlayWindow;

    private class PanelTestViewModel(EDServices services) : EDViewModel.EDViewModel(services)
    {
        public bool PanelEnabled
        {
            get => ShowSupportPanel;
            init => ShowSupportPanel = value;
        }
    }

}
