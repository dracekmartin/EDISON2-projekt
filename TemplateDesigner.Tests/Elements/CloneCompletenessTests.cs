using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Reflexní zámek úplnosti <see cref="TemplateElement.Clone"/> napříč <b>všemi</b> konkrétními
/// elementy. Chytá dvě historické pasti (viz analýza jádra tabulek):
/// <list type="bullet">
///   <item>zapomenutá property v ručně vypsaném initializeru <c>Clone()</c> (Trap B),</item>
///   <item>nezavolaný layered helper (<c>CopyStopScheduleProperties</c> / <c>CopyScheduleBaseProperties</c>)
///         → styly a DataSource klíče se tiše nenaklonují (Trap A).</item>
/// </list>
/// Princip: každá perzistentní property originálu se zmutuje na ne-default hodnotu, element se
/// naklonuje a porovná se serializovaný JSON originálu vs klonu (mimo vlastní <c>Id</c>, které
/// <c>Clone</c> záměrně přegeneruje). Zapomenutá property zůstane na klonu default → JSON se liší.
/// Nový typ elementu je pokrytý automaticky (enumerace přes <c>[JsonDerivedType]</c>).
/// </summary>
public class CloneCompletenessTests
{
    /// <summary>Konkrétní typy elementů deklarované pro serializaci na <see cref="TemplateElement"/>.</summary>
    private static readonly Type[] DeclaredElementTypes =
        typeof(TemplateElement).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(a => a.DerivedType).ToArray();

    public static TheoryData<Type> AllElementTypes
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var t in DeclaredElementTypes) data.Add(t);
            return data;
        }
    }

    /// <summary>
    /// Pojistka, že enumerace přes <c>[JsonDerivedType]</c> pokrývá přesně konkrétní ne-abstraktní
    /// potomky <see cref="TemplateElement"/>. Nový element bez registrace (nebo registrace bez
    /// třídy) tu spadne - Clone test by ho jinak tiše nepokryl.
    /// </summary>
    [Fact]
    public void RegistrovaneTypy_PokryvajiVsechnyKonkretniElementy()
    {
        var concrete = typeof(TemplateElement).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(TemplateElement)) && !t.IsAbstract)
            .OrderBy(t => t.FullName)
            .ToArray();

        Assert.Equal(
            concrete.Select(t => t.FullName),
            DeclaredElementTypes.OrderBy(t => t.FullName).Select(t => t.FullName));
    }

    [Theory]
    [MemberData(nameof(AllElementTypes))]
    public void Clone_ZachovaVsechnyPerzistentniProperties(Type elementType)
    {
        var original = (TemplateElement)Activator.CreateInstance(elementType)!;
        Mutate(original, depth: 0);

        var clone = original.Clone();

        // Clone MUSÍ přegenerovat Id (guard, že se vůbec zavolal CopyBaseTo)
        Assert.NotEqual(original.Id, clone.Id);
        Assert.IsType(elementType, clone);

        // Neutralizuj záměrný rozdíl (nové Id) a porovnej zbytek perzistentního stavu
        clone.Id = original.Id;
        Assert.Equal(SerializeElement(original), SerializeElement(clone));
    }

    // ── Serializace jednoho elementu (zabaleného do minimální šablony) ─────────

    // Fixní Id wrapperu - jinak by každé volání dalo nové Guid a JSON by se lišil v šumu obalu
    private static readonly Guid FixedTemplateId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FixedSectionId = new("22222222-2222-2222-2222-222222222222");

    private static string SerializeElement(TemplateElement element)
    {
        var section = new Section { Id = FixedSectionId, Name = "Test", DesignerHeight = 50f };
        section.Elements.Add(element);
        var template = new Template { Id = FixedTemplateId, Name = "CloneTest", Sections = [section] };
        var node = JsonNode.Parse(TemplateSerializer.Serialize(template))!.AsObject();
        node.Remove("modifiedAt");   // Serialize razítkuje UtcNow - z porovnání vyřadit
        node.Remove("createdAt");    // razítkuje se při vzniku Template → z porovnání vyřadit
        return node.ToJsonString();
    }

    // ── Reflexní mutátor: každou perzistentní property nastaví na ne-default ───

    private static void Mutate(object obj, int depth)
    {
        if (depth > 2) return;   // strop rekurze (nested styly stačí do 2. úrovně)

        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;
            if (prop.PropertyType == typeof(Guid)) continue;   // Id - Clone ho přegeneruje záměrně

            object? current = prop.CanRead ? prop.GetValue(obj) : null;

            // Kolekce a slovníky bez setteru mutujeme in-place
            if (!prop.CanWrite)
            {
                MutateInPlace(current, depth);
                continue;
            }

            object? next = NextValue(prop.PropertyType, current, depth);
            if (next is not null || IsNullableReference(prop.PropertyType))
                prop.SetValue(obj, next);
        }
    }

    private static void MutateInPlace(object? collection, int depth)
    {
        switch (collection)
        {
            case IDictionary dict:
                dict[$"k{dict.Count}"] = "v";
                break;
            case IList list:
                var itemType = list.GetType().GetGenericArguments().FirstOrDefault();
                var item = itemType is not null ? TryCreate(itemType, depth) : null;
                if (item is not null) list.Add(item);
                break;
        }
    }

    /// <summary>Vrátí ne-default hodnotu pro daný typ (odvozenou od aktuální, ať je vždy odlišná).</summary>
    private static object? NextValue(Type type, object? current, int depth)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(bool)) return !(current as bool? ?? false);
        if (t == typeof(int)) return (current as int? ?? 0) + 1;
        if (t == typeof(long)) return (current as long? ?? 0) + 1;
        if (t == typeof(float)) return (current as float? ?? 0f) + 1.5f;
        if (t == typeof(double)) return (current as double? ?? 0d) + 1.5d;
        if (t == typeof(string)) return current is string s && s.Length > 0 ? s + "_X" : "CLONE_TEST";
        if (t.IsEnum) return NextEnum(t, current);

        // Slovník / seznam
        if (typeof(IDictionary).IsAssignableFrom(t) || (t.IsGenericType && typeof(IEnumerable).IsAssignableFrom(t) && t != typeof(string)))
        {
            var coll = current ?? TryCreate(t, depth);
            MutateInPlace(coll, depth);
            return coll;
        }

        // Vnořený POCO (TextStyle, ColumnVisualStyle, …) - vytvoř a rekurzivně zmutuj
        if (t.IsClass)
        {
            var nested = TryCreate(t, depth);
            if (nested is not null) Mutate(nested, depth + 1);
            return nested;
        }

        return null;
    }

    private static object NextEnum(Type enumType, object? current)
    {
        var values = Enum.GetValues(enumType).Cast<object>().ToArray();
        foreach (var v in values)
            if (!Equals(v, current)) return v;
        return values[0];
    }

    private static object? TryCreate(Type t, int depth)
    {
        try { return Activator.CreateInstance(t); }
        catch { return null; }
    }

    private static bool IsNullableReference(Type type) =>
        !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
}
