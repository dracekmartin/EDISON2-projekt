using System;
using System.Globalization;
using System.Reflection;

namespace TemplateDesigner;

/// <summary>
/// Reflexní most mezi EDInputForm Field.Value (string) a typovanými properties
/// elementu. Klíč pole = property path (dot notation, např. "Style.FontSize").
///
/// Používá generický <see cref="ElementEditDialog"/> — eliminuje per-element
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
    /// — neaplikují se ani nevytvoří command.
    /// </summary>
    public static bool Exists(object root, string path)
    {
        var (parent, prop) = Resolve(root, path);
        return parent is not null && prop is not null && prop.CanWrite;
    }

    /// <summary>Zapíše string hodnotu do property elementu přes path (s type conversion).
    /// Null mezičlánky na cestě se lazy alokují (např. nullable <c>HeaderLine2Style</c>) — jen při
    /// zápisu (Get/Exists nealokují → otevření dialogu / Cancel element nemutuje).</summary>
    public static void Set(object root, string path, string? stringValue)
    {
        var (parent, prop) = Resolve(root, path, allocate: true);
        if (parent is null || prop is null || !prop.CanWrite) return;
        prop.SetValue(parent, FromStringValue(stringValue ?? "", prop.PropertyType));
    }

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
