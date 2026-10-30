using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SkiaSharp;
using EDControls.EDInputForm;
using EDControls.EDTheme;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Config;
using TemplateDesigner.Serialization;
using ALWA.SYS;

namespace TemplateDesigner;

/// <summary>Okna a panel vlastností prvku, sekce a stránky.</summary>
public partial class MainWindow
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    // ── Properties dialog helpers ────────────────────────────────────────────

    /// <summary>Filtr hledání, který si pamatují okna vlastností prvku otevřená z plátna.</summary>
    /// <remarks>Uživatel ladí jednu vlastnost: vyhledá ji, změní, zavře okno kvůli náhledu a otevře
    /// znovu - bez paměti by hledal pokaždé znovu (Jan 23. 9.). Paměť je pro každý druh prvku zvlášť:
    /// do 13. 10. byla společná a hledání z tabulky spojů se objevilo i v poznámkách (Jan 13. 10.).
    /// Panel vlastností, sekce, stránka ani buňka tabulky ji nemají.</remarks>
    internal static string ElementDialogSearchKey(TemplateElement element) =>
        $"TemplateDesigner.ElementDialog.{element.GetType().Name}";

    private void OpenPropertiesForElement(TemplateElement element)
    {
        if (element is DepartureGridElement depEl)
        {
            OpenDepartureGridDialog(depEl);
            return;
        }
        if (element is StopListElement stopEl)
        {
            OpenStopColumnsDialog(stopEl);
            return;
        }
        if (element is DynamicTableElement dynTable)
        {
            OpenDynamicTableDialog(dynTable);
            return;
        }
        if (element is LineTableElement lineEl)
        {
            OpenLineTableColumnsDialog(lineEl);
            return;
        }
        // Generický EDInputForm dialog pro elementy s Caché schema (key = property path).
        // Mapping element type → (cacheClass, title). Ostatní typy fallback na legacy
        // ElementPropertiesWindow (PropertyDescriptor panel).
        if (EDInputFormSchema(element) is var (cacheClass, title) && cacheClass is not null)
        {
            OpenGenericElementEditDialog(element, cacheClass, title!);
            return;
        }

        _propsVm!.PrepareForElement(element, isNewSession: true);
        new ElementPropertiesWindow(_propsVm) { Owner = this }.ShowDialog();
        InvalidateCanvas();
        InvalidatePreview();
    }

    private void OpenDepartureGridDialog(DepartureGridElement element)
    {
        // Dostupné skupiny z preview dat
        var previewData = _vm!.State.CurrentPreviewData;
        var availableGroups = element.GetAvailableSkupinyDnu(previewData);

        // Vzory sloupců staví server z konfigurace linky - klient je nesestavuje sám, aby
        // paritní test hlídal to, co editor reálně pošle (bez dat se vzory nenabízejí).
        var presetsJson = previewData?.GetValue("PRESETY_SLOUPCU")?.ToString();

        var dlg = new DepartureGroupEditorDialog(
            element, availableGroups, element.GroupColumns, element.HourColumnStyle,
            element.CustomColumns, presetsJson, _vm.CurrentAgendaCustomColumns)
            { Owner = this, SearchMemoryKey = ElementDialogSearchKey(element) };

        if (dlg.ShowDialog() != true) return;   // Cancel - nic se neaplikovalo (Obecné batchuje na OK)

        _vm.ApplyDepartureGridEdit(element, dlg.GeneralCommand,
            dlg.ResultGroupColumns, availableGroups.Count > 0, dlg.ResultHourColumnStyle,
            dlg.ResultHourColumnHeader, dlg.ResultHourColumnWidth, dlg.ResultCustomColumns);

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    /// <summary>Mapping element type → (Caché schema class, titulek) pro generický
    /// EDInputForm editor. Vrací (null, null) pro typy bez schema (fallback na legacy).</summary>
    private static (string? cacheClass, string? title) EDInputFormSchema(TemplateElement element)
    {
        string? cacheClass = element switch
        {
            Models.Elements.TextElement        => "ALVA.TemplateDesigner.EDInputForm.TextElementEdit",
            Models.Elements.LineElement        => "ALVA.TemplateDesigner.EDInputForm.LineElementEdit",
            Models.Elements.RectangleElement   => "ALVA.TemplateDesigner.EDInputForm.RectangleElementEdit",
            Models.Elements.BarcodeElement     => "ALVA.TemplateDesigner.EDInputForm.BarcodeElementEdit",
            Models.Elements.NotesLegendElement => "ALVA.TemplateDesigner.EDInputForm.NotesLegendElementEdit",
            Models.Elements.ImageElement       => "ALVA.TemplateDesigner.EDInputForm.ImageElementEdit",
            // Pevná tabulka má jediný tab (bez sloupců a řádků) - strukturu i obsah dělá plátno.
            Models.Elements.TableElement       => "ALVA.TemplateDesigner.EDInputForm.TableElementEdit",
            // LineTable NENÍ zde - má vlastní 2-tab dialog (Obecné + Sloupce), viz OpenLineTableColumnsDialog.
            _ => null
        };
        return cacheClass is null ? (null, null) : (cacheClass, ElementNames.TitulekVlastnosti(element));
    }

    /// <summary>Caché *Edit.cls schéma pro inline panel - pokrývá i komplexní elementy
    /// (StopList/DepartureGrid/Dynamic), jejichž „Obecné" tab modálu jede na tomtéž schématu.
    /// Sloupce komplexních zůstávají jen v dvojkliku (plný 2-tab dialog).</summary>
    private static string? PanelCacheClass(TemplateElement element)
    {
        var (cc, _) = EDInputFormSchema(element);
        if (cc is not null) return cc;
        return element switch
        {
            Models.Elements.StopListElement      => "ALVA.TemplateDesigner.EDInputForm.StopListElementEdit",
            Models.Elements.DepartureGridElement => "ALVA.TemplateDesigner.EDInputForm.DepartureGridElementEdit",
            Models.Elements.DynamicTableElement  => "ALVA.TemplateDesigner.EDInputForm.DynamicTableElementEdit",
            Models.Elements.LineTableElement     => "ALVA.TemplateDesigner.EDInputForm.LineTableElementEdit",
            _ => null
        };
    }

    private void OpenGenericElementEditDialog(TemplateElement element, string cacheClass, string title)
    {
        var dlg = new ElementEditDialog(element, cacheClass, title)
        {
            Owner = this, PlaceholderFields = _vm!.PlaceholderFieldItems(), SearchMemoryKey = ElementDialogSearchKey(element),
            InheritedValues = ElementFormBinder.InheritedValuesFor(element),
        };
        if (element is NotesLegendElement poznamky)
        {
            bool linkovy = _vm.JeLinkovyJr, rozdilovy = _vm.JeRozdilovyJr;
            dlg.ViditelnostPole = (klic, _) => NotesLegendElement.PoleFormulareViditelne(klic, linkovy, rozdilovy);
            dlg.UpravaPoli = pole => NastavVolbyRazeni(pole, linkovy);
            // Okno vlastností zapisuje až s Uložit - rozpracované druhy si proto drží samo,
            // aby druhé otevření ukázalo první úpravu.
            var rozpracovane = poznamky.EfektivniSkupiny();
            dlg.AkceTlacitek = new Dictionary<string, Func<IDesignerCommand?>>
            {
                [TlacitkaFormulare.DruhyPoznamek] = () =>
                {
                    if (VyberSkupinyPoznamek(poznamky, rozpracovane, dlg) is not { } skupiny) return null;
                    rozpracovane = skupiny;
                    return TemplateDesignerViewModel.SkupinyPoznamekCommand(poznamky, skupiny);
                },
            };
        }
        if (dlg.ShowDialog() == true && dlg.ResultCommand is not null)
        {
            _vm!.Execute(dlg.ResultCommand);
        }
        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    private void OpenStopColumnsDialog(StopListElement element)
    {
        var dlg = new StopColumnsEditorDialog(element, NabidkaSloupcuZastavek(element))
        {
            Owner = this, SearchMemoryKey = ElementDialogSearchKey(element), UpravaPoli = DoplneniPoliRadku(element),
            ViditelnostPole = ViditelnostSeznamuZastavek(element),
        };

        if (dlg.ShowDialog() == true)
        {
            _vm!.ApplyStopColumnsEdit(element, dlg.GeneralCommand, dlg.ResultColumns,
                dlg.HighlightEdited ? dlg.ResultHighlightRules ?? [] : null);
        }

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    /// <summary>Sloupce Seznamu zastávek, ke kterým agenda šablony posílá data (`B194d` E5): sloupek jen
    /// se sloupkem ve schématu zastávek, varianty tras jen s příznakem v registru agend.</summary>
    private Func<string, bool> NabidkaSloupcuZastavek(StopListElement prvek)
    {
        bool sloupek = _vm!.AgendaPosilaPoleKolekce(prvek.ZastavkyDataSource, StopFields.Sloupek);
        bool varianty = _vm.CurrentAgendaVariantyTras;
        return klic => StopListElement.SloupecNabizet(klic, sloupek, varianty);
    }

    /// <summary>Pole formuláře Seznamu zastávek podle dat agendy - „Výběr stanoviště" jen se stanovišti
    /// podle spojů (`B194d` E5).</summary>
    private Func<string, IReadOnlyDictionary<string, string>, bool> ViditelnostSeznamuZastavek(StopListElement prvek)
    {
        bool dleSpoju = _vm!.AgendaPosilaPoleKolekce(prvek.ZastavkyDataSource, StopFields.NastupisteSpoju);
        return (klic, _) => StopListElement.PoleFormulareViditelne(klic, dleSpoju);
    }

    /// <summary>EDInputForm button v inline panelu - otevře editor pravidel zvýraznění.</summary>
    private void OnInlineElementButtonClicked(string key)
    {
        // Z formuláře buňky zpátky na nastavení celé tabulky (= odchod z buňky, jako Esc).
        if (key == TlacitkaFormulare.NastaveniCeleTabulky) { ExitToTableSettings(); return; }

        if (key == TlacitkaFormulare.DruhyPoznamek)
        {
            if (_vm!.State.SelectedElements is [NotesLegendElement poznamky]
                && VyberSkupinyPoznamek(poznamky, poznamky.EfektivniSkupiny()) is { } skupiny)
            {
                _vm.Execute(TemplateDesignerViewModel.SkupinyPoznamekCommand(poznamky, skupiny));
                InvalidateCanvas();
                InvalidatePreview();
            }
            return;
        }

        if (key != TlacitkaFormulare.ZvyrazneniRadku) return;
        if (_vm!.State.SelectedElements is [IRowHighlightable h])
            OpenHighlightRulesDialog(h);
    }

    /// <summary>Okno „Druhy poznámek"; null = Storno.</summary>
    private List<NastaveniSkupinyPoznamek>? VyberSkupinyPoznamek(NotesLegendElement poznamky,
        IReadOnlyList<NastaveniSkupinyPoznamek> vychozi, Window? owner = null)
    {
        var dlg = new SkupinyPoznamekDialog(vychozi, poznamky.Columns) { Owner = owner ?? this };
        return dlg.ShowDialog() == true ? dlg.Vysledek : null;
    }

    private void OpenHighlightRulesDialog(IRowHighlightable element)
    {
        var dlg = new HighlightRulesEditorDialog(element.HighlightRules) { Owner = this };

        if (dlg.ShowDialog() == true)
            _vm!.ApplyHighlightRulesEdit(element, dlg.ResultRules);

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    private void OpenLineTableColumnsDialog(LineTableElement element)
    {
        bool rozdilovy = _vm!.JeRozdilovyJr;
        var dlg = new LineTableColumnsEditorDialog(element)
        {
            Owner = this, SearchMemoryKey = ElementDialogSearchKey(element),
            ViditelnostPole = (klic, _) => LineTableElement.PoleFormulareViditelne(klic, rozdilovy),
            UpravaPoli = DoplneniPoliRadku(element),
        };

        if (dlg.ShowDialog() == true)
        {
            _vm!.ApplyLineTableColumnsEdit(element, dlg.GeneralCommand, dlg.ResultColumns,
                dlg.HighlightEdited ? dlg.ResultHighlightRules ?? [] : null);
        }

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    private void OpenDynamicTableDialog(DynamicTableElement element)
    {
        var collections = _vm!.GetAvailableGroups();
        var dlg = new DynamicTableEditorDialog(element, collections) { Owner = this, SearchMemoryKey = ElementDialogSearchKey(element) };

        if (dlg.ShowDialog() == true)
        {
            _vm!.ApplyDynamicTableEdit(element, dlg.GeneralCommand, dlg.ResultColumns,
                dlg.HighlightEdited ? dlg.ResultHighlightRules ?? [] : null);
        }

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    /// <summary>
    /// Pole formuláře sekce, která v dané kombinaci nic nedělají, se schovají (rozhodnutí Jan 24. 9.):
    /// barva a tloušťka bez rámečku, Stránky jinde než u hlavičky a patičky (obsah se tiskne jednou,
    /// pod obsahem vždy na všech stránkách), Přetečení a nový list u opakované sekce (ta se nedělí
    /// a opakuje se na každém listu). Do `B18` pravidla drží návrhář, formulář z Caché viditelnost neumí.
    /// </summary>
    internal static bool ViditelnostPoleSekce(string klic, IReadOnlyDictionary<string, string> hodnoty)
    {
        string typ = hodnoty.GetValueOrDefault(nameof(Section.Typ)) ?? "";
        bool obsah = typ is "" or nameof(TypSekce.Obsah);
        return klic switch
        {
            nameof(Section.BorderColor) or nameof(Section.BorderWidth)
                => hodnoty.GetValueOrDefault(nameof(Section.ShowBorder)) == "1",
            nameof(Section.Stranky) => typ is nameof(TypSekce.HlavickaStranky) or nameof(TypSekce.PatickaStranky),
            nameof(Section.OverflowBehavior) or nameof(Section.ZacitNaNoveStrance) => obsah,
            _ => true,
        };
    }

    /// <summary>Nápověda viditelnosti řádku s poli zastávky z agendy šablony; null = prvek podmínku řádku nemá.</summary>
    private Action<IEnumerable<Item>?>? DoplneniPoliRadku(TemplateElement prvek)
    {
        string? kolekce = prvek switch
        {
            LineTableElement t => t.ZastavkyDataSource,
            StopListElement s  => s.ZastavkyDataSource,
            _                  => null,
        };
        if (kolekce is null) return null;
        var schema = _vm!.SchemaProvider.GetSchema(_vm.State.CurrentTemplate.AgendaCode ?? "");
        return pole => ElementFormBinder.DoplnPoleRadku(pole, schema, kolekce);
    }

    /// <summary>Nabídka „Řazení poznámek" podle agendy šablony (<see cref="NotesLegendElement.VolbyRazeni"/>).
    /// Nastavuje se vždy celá, protože panel drží týž formulář i pro prvek z jiné šablony.</summary>
    internal static void NastavVolbyRazeni(IEnumerable<Item>? pole, bool linkovyJr)
    {
        if ((pole ?? []).OfType<Field>().FirstOrDefault(f => f.Key == nameof(NotesLegendElement.Razeni)) is not { } razeni)
            return;
        razeni.ListItems = [.. NotesLegendElement.VolbyRazeni(linkovyJr, razeni.Value ?? "")
            .Select(v => new EDControls.ListItem(v.Hodnota, v.Popis))];
    }

    /// <summary>Klik na hlášku vpravo dole vybere její prvek na plátně (`B178`).</summary>
    private void VyberPrvekZHlaseni(Guid id)
    {
        if (_vm is null) return;
        var sekce = _vm.State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Any(e => e.Id == id));
        var prvek = sekce?.Elements.First(e => e.Id == id);
        if (sekce is null || prvek is null) return;
        _vm.State.SelectedElements.Clear();
        _vm.State.SelectedElements.Add(prvek);
        _vm.ActivateSection(sekce);
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private void OpenPropertiesForSection(Section section)
    {
        var dlg = new ElementEditDialog(
            section,
            "ALVA.TemplateDesigner.EDInputForm.SectionEdit",
            "Vlastnosti sekce: " + section.Name)
            {
                Owner = this, PlaceholderFields = _vm!.PlaceholderFieldItems(),
                ViditelnostPole = ViditelnostPoleSekce,
            };
        if (dlg.ShowDialog() == true && dlg.ResultCommand is not null)
        {
            _vm!.Execute(dlg.ResultCommand);
            _vm!.SrazVyskuSekceDoMezi(section);
        }
        InvalidateCanvas();
        InvalidatePreview();
        _vm!.RebuildSections();
    }

    private void OpenPropertiesForMultipleElements(List<TemplateElement> elements)
    {
        _propsVm!.PrepareForMultipleElements(elements);
        new ElementPropertiesWindow(_propsVm) { Owner = this }.ShowDialog();
        InvalidateCanvas();
        InvalidatePreview();
    }

    // Property-path klíče ve FinalValues (= ElementFormBinder.Snapshot cesta) - MUSÍ odpovídat
    // property cestě na Template.PageSize. Sdílená konstanta, ať detekce změny rozměru neselže tiše
    // při přejmenování pole formuláře.
    private const string PagePathWidth  = "PageSize.Width";
    private const string PagePathHeight = "PageSize.Height";

    private async Task OpenPropertiesForPage(Template template)
    {
        try
        {
            float oldW = template.PageSize.Width, oldH = template.PageSize.Height;

            var dlg = new ElementEditDialog(
                template,
                "ALVA.TemplateDesigner.EDInputForm.PageEdit",
                "Vlastnosti stránky") { Owner = this };
            if (dlg.ShowDialog() != true) return;

            // Změna velikosti / orientace stránky přepočítá obsah na novou velikost (B104) - dřív
            // šablonu vymazala. Ostatní změny (okraje, kultura, font) jdou jako běžná vratná změna.
            var fv = dlg.FinalValues;

            // Pojistka: dialog vrátil hodnoty, ale klíče rozměrů chybí → detekce resetu by tiše selhala.
            if (fv is not null && (!fv.ContainsKey(PagePathWidth) || !fv.ContainsKey(PagePathHeight)))
                System.Diagnostics.Debug.WriteLine(
                    $"[OpenPropertiesForPage] FinalValues bez klíče rozměrů ({PagePathWidth}/{PagePathHeight}) - reset detekce vypnutá?");

            bool dimsChanged = fv is not null
                && (DiffMm(oldW, ParseMm(fv, PagePathWidth, oldW))
                    || DiffMm(oldH, ParseMm(fv, PagePathHeight, oldH)));

            if (dimsChanged)
            {
                if (!await _vm!.ChangePageFormatAsync(fv!)) return;
                UpdateCanvasSize();
                InvalidateCanvas();
                InvalidatePreview();
                return;
            }

            if (dlg.ResultCommand is not null) _vm!.Execute(dlg.ResultCommand);
            UpdateCanvasSize();
            InvalidateCanvas();
            InvalidatePreview();
        }
        catch (Exception ex)
        {
            _vm?.PopupDisplayer.Error(ex);   // R2: chyby přes IPopupDisplayer, ne pád async void
        }
    }

    private static float ParseMm(IReadOnlyDictionary<string, string> fields, string key, float fallback) =>
        fields.TryGetValue(key, out var s)
        && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v : fallback;

    private static bool DiffMm(float a, float b) => Math.Abs(a - b) > 0.1f;

    private void UpdatePropertiesPanel()
    {
        if (_vm is null) return;

        RefreshTreeIfVisible();
        _vm.UpdateStatusForSelection();
        RefreshInlineProperties();
    }

    private void RefreshInlineProperties()
    {
        if (_propsVm is null || InlinePropertiesPanel is null) return;

        Action<IDesignerCommand> execute  = cmd => _vm!.Execute(cmd);
        Action                   invalidate = () => { skCanvas.InvalidateVisual(); InvalidatePreview(); };
        InlinePropertiesPanel.SetContext(execute, invalidate);
        InlineElementForm.SetContext(execute, invalidate);
        FormatToolbar.SetContext(execute, invalidate);

        // Uvnitř tabulky patří panel i lišta **vybrané buňce**, ne prvku - nastavení celé tabulky
        // je za tlačítkem v panelu (a přes Esc). Jinak by proti sobě stály dvě sady týchž
        // vlastností s jiným dosahem.
        if (InTableCells && CellFormatting is not null) { SyncFormatToolbar(); return; }

        var selected = _vm!.State.SelectedElements;

        // Jeden element se schématem → EDInputForm panel. Descriptory se pořád počítají
        // pro FormatToolbar. Více / žádný / bez schématu → starý PropertyDescriptor panel.
        string? panelSchema = selected.Count == 1 ? PanelCacheClass(selected[0]) : null;

        if (selected.Count >= 1)
        {
            var descriptors = selected.Count == 1
                ? _propsVm.PrepareForElement(selected[0], isNewSession: false)
                : _propsVm.PrepareForMultipleElements(selected);
            FormatToolbar.ShowDescriptors(descriptors);

            if (panelSchema is not null)
            {
                // Nabídka „vložit pole" - seznam zná jen návrhář (ze schématu agendy), Caché ne.
                InlineElementForm.PlaceholderFields = _vm.PlaceholderFieldItems();
                bool linkovy = _vm.JeLinkovyJr;
                bool poznamky = selected[0] is NotesLegendElement;
                bool rozdilovy = _vm.JeRozdilovyJr;
                InlineElementForm.ViditelnostPole = selected[0] switch
                {
                    NotesLegendElement => (klic, _) => NotesLegendElement.PoleFormulareViditelne(klic, linkovy, rozdilovy),
                    LineTableElement   => (klic, _) => LineTableElement.PoleFormulareViditelne(klic, rozdilovy),
                    StopListElement s  => ViditelnostSeznamuZastavek(s),
                    _                  => null,
                };
                InlineElementForm.UpravaPoli = poznamky ? pole => NastavVolbyRazeni(pole, linkovy) : DoplneniPoliRadku(selected[0]);
                // Vždy znovu: hodnoty buňky pevné tabulky tu jinak zůstaly a panel je ukázal u dalšího prvku.
                InlineElementForm.InheritedValues = ElementFormBinder.InheritedValuesFor(selected[0]);
                try
                {
                    InlineElementForm.ShowElement(selected[0], panelSchema);
                    ShowInlineFormPanel(true);
                }
                catch (Exception ex)
                {
                    // Formulář se načítá ze serveru; při výpadku spojení zůstane panel u jednoduchého
                    // seznamu vlastností, ať jde s prvkem pracovat dál (PL_POVED 12. 10.).
                    InlinePropertiesPanel.ShowDescriptors(descriptors);
                    ShowInlineFormPanel(false);
                    _vm.PopupDisplayer.Error(ex);
                }
            }
            else
            {
                InlinePropertiesPanel.ShowDescriptors(descriptors);
                ShowInlineFormPanel(false);
            }

            // Přepnout na záložku Vlastnosti pokud jsme na Sekce nebo Strom.
            // Netaháme uživatele pryč z tabů, kde aktivně pracuje s něčím jiným
            // (Datová pole = drag polí, Průvodce = chat, Parametry = nastavení před tiskem).
            // ⚠️ **Parametry sem přibyly až 25. 8.** a chyběly tu od jejich zavedení: změna
            // parametru, která sáhne do šablony (rozvržení sloupců), vyvolá obnovu panelu
            // vlastností - a uživateli to pod rukama odskočilo ze záložky, ve které zrovna
            // přepínal volbu (nahlásil Jan 24. 8.).
            if (LeftTabControl.SelectedItem != TabFields
                && LeftTabControl.SelectedItem != TabAiGuide
                && LeftTabControl.SelectedItem != TabParams)
                LeftTabControl.SelectedItem = TabProperties;
        }
        else
        {
            InlinePropertiesPanel.ShowMessage("Vyberte prvek na plátně");
            FormatToolbar.ShowDescriptors(null);
            ShowInlineFormPanel(false);
        }
    }

    /// <summary>Přepne viditelnost mezi EDInputForm panelem (jeden element) a starým PropertyDescriptor
    /// panelem (0 / více / bez schématu).</summary>
    private void ShowInlineFormPanel(bool useForm)
    {
        InlineElementForm.Visibility     = useForm ? Visibility.Visible : Visibility.Collapsed;
        InlinePropertiesPanel.Visibility = useForm ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateTitle()
    {
        if (_vm is null) return;
        string agenda = !string.IsNullOrEmpty(_vm.State.CurrentTemplate.AgendaCode)
            ? $"[{_vm.CurrentAgendaDisplayName}] " : "";
        // Indikátor původu: kam míří Uložit. [DB] = uložená v databázi (Ctrl+S aktualizuje),
        // [neuloženo] = nová/importovaná, ještě není v DB (Ctrl+S ji založí).
        string origin = _vm.IsCacheBacked ? "[DB] " : "[neuloženo] ";
        string name = _vm.State.CurrentTemplate.Name;
        string dirty = _vm.IsDirty ? " *" : "";
        Title = $"{agenda}{origin}{name}{dirty} - Návrhář sestav";
    }

    /// <summary>
    /// Kontextová paleta JŘ elementů dle agendy šablony: zastávkové agendy ukazují
    /// StopList/DepartureGrid/NotesLegend, knižní LineTable; agenda bez JŘ kontextu (GENERAL,
    /// fakturace) žádné - JŘ elementy patří jen ke své agendě.
    /// </summary>
    private void UpdatePaletteForAgenda()
    {
        if (_vm is null) return;
        var kind = _vm.CurrentAgendaElementKind;
        static Visibility Vis(bool b) => b ? Visibility.Visible : Visibility.Collapsed;
        BtnPalStopList.Visibility      = Vis(AgendaCapabilities.Supports(kind, ElementTypes.StopList));
        BtnPalDepartureGrid.Visibility = Vis(AgendaCapabilities.Supports(kind, ElementTypes.DepartureGrid));
        BtnPalNotesLegend.Visibility   = Vis(AgendaCapabilities.Supports(kind, ElementTypes.NotesLegend));
        BtnPalLineTable.Visibility     = Vis(AgendaCapabilities.Supports(kind, ElementTypes.LineTable));
        PalScheduleSep.Visibility      = Vis(AgendaCapabilities.ScheduleElementTypes(kind).Count > 0);
    }

    private void ShowHelp()
    {
        var helpDialog = new HelpDialog { Owner = this };
        helpDialog.ShowDialog();
    }

    private void OpenNormalizePositionsDialog()
    {
        if (_vm is null) return;
        var elements = _vm.State.SelectedElements.Count > 0
            ? (IReadOnlyList<TemplateElement>)_vm.State.SelectedElements
            : _vm.State.CurrentTemplate.Sections.SelectMany(s => s.Elements).ToList();
        if (elements.Count == 0) return;

        var vm  = new NormalizePositionsDialogViewModel(elements);
        var dlg = new NormalizePositionsDialog(vm) { Owner = this };
        if (dlg.ShowDialog() == true)
            _vm.NormalizePositions(elements, vm.Step);
    }
}
