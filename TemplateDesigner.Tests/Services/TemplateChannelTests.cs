using System.Text.Json;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Services;

/// <summary>
/// Kanál, kterým M-line doručuje systémové šablony z helpdesku. Identitu šablony určuje GUID
/// uvnitř JSONu, ne identifikátor v úložišti - jinak by opakované stažení zakládalo duplicity.
/// Stav „co už máme" cestuje v šabloně, takže patří databázi firmy, ne počítači uživatele.
/// </summary>
public class TemplateChannelTests
{
    [Fact]
    public void PuvodZKanaluPrezijeUlozeniACteniSablony()
    {
        var id = Guid.NewGuid();
        var cas = new DateTime(2026, 8, 14, 11, 40, 0, DateTimeKind.Utc);
        var template = new Template
        {
            Id = id,
            Name = "Zastávkový JŘ - standard",
            ChannelOrigin = new TemplateChannelOrigin("6f1b3c2e-8a41-4d7f-9c22-5b8e2f0a91d4", cas)
        };

        var znovu = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(template));

        Assert.Equal(id, znovu.Id);
        Assert.NotNull(znovu.ChannelOrigin);
        Assert.Equal("6f1b3c2e-8a41-4d7f-9c22-5b8e2f0a91d4", znovu.ChannelOrigin!.Id);
        Assert.Equal(cas, znovu.ChannelOrigin.UpdatedAt);
    }

    /// <summary>Šablona, která kanálem nepřišla (vlastní, nebo z .exe), původ nemá - a nesmí
    /// ho dostat jen tím, že projde uložením.</summary>
    [Fact]
    public void SablonaBezKanaluPuvodNema()
    {
        var znovu = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(new Template()));

        Assert.Null(znovu.ChannelOrigin);
    }

    /// <summary>Starší šablona bez toho pole se musí načíst beze změny chování (aditivní klíč).</summary>
    [Fact]
    public void StarsiSablonaBezPoleSeNacte()
    {
        string json = TemplateSerializer.Serialize(new Template { Name = "Stará" });
        Assert.DoesNotContain("channelOrigin", json, StringComparison.OrdinalIgnoreCase);

        var znovu = TemplateSerializer.Deserialize(json);

        Assert.Equal("Stará", znovu.Name);
        Assert.Null(znovu.ChannelOrigin);
    }

    /// <summary>Obálka odpovědi z helpdesku { success, data } - ať se změna tvaru pozná tady.</summary>
    [Fact]
    public void SeznamZUlozisteSeRozparsuje()
    {
        const string odpoved = """
        {
          "success": true,
          "data": [
            { "id": "6f1b3c2e", "agenda": "ZASTAVKOVY_JR", "varianta": "AD",
              "name": "Zastávkový JR - standard", "updatedAt": "2026-08-14T11:40:00.000Z" }
          ],
          "total": 1, "page": 1, "limit": 50
        }
        """;

        using var doc = JsonDocument.Parse(odpoved);
        var data = doc.RootElement.GetProperty("data");
        var polozka = JsonSerializer.Deserialize<ChannelTemplateInfo>(
            data[0].GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(polozka);
        Assert.Equal("6f1b3c2e", polozka!.Id);
        Assert.Equal("Zastávkový JR - standard", polozka.Name);
        Assert.Equal(new DateTime(2026, 8, 14, 11, 40, 0, DateTimeKind.Utc), polozka.UpdatedAt.ToUniversalTime());
    }

    /// <summary>Bez klíče se kanál mlčky přeskočí - přehled šablon musí fungovat i tak.</summary>
    [Fact]
    public async Task BezKliceKanalNicNevraci()
    {
        var klient = new TemplateChannelClient();

        Assert.Empty(await klient.ListAsync());
        Assert.Null(await klient.DownloadAsync("cokoli"));
    }
}
