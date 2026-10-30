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

/// <summary>Lišta, menu a tlačítka (tenké obsluhy, práci dělá ViewModel), nabídky přidání sekce a prvku.</summary>
public partial class MainWindow
{
    // ─────────────────────────────────────────────────────────────────────────
    // Toolbar handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void CmbZoom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        int idx = CmbZoom.SelectedIndex;
        if (idx >= 0 && idx < ZoomLevels.Length)
        {
            _vm.State.Zoom = ZoomLevels[idx];
            UpdateCanvasSize();
        }
    }

    private void CmbGridSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || CmbGridSize.SelectedItem is not ComboBoxItem item) return;
        if (float.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float size))
            _vm.GridSize = size;
    }

    /// <summary>Najde ComboBoxItem s odpovídajícím Tagem a aktivuje ho. Volá se po načtení šablony.</summary>
    private void SyncGridSizeComboBox(float gridSizeMm)
    {
        foreach (var item in CmbGridSize.Items.OfType<ComboBoxItem>())
        {
            if (float.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)
                && Math.Abs(v - gridSizeMm) < 0.001f)
            {
                CmbGridSize.SelectedItem = item;
                return;
            }
        }
    }

    /// <summary>Sync CmbSnapGap na uloženou hodnotu mezery - nebindovaný combobox (jako CmbGridSize).
    /// Bez toho combo po startu/loadu ukazoval default, i když State.SnapGapMm držel uloženou hodnotu.</summary>
    private void SyncSnapGapComboBox(float gapMm)
    {
        foreach (var item in CmbSnapGap.Items.OfType<ComboBoxItem>())
        {
            if (float.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)
                && Math.Abs(v - gapMm) < 0.001f)
            {
                CmbSnapGap.SelectedItem = item;
                return;
            }
        }
    }

    private void CmbSnapGap_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || CmbSnapGap.SelectedItem is not ComboBoxItem item) return;
        if (float.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float gap))
            _vm.SnapGapMm = gap;
    }

    private void MenuScale_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not MenuItem item || item.Tag is not string tagStr) return;
        if (float.TryParse(tagStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float scale))
        {
            _vm.ScaleTemplate(scale);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Menu / button click handlers (thin - delegate to ViewModel)
    // ─────────────────────────────────────────────────────────────────────────

    private void MenuNew_Click(object sender, RoutedEventArgs e)       => _vm!.NewCommand.Execute(null);
    private void MenuOpen_Click(object sender, RoutedEventArgs e)      => _vm!.OpenCommand.Execute(null);
    private void MenuSave_Click(object sender, RoutedEventArgs e)      => _vm!.SaveCommand.Execute(null);
    private void MenuSaveAs_Click(object sender, RoutedEventArgs e)    => _vm!.SaveAsCommand.Execute(null);

    // Re-entrance guard - modální ShowDialog otevírá vnořený dispatcher, který může
    // doručit dříve queueované Ctrl+P. Bez guardu by druhé pressnutí během už-otevřeného
    // dialogu vyvolalo nový PrintDialog skrz Owner-set-on-closed crash.
    private bool _printDialogOpening;

    private void MenuPrint_Click(object sender, RoutedEventArgs e)
    {
        if (_printDialogOpening || _vm is null) return;
        _printDialogOpening = true;
        try { OpenPrintDialog(); }
        finally { _printDialogOpening = false; }
    }

    /// <summary>
    /// Kontrola všech vybraných linek z návrháře (`B178`). Okno s výsledkem se otevře hned a ukazuje
    /// průběh; klik na hlášku přepne na její linku a vybere prvek - to z tiskového okna nejde.
    /// </summary>
    private async void BtnKontrola_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var nacitani = _vm!.NacitaniVsechLinek(out var bezDat);
        if (nacitani is null || _vm.ActiveInstances.Count == 0)
        {
            _vm.StatusText = "Kontrola potřebuje vybrané linky - vyberte je na štítku vedle agendy.";
            return;
        }

        var vysledek = new KontrolaVysledkuDialogViewModel("Načítám data…", []) { Probiha = true };
        vysledek.RequestVybrat = PrejdiNaHlaseni;
        new KontrolaVysledkuDialog(vysledek) { Owner = this }.Show();
        try
        {
            var parametry = new Dictionary<string, object?>(_vm.PreviewParamValues);
            var prubeh = new Progress<(int Hotovo, int Celkem, string Linka)>(p =>
            {
                vysledek.Nadpis = $"Načítám data {p.Hotovo + 1} z {p.Celkem} · {p.Linka}";
                vysledek.Prubeh = p.Celkem > 0 ? 0.5 * p.Hotovo / p.Celkem : 0;
            });
            var data = await Task.Run(() => nacitani(parametry, prubeh));
            if (data is null or { Count: 0 })
            {
                // Linky bez dat řeknou proč (`B193g`); bez nich jde nejspíš o výpadek databáze.
                vysledek.Hlaseni = TemplateDesigner.Models.KontrolaVystupu.SBezDat(bezDat, []);
                vysledek.Nadpis = bezDat.Count > 0 ? "Žádná vybraná linka nemá data" : "Databáze nevrátila žádná data.";
                return;
            }
            var template = _vm.State.CurrentTemplate;
            var hlaseni = TemplateDesigner.Models.KontrolaVystupu.SBezDat(bezDat,
                await TemplateDesigner.Models.KontrolaVystupu.ZkontrolujAsync(template, data,
                d => TemplateDesigner.Models.KontrolaVystupu.ZLinky(template, d, _previewCanvas!.HlaseniSazby),
                (hotovo, celkem, linka) =>
                {
                    vysledek.Nadpis = hotovo < celkem ? $"Kontroluji {hotovo + 1} z {celkem} · {linka}" : "";
                    vysledek.Prubeh = celkem > 0 ? 0.5 + 0.5 * hotovo / celkem : 1;
                }));
            int linek = TemplateDesigner.Models.KontrolaVystupu.PoLinkach(data).Count + bezDat.Count;
            vysledek.Hlaseni = hlaseni;
            vysledek.Nadpis = hlaseni.Count > 0
                ? $"Zkontrolováno {TemplateDesigner.Models.KontrolaVystupu.Linek(linek)}: {TemplateDesigner.Models.KontrolaVystupu.SouhrnPoctu(hlaseni)}"
                : $"Zkontrolováno {TemplateDesigner.Models.KontrolaVystupu.Linek(linek)} - bez hlášek";
        }
        catch (Exception ex)
        {
            vysledek.Nadpis = $"Kontrola selhala: {ex.Message}";
        }
        finally
        {
            vysledek.Probiha = false;
        }
    }

    /// <summary>Klik na hlášku v okně kontroly: přepnout na její linku a vybrat prvek.</summary>
    private void PrejdiNaHlaseni(Hlaseni h)
    {
        if (_vm is null) return;
        if (h.Linka is { } linka)
        {
            int i = _vm.ActiveInstances.ToList().FindIndex(x => x.Display == linka);
            if (i >= 0 && i != _vm.ActiveInstanceIndex) _vm.ActiveInstanceIndex = i;
        }
        if (h.PrvekId is { } id) VyberPrvekZHlaseni(id);
        Activate();
    }

    private void OpenPrintDialog()
    {
        // PrintDialog operuje nad sdíleným dict s preview lištou (_vm.PreviewParamValues).
        // Saved PrintParams se aplikují při openu šablony (LoadPrintParamsAndApplyAsync),
        // takže _previewParams už obsahuje persistované hodnoty - žádný extra I/O zde.

        var buildRealData = _vm!.NacitaniVsechLinek(out var vynechaneLinky);

        var tplAgendaCode = _vm!.State.CurrentTemplate.AgendaCode;
        var agendaClass = string.IsNullOrEmpty(tplAgendaCode) ? null : _vm.CacheProvider?.GetCacheClass(tplAgendaCode);

        var vm = new PrintDialogViewModel(
            _vm.State.CurrentTemplate,
            _vm.AgendaParameters,
            _vm.PreviewParamValues,
            paramValues => _vm.BuildPreviewMockData(paramValues),
            buildRealData,
            _preferences,
            onParamsChanged: _vm.OnExternalParamsChanged,
            agendaClass: agendaClass,
            maVybraneLinky: () => _vm.ActiveInstances.Count > 0,
            elementKind: _vm.CurrentAgendaElementKind);
        vm.VynechaneLinky = () => vynechaneLinky;

        vm.RequestRenderPng = (filePath, dataPages) =>
            _previewCanvas!.RenderMultiDataToPng(filePath, dataPages,
                template: Serialization.TemplateSerializer.DeepClone(_vm!.State.CurrentTemplate));

        vm.RequestRenderPdf = (filePath, data, sheets, meritko) =>
            _previewCanvas!.RenderToPdf(filePath, data, sheets, meritko);

        // Render nad snapshotem šablony - VM volá callback z threadpoolu (async tisk),
        // živá šablona by závodila s preview renderem UI threadu (element cache).
        vm.RequestRenderMultiPdf = (filePath, dataPages, sheets, meritko) =>
            _previewCanvas!.RenderMultiDataToPdf(filePath, dataPages,
                template: Serialization.TemplateSerializer.DeepClone(_vm!.State.CurrentTemplate),
                sheetLayout: sheets, meritko: meritko);

        vm.RequestOverflowCheck = dataPages =>
            _previewCanvas!.CountOverflowingPages(dataPages);
        vm.RequestKontrolaLinky = data => TemplateDesigner.Models.KontrolaVystupu.ZLinky(
            _vm.State.CurrentTemplate, data, _previewCanvas!.HlaseniSazby);

        var dlg = new PrintDialog(vm)
            { Owner = this, PlaceholderFields = _vm!.PlaceholderFieldItems() };
        dlg.ShowDialog();

        // Tisk sdílí _previewParams s tabem parametrů - po zavření přenačti tab formulář,
        // aby odrážel hodnoty případně změněné v dialogu.
        ReloadParamsTabForm();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Add section / element menus
    // ─────────────────────────────────────────────────────────────────────────

    private void BtnAddPage_Click(object sender, RoutedEventArgs e) => _vm?.AddPage();

    private void BtnAddSection_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var menu = new ContextMenu();

        void Add(string label, Func<Section> factory)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => _vm.AddSection(factory());
            menu.Items.Add(item);
        }

        void Sep() => menu.Items.Add(new Separator());

        Add("Hlavička stránky", () => new Section
        {
            Name = "Hlavička stránky", PrintBehavior = SectionPrintBehavior.OnEveryPage,
            Role = SectionRole.Header,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 25f
        });
        Add("Patička stránky", () => new Section
        {
            Name = "Patička stránky", PrintBehavior = SectionPrintBehavior.OnEveryPage,
            Role = SectionRole.Footer,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 15f
        });
        Add("Oddělovač", () => new Section
        {
            Name = "Oddělovač", PrintBehavior = SectionPrintBehavior.Once, DesignerHeight = 5f
        });
        Sep();
        Add("Hlavička sestavy (jen 1. str.)", () => new Section
        {
            Name = "Hlavička sestavy", PrintBehavior = SectionPrintBehavior.OnFirstPageOnly,
            Role = SectionRole.Header,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 30f
        });
        Add("Patička sestavy (jen posl. str.)", () => new Section
        {
            Name = "Patička sestavy", PrintBehavior = SectionPrintBehavior.OnLastPageOnly,
            Role = SectionRole.Footer,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 20f
        });
        Add("Hlavička kromě 1. str.", () => new Section
        {
            Name = "Hlavička kromě 1. str.", PrintBehavior = SectionPrintBehavior.OnEveryPageExceptFirst,
            Role = SectionRole.Header,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 25f
        });
        Sep();

        var customTemplates = _vm.GetCustomSectionTemplateNames();
        if (customTemplates.Count > 0)
        {
            foreach (var templateName in customTemplates)
            {
                var name = templateName;
                Add($"💾 {templateName}", () =>
                {
                    var t = _vm.LoadCustomSectionTemplate(name);
                    if (t is null)
                    {
                        _vm.PopupDisplayer.Info($"Šablonu „{name}“ se nepodařilo načíst.");
                        return new Section { Name = "Chyba" };
                    }
                    return t;
                });
            }
            Sep();
        }

        Add("⚙ Prázdná sekce", () => new Section
        {
            Name = $"Sekce {_vm.State.CurrentTemplate.Sections.Count + 1}",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 30f
        });

        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private void BtnAddElement_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (_vm.State.ActiveSection is null)
        {
            _vm.PopupDisplayer.Info("Nejprve vyberte sekci kliknutím v levém panelu nebo na plátně.");
            return;
        }

        var menu = new ContextMenu();
        void Add(string label, string hint, Func<TemplateElement?> factory)
        {
            var item = new MenuItem { Header = label, ToolTip = hint };
            item.Click += (_, _) =>
            {
                // Null = uživatel vkládání zrušil (rozměr nové tabulky).
                if (factory() is not { } element) return;
                _vm.Execute(new AddElementCommand(_vm.State.ActiveSection!, element));
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.Add(element);
                UpdatePropertiesPanel();
                InvalidateCanvas();
            };
            menu.Items.Add(item);
        }

        // Prvky z katalogu v jeho pořadí: obecné vždy, prvky JŘ podle druhu agendy (jako paleta).
        var kind = _vm.CurrentAgendaElementKind;
        bool oddelovac = false;
        foreach (var prvek in ElementCatalog.All)
        {
            if (!prvek.IsGeneric && !AgendaCapabilities.Supports(kind, prvek.Type)) continue;
            if (!prvek.IsGeneric && !oddelovac) { menu.Items.Add(new Separator()); oddelovac = true; }
            var (x, y) = prvek.MenuPosition;
            Add(prvek.Name, prvek.Hint, () => _vm.CreateElementInteractive(prvek.Type, x, y));
        }

        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }
}
