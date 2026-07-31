using System;
using System.Collections.Generic;
using System.Linq;
using EDControls;
using EDControls.EDInputForm;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;

namespace TemplateDesigner;

/// <summary>
/// Most mezi EDInputFormem a libovolným cílovým objektem (element/sekce/stránka) přes reflexi
/// (<see cref="ElementPropertyAccessor"/>, field key = property path).
///
/// Sdílené pro samostatný <see cref="ElementEditDialog"/> i pro embedded EDInputForm v tabu
/// „Obecné" komplexních dialogů (StopColumnsEditorDialog, DepartureGroupEditorDialog) - tam je
/// EDInputForm jen část okna vedle custom editoru sloupců.
/// </summary>
public static class ElementFormBinder
{
    /// <summary>
    /// Naplní Field.Value z cílového objektu (reflexe). Vrátí snapshot pre-filled hodnot
    /// pro pozdější change detection. Volat po EDInputForm.SchemaLoaded (schema načteno).
    /// Pokud property nemá hodnotu, nechá Caché schema default (řeší nullable „inherit" props).
    /// </summary>
    // Nainstalované fonty (z FontResolver = SKFontManager, tentýž zdroj jako renderer). Cachované.
    private static List<ListItem>? _fontItems;
    private static List<ListItem> FontItems => _fontItems ??=
        Models.FontResolver.InstalledFamilies.Select(s => new ListItem(s, s)).ToList();

    /// <summary>Klientsky naplní seznam nainstalovaných fontů do ComboBox polí (klíč končí "FontFamily").
    /// Caché ten seznam nezná (OS věc). Volat po EDInputForm.SchemaLoaded (před/spolu s Prefill) - set
    /// proběhne dřív, než WPF realizuje kontejner ComboBoxu, takže se binding chytí i bez change-notifikace.
    /// Sdílené napříč všemi hosty EDInputFormu (ElementEditDialog + embedded v komplexních dialozích).</summary>
    public static void PopulateFontFields(EDInputForm form)
    {
        foreach (var field in (form.Items ?? []).OfType<Field>())
            if (field.FieldType == EFieldType.ComboBox
                && field.Key.EndsWith("FontFamily", StringComparison.Ordinal))
                field.ListItems = FontItems;
    }

    private const string FamilySuffix = "FontFamily";
    private const string WeightSuffix = "FontWeight";

    /// <summary>Klientsky naplní „Řez" (List pole s klíčem končícím "FontWeight") reálnými váhami
    /// sourozenecké rodiny ("…FontFamily") ze sdíleného <see cref="Models.FontWeightCatalog"/>. Volat
    /// PO <see cref="Prefill"/> (kdy je hodnota rodiny známá). Caché řezy nezná (OS věc, jako fonty).</summary>
    public static void PopulateFontWeightFields(EDInputForm form)
    {
        var fields = (form.Items ?? []).OfType<Field>().ToList();
        foreach (var field in fields)
        {
            if (!IsWeightField(field)) continue;
            var familyKey = field.Key[..^WeightSuffix.Length] + FamilySuffix;
            field.ListItems = WeightItems(fields.FirstOrDefault(f => f.Key == familyKey)?.Value);
        }
    }

    /// <summary>Napojí přepočet nabídky řezů na změnu rodiny písma (volat jednou u hosta, který
    /// nemá vlastní obsluhu změn polí - typicky modální dialog). Hosté s vlastní obsluhou volají
    /// <see cref="SyncFontWeightAfterFamilyChange(EDInputForm, string)"/> přímo, ať mají pořadí
    /// vůči svému zpracování změny pod kontrolou.</summary>
    public static void HookFontWeightRefresh(EDInputForm form)
        => form.FieldChanged += key => SyncFontWeightAfterFamilyChange(form, key);

    /// <summary>Po změně rodiny písma přepočítá nabídku řezů sourozeneckého pole „Řez".</summary>
    public static bool SyncFontWeightAfterFamilyChange(EDInputForm form, string changedKey)
        => SyncFontWeightAfterFamilyChange(
            [.. (form.Items ?? []).OfType<Field>()], changedKey, allowValueReset: _prefilling == 0);

    /// <summary>
    /// Přepočítá nabídku řezů u pole sourozeneckého ke změněné rodině a zahodí řez, který nová
    /// rodina reálně nemá - jinak by ve formuláři visela hodnota, kterou renderer při kreslení
    /// nahradí nejbližší dostupnou (tisk by nesouhlasil s tím, co je v dialogu napsané).
    /// Zahazuje se jen podle **skutečně známých** řezů: neznámá / nezadaná rodina (dědí se, nebo
    /// překlep) → hodnota se nechává, protože nabídka je v tu chvíli jen odhad.
    /// </summary>
    /// <param name="allowValueReset">False při programovém plnění formuláře - <see cref="Prefill"/>
    /// nastavuje hodnoty polí, což vyvolá stejné ohlášení změny jako uživatelův zápis; bez tohoto
    /// rozlišení by pouhé otevření dialogu tiše zahodilo uložený řez.</param>
    /// <returns>True, pokud se hodnota řezu zahodila.</returns>
    internal static bool SyncFontWeightAfterFamilyChange(
        IReadOnlyList<Field> fields, string changedKey, bool allowValueReset)
    {
        if (!changedKey.EndsWith(FamilySuffix, StringComparison.Ordinal)) return false;

        var weightKey = changedKey[..^FamilySuffix.Length] + WeightSuffix;
        var weightField = fields.FirstOrDefault(f => f.Key == weightKey && IsWeightField(f));
        if (weightField is null) return false;

        var family = fields.FirstOrDefault(f => f.Key == changedKey)?.Value;
        weightField.ListItems = WeightItems(family);

        if (!allowValueReset || string.IsNullOrEmpty(weightField.Value)) return false;

        var known = Models.FontWeightCatalog.InstalledWeights(family);
        if (known.Count == 0 || known.Any(w => w.ToString() == weightField.Value)) return false;

        weightField.Value = "";
        return true;
    }

