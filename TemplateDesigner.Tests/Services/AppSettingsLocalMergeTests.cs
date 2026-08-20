using TemplateDesigner.Config;

namespace TemplateDesigner.Tests.Services;

/// <summary>
/// Sloučení lokálního nastavení (soubor vedle .exe) do konfigurace aplikace. Slučuje se
/// vyjmenovaně, ne celý objekt - zapomenutý oddíl se **tiše zahodí** a featura se tváří jako
/// vypnutá. Přesně tak zmizel klíč k distribučnímu kanálu: v souboru byl, v aplikaci ne
/// a přehled šablon nic nenabídl (19. 8.). Nový oddíl konfigurace patří sem i do testu.
/// </summary>
public class AppSettingsLocalMergeTests
{
    private static (AppSettings Target, AppSettings Local) Dvojice()
    {
        var target = new AppSettings();
        var local = new AppSettings();
        return (target, local);
    }

    [Fact]
    public void KlicKeKanaluSePrenese()
    {
        var (target, local) = Dvojice();
        local.TemplateChannel.ApiKey = "tajny-klic";

        AppSettings.MergeLocal(target, local);

        Assert.Equal("tajny-klic", target.TemplateChannel.ApiKey);
    }

    [Fact]
    public void AdresaKanaluSePrenese()
    {
        var (target, local) = Dvojice();
        local.TemplateChannel.BaseUrl = "https://jiny.server";

        AppSettings.MergeLocal(target, local);

        Assert.Equal("https://jiny.server", target.TemplateChannel.BaseUrl);
    }

    /// <summary>Prázdná hodnota v lokálním souboru nesmí přemazat to, co je v základní konfiguraci.</summary>
    [Fact]
    public void PrazdnaHodnotaZakladNeprepise()
    {
        var (target, local) = Dvojice();
        target.TemplateChannel.ApiKey = "z-baseline";
        local.TemplateChannel.ApiKey = "";

        AppSettings.MergeLocal(target, local);

        Assert.Equal("z-baseline", target.TemplateChannel.ApiKey);
    }

    [Fact]
    public void VypnutiKanaluSePrenese()
    {
        var (target, local) = Dvojice();
        local.TemplateChannel.Enabled = false;

        AppSettings.MergeLocal(target, local);

        Assert.False(target.TemplateChannel.Enabled);
    }

    /// <summary>Klíč k AI se slučoval už dřív - ať se to nerozbije při přidávání dalších oddílů.</summary>
    [Fact]
    public void KlicKAiSePoradPrenese()
    {
        var (target, local) = Dvojice();
        local.AiAssistant.AnthropicApiKey = "sk-ant-test";

        AppSettings.MergeLocal(target, local);

        Assert.Equal("sk-ant-test", target.AiAssistant.AnthropicApiKey);
    }
}
