using System;
using System.Globalization;
using System.Reflection;

namespace TemplateDesigner;

/// <summary>
/// Reflexní most mezi EDInputForm Field.Value (string) a typovanými properties
/// elementu. Klíč pole = property path (dot notation, např. "Style.FontSize").
///
/// Používá generický <see cref="ElementEditDialog"/> - eliminuje per-element
/// pre-fill/extract boilerplate. Type conversion: string ↔ float/double/int/bool/
/// enum/nullable. Color je string property → as-is.
/// </summary>
public static class ElementPropertyAccessor
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Přečte property z elementu přes path a vrátí jako string (pro Field.Value).</summary>
    public static string Get(object root, string path)
    {
        var (parent, prop) = Resolve(root, path);
        if (parent is null || prop is null) return "";
        return ToStringValue(prop.GetValue(parent));
    }

    /// <summary>
    /// True pokud path ukazuje na reálnou zapisovatelnou property. False pro „virtuální"
    /// pole, která nemají property na cílovém objektu (např. driver pole „Format"/„Orientation",
    /// jejichž jediným účelem je řídit Validate logiku server-side). Volající je pak přeskočí
    /// - neaplikují se ani nevytvoří command.
    /// </summary>
    public static bool Exists(object root, string path)
    {
        // Traverzace podle TYPU, ne podle hodnoty - jinak by cesta s null mezičlánkem
        // (např. DefaultTextStyle == null) vrátila false a změna by se zahodila, i když je
        // property reálně zapisovatelná (Set ji při zápisu lazy alokuje). Virtuální driver pole
        // (klíč není property typu) tak dál správně vrací false.
        var parts = path.Split('.');
        var type = root.GetType();
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var p = type.GetProperty(parts[i]);
            if (p is null) return false;
            type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        }
        var final = type.GetProperty(parts[^1]);
        return final is not null && final.CanWrite;
    }

    /// <summary>Zapíše string hodnotu do property elementu přes path (s type conversion).
    /// Null mezičlánky na cestě se lazy alokují (např. nullable <c>HeaderLine2Style</c>) - jen při
    /// zápisu (Get/Exists nealokují → otevření dialogu / Cancel element nemutuje).
    /// <paramref name="stringValue"/> = null zapíše null, pokud to property dovolí (jinak prázdno).</summary>
    public static void Set(object root, string path, string? stringValue)
    {
        var (parent, prop) = Resolve(root, path, allocate: true);
        if (parent is null || prop is null || !prop.CanWrite) return;

        // Explicitní null (ne prázdný string) - „hodnota se dědí" u nullable properties. Prázdný
        // string je jiná věc: u stringové property je to legitimní hodnota, ne „nezadáno".
        if (stringValue is null && AcceptsNullType(prop.PropertyType))
        {
            prop.SetValue(parent, null);
            return;
        }
        prop.SetValue(parent, FromStringValue(stringValue ?? "", prop.PropertyType));
    }

    private static readonly NullabilityInfoContext NullCtx = new();

    /// <summary>True pokud property na dané cestě smí být null (<c>Nullable&lt;T&gt;</c> nebo
    /// nullable referenční typ, např. <c>string?</c>). Traverzuje podle typů, takže funguje
    /// i s null mezičlánkem na cestě - stejný důvod jako u <see cref="Exists"/>.</summary>
    public static bool AcceptsNull(object root, string path)
    {
        var parts = path.Split('.');
        var type = root.GetType();
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var p = type.GetProperty(parts[i]);
            if (p is null) return false;
            type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        }
        var final = type.GetProperty(parts[^1]);
        if (final is null || !final.CanWrite) return false;
        if (!AcceptsNullType(final.PropertyType)) return false;
        if (Nullable.GetUnderlyingType(final.PropertyType) is not null) return true;

        // NullabilityInfoContext není thread-safe.
        lock (NullCtx) return NullCtx.Create(final).WriteState == NullabilityState.Nullable;
    }

    private static bool AcceptsNullType(Type t) =>
        !t.IsValueType || Nullable.GetUnderlyingType(t) is not null;

    /// <summary>Traverzuje path (všechny segmenty kromě posledního) → vrátí finální parent objekt + PropertyInfo.
    /// <paramref name="allocate"/>=true → null mezičlánek se vytvoří (parameterless ctor) a zapíše zpět.</summary>
    private static (object? parent, PropertyInfo? prop) Resolve(object root, string path, bool allocate = false)
    {
        var parts = path.Split('.');
        object? current = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current is null) return (null, null);
            var p = current.GetType().GetProperty(parts[i]);
            if (p is null) return (null, null);
            var next = p.GetValue(current);
            if (next is null && allocate && p.CanWrite)
            {
                try { next = Activator.CreateInstance(p.PropertyType); p.SetValue(current, next); }
                catch { return (null, null); }
            }
            current = next;
        }
        if (current is null) return (null, null);
        return (current, current.GetType().GetProperty(parts[^1]));
    }

    private static string ToStringValue(object? value) => value switch
    {
        null => "",
        float f => f.ToString(Inv),
        double d => d.ToString(Inv),
        bool b => b ? "1" : "0",
        Enum e => e.ToString(),
        _ => value.ToString() ?? ""
    };

    private static object? FromStringValue(string s, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        bool isNullable = underlying is not null;
        var t = underlying ?? targetType;

        // Nullable + prázdný string → null (např. dynamická výška, custom dash)
        if (isNullable && string.IsNullOrEmpty(s)) return null;

        if (t == typeof(string)) return s;
        if (t == typeof(float))  return float.TryParse(s, NumberStyles.Float, Inv, out var f) ? f : (isNullable ? null : 0f);
        if (t == typeof(double)) return double.TryParse(s, NumberStyles.Float, Inv, out var d) ? d : (isNullable ? null : 0d);
        if (t == typeof(int))    return int.TryParse(s, out var i) ? i : (isNullable ? null : 0);
        if (t == typeof(bool))   return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (t.IsEnum)            return Enum.TryParse(t, s, ignoreCase: true, out var e) ? e : Enum.GetValues(t).GetValue(0);
        return s;
    }
}
