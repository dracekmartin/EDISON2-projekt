using ALWA.SYS;
using System.Collections.Generic;
using System.Linq;

namespace EDControls.EDInputForm;

/// <summary>
/// Kodek wire formátu EDInputFormu (CVar ⟷ Items). Nezávislý na WPF, aby ho
/// <see cref="EDInputFormController"/> mohl použít bez controlu; pořadí pozic v CList je
/// protokol - viz komponentový CLAUDE.md.
/// </summary>
internal static class EDInputFormWire
{
    internal const char ListItemSep = '|';
    internal const char ListKeyValSep = '~';

    internal static CVar SerializeItemsCVar(IEnumerable<Item>? items)
    {
        var cVar = new CVar();
        int i = 1;
        foreach (var item in items ?? [])
        {
            CList entry = item switch
            {
                Group g => new CList("GROUP", g.Number, g.Key, g.GroupName, g.Visibility.ToWire(), g.HelpText ?? ""),
                Field f => new CList(
                    "FIELD", f.Number, f.Key, f.Label,
                    f.FieldType.ToWire(),
                    // Image hodnota (velký Base64) se NIKDY neposílá na server - MLRest
                    // StreamToGlobal to neukočíruje (<SYNTAX>/limit) a obrázek se server-side
                    // nevaliduje. Drží se jen client-side (viz EDInputFormController.ApplyOutput).
                    f.FieldType == FieldType.Image ? "" : (f.Value ?? ""),
                    f.ValidateOnChange ? "1" : "0",
                    f.Required ? "1" : "0",
                    f.ValidationError ?? "",
                    f.Editable ? "1" : "0",
                    f.Visibility.ToWire(),
                    f.HelpText ?? "",
                    SerializeListItems(f.ListItems),
                    f.FileFilter ?? "",
                    f.Min?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                    f.Max?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                    f.Format ?? "",
                    f.Unit ?? "",
                    f.Placeholders ? "1" : "0"),
                _ => new CList()
            };
            cVar[i.ToString()] = entry;
            i++;
        }
        return cVar;
    }

    internal static List<Item> ParseItems(CVar output)
    {
        var items = new List<Item>();
        Group? currentGroup = null;
        foreach (KeyValuePair<string, CVar> kvp in output)
        {
            if (!IsItemKey(kvp.Key)) continue;
            CList row = kvp.Value;
            string type = row.GetString(1);
            if (type == "GROUP")
            {
                currentGroup = new Group
                {
                    Number     = int.TryParse(row.GetString(2), out int gn) ? gn : 0,
                    Key        = row.GetString(3),
                    GroupName  = row.GetString(4),
                    Visibility = row.Length >= 5 ? ItemVisibilityWire.FromWire(row.GetString(5)) : ItemVisibility.Visible,
                    HelpText   = row.Length >= 6 && row.GetString(6) != "" ? row.GetString(6) : null,
                };
                items.Add(currentGroup);
            }
            else if (type == "FIELD")
            {
                var field = new Field
                {
                    Number           = int.TryParse(row.GetString(2), out int fn) ? fn : 0,
                    Key              = row.GetString(3),
                    Label            = row.GetString(4),
                    FieldType        = FieldTypeWire.FromWire(row.GetString(5)),
                    Value            = row.GetString(6) == "" ? null : row.GetString(6),
                    ValidateOnChange = row.GetString(7) == "1",
                    Required         = row.GetString(8) == "1",
                    ValidationError  = row.Length >= 9 && row.GetString(9) != "" ? row.GetString(9) : null,
                    Editable         = row.Length < 10 || row.GetString(10) != "0",
                    Visibility       = row.Length >= 11 ? ItemVisibilityWire.FromWire(row.GetString(11)) : ItemVisibility.Visible,
                    HelpText         = row.Length >= 12 && row.GetString(12) != "" ? row.GetString(12) : null,
                    ParentGroup      = currentGroup,
                };
                // List + ComboBox sdílejí listItems storage v pozici 13
                if ((field.FieldType == FieldType.List || field.FieldType == FieldType.ComboBox)
                    && row.Length >= 13)
                    field.ListItems = ParseListItems(row.GetString(13));
                if (field.FieldType == FieldType.File && row.Length >= 14)
                    field.FileFilter = row.GetString(14);
                // Pozice 15–18: rozšíření pro Number/Decimal (min, max, format, unit)
                if (row.Length >= 15 && decimal.TryParse(row.GetString(15),
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var minVal))
                    field.Min = minVal;
                if (row.Length >= 16 && decimal.TryParse(row.GetString(16),
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var maxVal))
                    field.Max = maxVal;
                if (row.Length >= 17 && row.GetString(17) != "")
                    field.Format = row.GetString(17);
                if (row.Length >= 18 && row.GetString(18) != "")
                    field.Unit = row.GetString(18);
                // Pozice 19: pole přijímá {POLE} placeholdery. Aditivní - starší server ji neposílá.
                if (row.Length >= 19)
                    field.Placeholders = row.GetString(19) == "1";
                items.Add(field);
            }
        }
        return items;
    }

    /// <summary>Položky jsou jen na číselných subscriptech; pojmenované klíče (ERR, helpTextMode, nový výstup
    /// serveru) se přeskočí, aby je starší klient snesl.</summary>
    internal static bool IsItemKey(string key) => int.TryParse(key, out _);

    internal static HelpTextMode ParseHelpTextMode(CVar output)
    {
        string? wire = output["helpTextMode"]?.Value?.ToString();
        return HelpTextModeWire.FromWire(wire);
    }

    internal static string SerializeListItems(List<ListItem> items)
    {
        if (items.Count == 0) return "";
        return string.Join(ListItemSep.ToString(),
            items.Select(i => i.Key == i.Label ? i.Key : i.Key + ListKeyValSep + i.Label));
    }

    internal static List<ListItem> ParseListItems(string wire)
    {
        if (string.IsNullOrEmpty(wire)) return [];
        return wire.Split(ListItemSep)
            .Where(entry => entry.Length > 0)
            .Select(entry =>
            {
                int sep = entry.IndexOf(ListKeyValSep);
                return sep >= 0
                    ? new ListItem(entry[..sep], entry[(sep + 1)..])
                    : new ListItem(entry, entry);
            })
            .ToList();
    }
}