    private static bool IsWeightField(Field f) =>
        f.FieldType == EFieldType.List && f.Key.EndsWith(WeightSuffix, StringComparison.Ordinal);

    private static List<ListItem> WeightItems(string? family) =>
        Models.FontWeightCatalog.Items(family)
            .Select(x => new ListItem(x.Weight.ToString(), x.Label)).ToList();

    // ── Barvy: kontrola vstupu ────────────────────────────────────────────────────
    // Barva se zadává jako volný text, takže do formuláře může přijít cokoli. Neplatnou hodnotu
    // nesmíme uložit do šablony: kreslení ji sice po opravě fail-soft přežije (viz ColorHex), ale
    // v šabloně by zůstal nesmysl a uživatel by nevěděl, že jeho barva nikam nedošla.

    // Hlášku u pole (červený rámeček) řeší typová kontrola v EDInputFormu (Field.GetTypeError) -
    // musí být tam, protože serverová validace při ukládání přestaví seznam polí a hlášku nastavenou
    // odsud by zahodila. Tady zůstává jen pojistka, že se neplatná hodnota nedostane do modelu.

    private static bool IsColorField(Field f) => f.FieldType == EFieldType.Color;

    /// <summary>Neprázdná hodnota, kterou nelze přečíst jako barvu. Prázdno = „vymazáno", ne chyba.</summary>
    private static bool IsInvalidColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !Models.ColorHex.IsValid(value);

    /// <summary>Snapshot aktuálních hodnot polí formuláře (bez zápisu do cíle) - pro update
    /// change-detection baseline po živém write-backu v inline panelu.</summary>
    public static Dictionary<string, string> Snapshot(EDInputForm form)
    {
        var snap = new Dictionary<string, string>();
        foreach (var field in (form.Items ?? []).OfType<Field>())
            snap[field.Key] = field.Value ?? "";
        return snap;
    }

    // >0 = běží programové plnění formuláře. Zápis Field.Value hlásí změnu stejně jako uživatelův
    // zápis, takže bez tohoto rozlišení by obsluhy změn (přepočet řezů) považovaly plnění za edit.
    private static int _prefilling;

    public static Dictionary<string, string> Prefill(EDInputForm form, object target)
    {
        _prefilling++;
        try
        {
            var preFilled = new Dictionary<string, string>();
            foreach (var field in (form.Items ?? []).OfType<Field>())
            {
                var reflected = ElementPropertyAccessor.Get(target, field.Key);
                var display = !string.IsNullOrEmpty(reflected) ? reflected : (field.Value ?? "");
                field.Value = display;
                preFilled[field.Key] = display;
            }
            return preFilled;
        }
        finally { _prefilling--; }
    }

    /// <summary>
    /// Vytvoří SetPropertyCommandy pro pole, která se reálně změnila proti <paramref name="preFilled"/>.
    /// Virtuální driver pole (klíč není property cíle) přeskočí. Volat po úspěšném EDInputForm.Save().
    /// </summary>
    public static List<IDesignerCommand> ExtractCommands(
        EDInputForm form, object target, Dictionary<string, string> preFilled)
        => ExtractCommands([.. (form.Items ?? []).OfType<Field>()], target, preFilled);

    /// <summary>Testovatelné jádro <see cref="ExtractCommands(EDInputForm, object, Dictionary{string, string})"/>
    /// nad holým seznamem polí (bez WPF kontrolky).</summary>
    internal static List<IDesignerCommand> ExtractCommands(
        List<Field> fields, object target, Dictionary<string, string> preFilled)
    {
        var commands = new List<IDesignerCommand>();
        foreach (var field in fields)
        {
            var path = field.Key;
            string? newVal = field.Value ?? "";
            if (newVal == preFilled.GetValueOrDefault(path, "")) continue;
            if (!ElementPropertyAccessor.Exists(target, path)) continue;

            if (IsColorField(field))
            {
                // Nesmyslnou barvu do modelu nepustit (hlášku u pole hlásí EDInputForm).
                if (IsInvalidColor(newVal)) continue;

                // Vymazaná barva: u zděditelné (nullable) znamená „dědit" → null, u ostatních
                // „nic nekreslit" → průhledná. Prázdný string by byl uložený nesmysl.
                if (string.IsNullOrWhiteSpace(newVal))
                    newVal = ElementPropertyAccessor.AcceptsNull(target, path) ? null : Models.ColorHex.Transparent;
            }

            var oldVal = ElementPropertyAccessor.Get(target, path);
            commands.Add(new SetPropertyCommand(
                field.Label, oldVal, newVal,
                v => ElementPropertyAccessor.Set(target, path, (string?)v)));
        }
        return commands;
    }
}
