using System;
using System.Collections.Generic;
using System.Linq;
using EDControls;
using EDControls.EDInputForm;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;

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
            if (field.FieldType == FieldType.ComboBox
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
        f.FieldType == FieldType.List && f.Key.EndsWith(WeightSuffix, StringComparison.Ordinal);

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

    private static bool IsColorField(Field f) => f.FieldType == FieldType.Color;

    /// <summary>Neprázdná hodnota, kterou nelze přečíst jako barvu. Prázdno = „vymazáno", ne chyba.</summary>
    private static bool IsInvalidColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !Models.ColorHex.IsValid(value);

    /// <summary>
    /// Schová pole, která podle pravidla hostitele nic nedělají, a s nimi skupinu, ve které žádné
    /// viditelné pole nezbylo - jinak by po ní zůstal prázdný nadpis. Null pravidlo = vše vidět.
    /// </summary>
    /// <remarks>Pole patří ke skupině, která je ve schématu před ním (formulář přijde z Caché
    /// v pořadí skupina, její pole, další skupina). Skupinu, kterou si uživatel sbalil, nechá
    /// sbalenou; rozbalí jen tu, kterou dřív schovalo pravidlo.</remarks>
    public static void PouzijViditelnost(IEnumerable<Item>? items,
        Func<string, IReadOnlyDictionary<string, string>, bool>? pravidlo)
    {
        var seznam = (items ?? []).ToList();
        var hodnoty = seznam.OfType<Field>().ToDictionary(f => f.Key, f => f.Value ?? "");
        Group? skupina = null;
        var skupiny = new Dictionary<Group, (int Poli, int Viditelnych)>();

        foreach (var item in seznam)
        {
            if (item is Group g) { skupina = g; skupiny.TryAdd(g, (0, 0)); continue; }
            if (item is not Field f) continue;
            bool viditelne = pravidlo is null || pravidlo(f.Key, hodnoty);
            var cil = viditelne ? ItemVisibility.Visible : ItemVisibility.Collapsed;
            if (f.Visibility != cil) f.Visibility = cil;
            if (skupina is not null)
            {
                var (poli, vid) = skupiny[skupina];
                skupiny[skupina] = (poli + 1, vid + (viditelne ? 1 : 0));
            }
        }

        foreach (var (g, (poli, viditelnych)) in skupiny)
        {
            if (poli > 0 && viditelnych == 0)
                g.Visibility = ItemVisibility.ProgrammerHidden;
            else if (g.Visibility == ItemVisibility.ProgrammerHidden)
                g.Visibility = ItemVisibility.Visible;
        }
    }

    /// <summary>Klíč podmínky viditelnosti řádku (Seznam zastávek, Tabulka spojů).</summary>
    public const string ViditelnostRadkuKlic = "RowVisibilityExpression";

    private const string PoleRadkuUvod = " Pole řádku: ";

    /// <summary>
    /// Doplní do nápovědy podmínky viditelnosti řádku pole, která řádek v agendě šablony opravdu nese
    /// (kolekce <paramref name="kolekce"/> ze schématu). Formulář je společný všem agendám, takže pevný
    /// seznam by v některé radil pole, která data nemají (`B194a` C7). Bez schématu zůstane obecný text.
    /// </summary>
    public static void DoplnPoleRadku(IEnumerable<Item>? items, Models.Schema.DataSchema? schema, string kolekce)
    {
        if ((items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == ViditelnostRadkuKlic) is not { } pole) return;
        string zaklad = pole.HelpText ?? "";
        int konec = zaklad.IndexOf(PoleRadkuUvod, StringComparison.Ordinal);
        if (konec >= 0) zaklad = zaklad[..konec];   // re-Prefill panelu volá úpravu znovu
        var nazvy = schema?.FieldCollections?.FirstOrDefault(c => c.CollectionKey == kolekce)?.Fields
            .Select(f => f.FieldName).ToList();
        pole.HelpText = nazvy is { Count: > 0 } ? $"{zaklad}{PoleRadkuUvod}{string.Join(", ", nazvy)}." : zaklad;
    }

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

    /// <summary>
    /// Přepíše výchozí hodnoty polí hodnotami z kaskády - pro cíl, jehož „nenastaveno" znamená
    /// dědění (buňka tabulky dědí z nastavení celé tabulky). Volat PŘED <see cref="Prefill"/>:
    /// ten prázdnou reflexi doplní právě z <c>Field.Value</c>, takže formulář ukáže, jak cíl reálně
    /// vypadá, a protože se stejná hodnota stane baselinou změn, potvrzení beze změny nic nezapíše.
    /// Bez toho by formulář nabízel výchozí hodnoty ze schématu, které pro buňku nic neznamenají.
    /// </summary>
    public static void ApplyInheritedValues(EDInputForm form, IReadOnlyDictionary<string, string>? inherited)
        => ApplyInheritedValues([.. (form.Items ?? []).OfType<Field>()], inherited);

    internal static void ApplyInheritedValues(List<Field> fields, IReadOnlyDictionary<string, string>? inherited)
    {
        if (inherited is null) return;
        foreach (var field in fields)
            if (inherited.TryGetValue(field.Key, out var value)) field.Value = value;
    }

    public static Dictionary<string, string> Prefill(EDInputForm form, object target)
        => Prefill([.. (form.Items ?? []).OfType<Field>()], target);

    /// <summary>Testovatelné jádro <see cref="Prefill(EDInputForm, object)"/> nad holým seznamem
    /// polí (bez WPF kontrolky).</summary>
    internal static Dictionary<string, string> Prefill(List<Field> fields, object target)
    {
        _prefilling++;
        try
        {
            var preFilled = new Dictionary<string, string>();
            foreach (var field in fields)
            {
                // Povinnost u pole, které na cíli nemá property, by nešla splnit: prefill nemá co
                // načíst a hodnotu stejně zahodí ExtractCommands. Potkává to starší program po
                // přidání pole do schématu (schéma jde na server hned, program až s vydáním) -
                // bez tohohle formulář nepustí OK a nejde uložit ani zbytek. Serverová Validate
                // platí dál, tohle ruší jen klientskou hlášku.
                if (field.Required && !ElementPropertyAccessor.Exists(target, field.Key))
                    field.Required = false;

                var reflected = ElementPropertyAccessor.Get(target, field.Key);
                var display = !string.IsNullOrEmpty(reflected) ? reflected : (field.Value ?? "");
                field.Value = display;
                // Barva z dat ({BARVA_LINKY}): čtvereček dostane vyřešenou barvu jako NÁHLED, hodnota
                // zůstane tokenem. Jinak vypadá pole jako nenastavená bílá (#12997). Bez dat zůstane
                // náhled prázdný a komponenta ukáže šrafování „z dat".
                field.PreviewValue = ColorHex.ResolveDataBoundForUi(display);
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

            // Nesmyslnou barvu do modelu nepustit (hlášku u pole hlásí EDInputForm).
            if (IsColorField(field) && IsInvalidColor(newVal)) continue;

            if (string.IsNullOrWhiteSpace(newVal))
            {
                // Vymazané pole u zděditelné (nullable) vlastnosti znamená „nenastaveno" → null,
                // ne prázdný řetězec. Ten by se uložil do šablony a **přebil dědění**: prázdný font
                // u buňky by neznamenal „vezmi z tabulky", ale „žádný font", a prázdný formát
                // hodnoty by přepsal formát sloupce.
                if (ElementPropertyAccessor.AcceptsNull(target, path)) newVal = null;
                // Barva, která null nesmí být, znamená „nic nekreslit" → průhledná.
                else if (IsColorField(field)) newVal = Models.ColorHex.Transparent;
            }

            var oldVal = ElementPropertyAccessor.Get(target, path);
            commands.Add(new SetPropertyCommand(
                field.Label, oldVal, newVal,
                v => ElementPropertyAccessor.Set(target, path, (string?)v)));
        }
        return commands;
    }
}
