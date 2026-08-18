using System.Reflection;
using System.Text.Json.Serialization;
using TemplateDesigner;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Drift-pojistka registrace typů elementů. Nový typ elementu se registruje na ~8 místech
/// (ElementTypes, [JsonDerivedType], CreateElementByType + GetDefaultElementSize, SkiaHeightCalculator,
/// CapabilityCatalog, menu/paleta). Nejzrádnější je factory: <see cref="TemplateDesignerViewModel.CreateElementByType"/>
/// má <c>_ =&gt; TextElement</c> fallback → zapomenutý case tiše vrátí TextElement místo chyby.
/// Tenhle test to odhalí - pro každý deklarovaný <c>[JsonDerivedType]</c> ověří, že factory vrátí
/// přesně ten typ.
///
/// Scope = factory (CreateElementByType, které volá i GetDefaultElementSize). Paleta/menu (WPF) a
/// SkiaHeightCalculator (Skia závislost) jsou mimo - netestovatelné bez UI/Skia; factory je nejlevnější
/// a nejcennější strážce (drift CapabilityCatalog / LineTable byl přesně tenhle typ chyby).
/// </summary>
public class ElementRegistrationDriftTests
{
    /// <summary>Typy záměrně serializovatelné, ale NEvytvářené z palety (load-only). Při oživení vyřaď.</summary>
    private static readonly HashSet<string> DormantDiscriminators = new(StringComparer.Ordinal);

    private static IEnumerable<(Type Type, string Discriminator)> DeclaredTypes() =>
        typeof(TemplateElement).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(a => (a.DerivedType, (string)a.TypeDiscriminator!));

    public static TheoryData<string, string> CreatableTypes
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (type, disc) in DeclaredTypes())
                if (!DormantDiscriminators.Contains(disc))
                    data.Add(disc, type.FullName!);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CreatableTypes))]
    public void CreateElementByType_VratiPresnyTyp(string discriminator, string expectedTypeName)
    {
        var el = TemplateDesignerViewModel.CreateElementByType(discriminator, 0, 0);
        Assert.Equal(expectedTypeName, el.GetType().FullName);
    }

    /// <summary>Allowlist uspaných typů nesmí zplesnivět - každý musí být pořád deklarovaný [JsonDerivedType].</summary>
    [Fact]
    public void DormantAllowlist_JsouStaleDeklarovane()
    {
        var declared = DeclaredTypes().Select(t => t.Discriminator).ToHashSet(StringComparer.Ordinal);
        foreach (var d in DormantDiscriminators)
            Assert.Contains(d, declared);
    }
}
