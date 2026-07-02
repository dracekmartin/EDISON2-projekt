using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Round-trip idempotence: Serialize → Deserialize → Serialize musí dát identický JSON.
/// Chytá zapomenuté properties v serializaci, rozbité polymorfní mapování
/// (JsonDerivedType) a drift RoundedFloatConverter.
/// </summary>
public class TemplateSerializerRoundTripTests
{
    /// <summary>Jedna instance od každého typu elementu (registrovaného v ElementTypes).</summary>
    public static TheoryData<string> AllElementTypes => new(
        ElementTypes.Text, ElementTypes.Rectangle, ElementTypes.Line,
        ElementTypes.Image, ElementTypes.Table, ElementTypes.Barcode,
        ElementTypes.StopList, ElementTypes.DepartureGrid, ElementTypes.NotesLegend);

    private static TemplateElement CreateElement(string type) => type switch
    {
        ElementTypes.Text          => new TextElement { Text = "Ahoj {POLE}" },
        ElementTypes.Rectangle     => new RectangleElement(),
        ElementTypes.Line          => new LineElement(),
        ElementTypes.Image         => new ImageElement(),
        ElementTypes.Table         => new TableElement(),
        ElementTypes.Barcode       => new BarcodeElement(),
        ElementTypes.StopList      => new StopListElement(),
        ElementTypes.DepartureGrid => new DepartureGridElement(),
        ElementTypes.NotesLegend   => new NotesLegendElement(),
        _ => throw new ArgumentException($"Neznámý typ elementu '{type}' — doplň do testu.", nameof(type))
    };

    private static Template TemplateWith(TemplateElement element)
    {
        element.X = 10.5f;
        element.Y = 20f;
        element.Width = 80f;
        var section = new Section { Name = "Test", DesignerHeight = 50f };
        section.Elements.Add(element);
        return new Template { Name = "RoundTrip", Sections = [section] };
    }

    [Theory]
    [MemberData(nameof(AllElementTypes))]
    public void KazdyTypElementu_RoundTripJeIdempotentni(string elementType)
    {
        var template = TemplateWith(CreateElement(elementType));

        string json1 = TemplateSerializer.Serialize(template);
        var reloaded = TemplateSerializer.Deserialize(json1);
        string json2 = TemplateSerializer.Serialize(reloaded);

        // Serialize() záměrně razítkuje ModifiedAt = UtcNow → timestamp z porovnání vyřadit
        Assert.Equal(StripModifiedAt(json1), StripModifiedAt(json2));
    }

    private static string StripModifiedAt(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("modifiedAt");
        return node.ToJsonString();
    }

    [Fact]
    public void JsonSNeznamouProperty_SeNactoBezChyby()
    {
        // Forward tolerance — šablona z novější verze s neznámým polem nesmí spadnout
        var template = TemplateWith(new TextElement { Text = "x" });
        string json = TemplateSerializer.Serialize(template);
        json = json.Replace("\"name\": \"RoundTrip\"",
            "\"name\": \"RoundTrip\", \"neznamaVlastnost\": 123");

        var reloaded = TemplateSerializer.Deserialize(json);

        Assert.Equal("RoundTrip", reloaded.Name);
    }

    [Fact]
    public void RoundedFloatConverter_OrezavaFloatArtefakty()
    {
        var template = TemplateWith(new TextElement { Text = "x" });
        template.Sections[0].Elements[0].X = 0.5000001f;

        string json = TemplateSerializer.Serialize(template);

        Assert.Contains("\"x\": 0.5", json);
        Assert.DoesNotContain("0.5000001", json);
    }

    [Fact]
    public void LegacyAgendaKodMHD_SePremapujeNaKanonicky()
    {
        var template = TemplateWith(new TextElement { Text = "x" });
        string json = TemplateSerializer.Serialize(template)
            .Replace("\"name\": \"RoundTrip\"", "\"name\": \"RoundTrip\", \"agendaCode\": \"MHD\"");

        var reloaded = TemplateSerializer.Deserialize(json);

        Assert.Equal("ZASTAVKOVY_JR_MHD", reloaded.AgendaCode);
        Assert.True(reloaded.WasMigrated);
    }

    [Fact]
    public void NevalidniJson_VyhodiTemplateDeserializationException()
    {
        Assert.Throws<TemplateDeserializationException>(
            () => TemplateSerializer.Deserialize("{ tohle není šablona"));
    }

    [Fact]
    public void SekceRoundTrip_ZachovaPrintBehaviorAOverflow()
    {
        var section = new Section
        {
            Name = "Patička",
            PrintBehavior = SectionPrintBehavior.OnLastPageOnly,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
            Role = SectionRole.Footer,
            VisibilityExpression = "{PAGE} > 1"
        };
        var template = new Template { Sections = [section] };

        var reloaded = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(template));

        var s = Assert.Single(reloaded.Sections);
        Assert.Equal(SectionPrintBehavior.OnLastPageOnly, s.PrintBehavior);
        Assert.Equal(SectionOverflowBehavior.SplitAcrossPages, s.OverflowBehavior);
        Assert.Equal(SectionRole.Footer, s.Role);
        Assert.Equal("{PAGE} > 1", s.VisibilityExpression);
    }
}
