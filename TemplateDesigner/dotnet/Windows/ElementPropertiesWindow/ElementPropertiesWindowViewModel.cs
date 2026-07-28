using EDControls.EDViewModel;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

public class ElementPropertiesWindowViewModel : EDViewModel
{
    private readonly DesignerState _state;
    private readonly Action        _invalidate;
    private readonly Action        _refreshSections;
    private readonly Action        _updateCanvasSize;
    private readonly Action        _refreshFields;

    private readonly Func<IReadOnlyList<DataField>?>?           _getAvailableFields;
    private readonly Func<IReadOnlyList<DataFieldCollection>?>? _getAvailableGroups;

    private int    _undoDepthAtOpen;
    private Action _currentInvalidate;

    // ── Exponováno do code-behind ─────────────────────────────────────────

    /// <summary>Aktuálně zobrazovaný element - pro detekci nové session v ShowForElement.</summary>
    public TemplateElement? CurrentElement { get; private set; }

    /// <summary>Invalidace odpovídající aktuálnímu kontextu (element / sekce / stránka).</summary>
    public Action CurrentInvalidate => _currentInvalidate;

    /// <summary>Předávané do PropertiesPanel.SetContext jako execute akce.</summary>
    public Action<IDesignerCommand> ExecuteCommand => cmd => _state.Execute(cmd);

    /// <summary>Bindovatelný titulek okna.</summary>
    private string _windowTitle = "Vlastnosti";
    public string WindowTitle { get => _windowTitle; set => Set(ref _windowTitle, value); }

    /// <summary>
    /// Callback: VM říká View "zobraz tyto deskriptory" - stejný pattern jako RequestCanvasRefresh
    /// v MainWindow. Používá se uvnitř EnhanceTableEditor při interní změně panelu.
    /// </summary>
    public Action<IReadOnlyList<Models.PropertyDescriptor>>? RequestShowDescriptors { get; set; }

    /// <summary>Deskriptory připravené posledním voláním PrepareFor* - Window je přečte v konstruktoru.</summary>
    public IReadOnlyList<Models.PropertyDescriptor>? CurrentDescriptors { get; private set; }

    /// <summary>
    /// Callback pro otevření DepartureGroupEditorDialog z inline panelu.
    /// Sdílí logiku s MainWindow.OpenDepartureGridDialog - jedna cesta, žádná duplikace.
    /// </summary>
    public Action<DepartureGridElement>? RequestOpenDepartureGridDialog { get; set; }

    /// <summary>
    /// Callback pro otevření StopColumnsEditorDialog z inline panelu.
    /// Sdílí logiku s MainWindow.OpenStopColumnsDialog - jedna cesta, žádná duplikace.
    /// </summary>
    public Action<StopListElement>? RequestOpenStopColumnsDialog { get; set; }

    /// <summary>
    /// Callback pro otevření DynamicTableEditorDialog z inline panelu i modálního dialogu.
    /// Sdílí logiku s MainWindow.OpenDynamicTableDialog - jedna cesta, žádná duplikace.
    /// </summary>
    public Action<DynamicTableElement>? RequestOpenDynamicTableDialog { get; set; }

    /// <summary>
    /// Callback pro otevření TableEditorDialog (statická tabulka) - otevření + apply řeší View
    /// (MainWindow.OpenTableEditorDialog), stejně jako ostatní tabulkové editory. VM nezná WPF.
    /// </summary>
    public Action<TableElement>? RequestOpenTableEditorDialog { get; set; }

    /// <summary>Callback pro výběr souboru (title, filter → cesta / null). View drží OpenFileDialog.</summary>
    public Func<string, string, string?>? RequestPickFile { get; set; }

    /// <summary>Zruší změny a zavře dialog (DialogResult = false).</summary>
    public RelayCommand CancelCommand { get; }

    // ── Konstruktor ───────────────────────────────────────────────────────

    public ElementPropertiesWindowViewModel(
        DesignerState state,
        Action invalidate,
        Action refreshSections,
        Action updateCanvasSize,
        Action refreshFields,
        Func<IReadOnlyList<DataField>?>? getAvailableFields = null,
        Func<IReadOnlyList<DataFieldCollection>?>? getAvailableGroups = null)
    {
        _state                 = state;
        _invalidate            = invalidate;
        _refreshSections       = refreshSections;
        _updateCanvasSize      = updateCanvasSize;
        _refreshFields         = refreshFields;
        _getAvailableFields    = getAvailableFields;
        _getAvailableGroups    = getAvailableGroups;
        _currentInvalidate     = invalidate;

        // OkCommand je zděděn z EDViewModel - base.OnOk() volá CloseDialog(true), což je správně
        CancelCommand = Command(() => { CancelChanges(); CloseDialog(false); });
    }

    // ── Příprava deskriptorů ──────────────────────────────────────────────

    /// <summary>
    /// Připraví deskriptory pro daný element, nastaví titulek a undo hloubku.
    /// isNewSession=true při prvním zobrazení nebo přepnutí na jiný element.
    /// </summary>
    public IReadOnlyList<Models.PropertyDescriptor> PrepareForElement(TemplateElement element, bool isNewSession)
    {
        if (isNewSession)
            _undoDepthAtOpen = _state.UndoStack.Count;

        CurrentElement     = element;
        _currentInvalidate = _invalidate;
        WindowTitle        = "Vlastnosti: " + element.GetType().Name.Replace("Element", "");

        var descriptors = element.GetPropertyDescriptors();

        if (element is TextElement)
        {
            var fields = _getAvailableFields?.Invoke();
            if (fields is { Count: > 0 })
                descriptors = EnhanceWithFieldPicker(descriptors, fields);
        }

        if (element is TableElement tableElement)
            descriptors = EnhanceTableEditor(descriptors, tableElement);

        if (element is DynamicTableElement dynamicTable)
            descriptors = EnhanceDynamicTableEditor(descriptors, dynamicTable);

        if (element is StopListElement stopListElement)
            descriptors = EnhanceStopListEditor(descriptors, stopListElement);

        if (element is ImageElement imageElement)
            descriptors = EnhanceImageEditor(descriptors, imageElement);

        if (element is DepartureGridElement departureGridElement)
            descriptors = EnhanceDepartureGroupEditor(descriptors, departureGridElement);

        CurrentDescriptors = descriptors;
        return descriptors;
    }

    /// <summary>Připraví deskriptory pro sekci, nastaví titulek a undo hloubku.</summary>
    public IReadOnlyList<Models.PropertyDescriptor> PrepareForSection(Section section)
    {
        _undoDepthAtOpen   = _state.UndoStack.Count;
        CurrentElement     = null;
        _currentInvalidate = () => { _updateCanvasSize(); _invalidate(); _refreshSections(); };
        WindowTitle        = "Vlastnosti sekce: " + section.Name;

        return CurrentDescriptors = BuildSectionDescriptors(section);
    }

    /// <summary>Připraví deskriptory pro stránku, nastaví titulek a undo hloubku.</summary>
    public IReadOnlyList<Models.PropertyDescriptor> PrepareForPage(Template template)
    {
        _undoDepthAtOpen   = _state.UndoStack.Count;
        CurrentElement     = null;
        _currentInvalidate = () => { _invalidate(); _updateCanvasSize(); _refreshFields(); };
        WindowTitle        = "Vlastnosti stránky";

        return CurrentDescriptors = BuildPageDescriptors(template);
    }

    /// <summary>
    /// Připraví deskriptory společných vlastností pro více vybraných elementů.
    /// Změna hodnoty se aplikuje na všechny elementy najednou (CompositeCommand).
    /// </summary>
    public IReadOnlyList<Models.PropertyDescriptor> PrepareForMultipleElements(List<TemplateElement> elements)
    {
        if (elements.Count == 0) return CurrentDescriptors = [];

        _undoDepthAtOpen   = _state.UndoStack.Count;
        CurrentElement     = null;
        _currentInvalidate = _invalidate;
        WindowTitle        = $"Společné vlastnosti ({elements.Count} elementů)";

        return CurrentDescriptors = BuildMultiElementDescriptors(elements);
    }

    /// <summary>Vrátí veškeré změny provedené od posledního otevření a překreslí.</summary>
    public void CancelChanges()
    {
        while (_state.UndoStack.Count > _undoDepthAtOpen)
            _state.Undo();
        _currentInvalidate();
    }

    // ── Privátní buildery ─────────────────────────────────────────────────

    private IReadOnlyList<Models.PropertyDescriptor> EnhanceTableEditor(
        IReadOnlyList<Models.PropertyDescriptor> descriptors,
        TableElement element)
    {
        var collections = _getAvailableGroups?.Invoke() ?? [];

        var list = new List<Models.PropertyDescriptor>(descriptors.Count);
        foreach (var d in descriptors)
        {
            if (d is Models.EnumPropertyDescriptor enumD && enumD.Label == "Režim")
            {
                // Obalit Set - po změně módu obnovit panel (Zdroj dat se zobrazí/skryje)
                list.Add(new Models.EnumPropertyDescriptor
                {
                    Label   = enumD.Label,
                    Group   = enumD.Group,
                    Get     = enumD.Get,
                    Set     = v =>
                    {
                        enumD.Set(v);
                        RequestShowDescriptors?.Invoke(
                            EnhanceTableEditor(element.GetPropertyDescriptors(), element));
                    },
                    Options = enumD.Options
                });
            }
            else if (d is Models.StringPropertyDescriptor strD && strD.Label == "Zdroj dat" && collections.Count > 0)
            {
                list.Add(new Models.SuggestStringPropertyDescriptor
                {
                    Label          = strD.Label,
                    Group          = strD.Group,
                    Get            = strD.Get,
                    Set            = strD.Set,
                    GetSuggestions = () => collections.Select(c => (c.CollectionKey, c.Name))
                });
            }
            else if (d is Models.ButtonPropertyDescriptor btn && btn.ActionId == Models.ButtonPropertyDescriptor.ActionOpenTableEditor)
            {
                list.Add(new Models.ButtonPropertyDescriptor
                {
                    Label       = btn.Label,
                    Group       = btn.Group,
                    ButtonLabel = btn.ButtonLabel,
                    OnClick     = () => RequestOpenTableEditorDialog?.Invoke(element)
                });
            }
            else
            {
                list.Add(d);
            }
        }
        return list;
    }

    private IReadOnlyList<Models.PropertyDescriptor> EnhanceDynamicTableEditor(
        IReadOnlyList<Models.PropertyDescriptor> descriptors,
        DynamicTableElement element)
    {
        var collections = _getAvailableGroups?.Invoke() ?? [];

        var list = new List<Models.PropertyDescriptor>(descriptors.Count);
        foreach (var d in descriptors)
        {
            if (d is Models.StringPropertyDescriptor strD && strD.Label == "Zdroj dat" && collections.Count > 0)
            {
                // Panel jen nabídne dropdown zdrojů + nastaví ho. Generování sloupců z polí kolekce
                // řeší editor sloupců (DynamicTableEditorDialog) - jedno místo, undoable přes příkaz.
                list.Add(new Models.SuggestStringPropertyDescriptor
                {
                    Label          = strD.Label,
                    Group          = strD.Group,
                    Get            = strD.Get,
                    Set            = strD.Set,
                    GetSuggestions = () => collections.Select(c => (c.CollectionKey, c.Name))
                });
            }
            else if (d is Models.ButtonPropertyDescriptor btn && btn.ActionId == Models.ButtonPropertyDescriptor.ActionOpenTableEditor)
            {
                list.Add(new Models.ButtonPropertyDescriptor
                {
                    Label       = btn.Label,
                    Group       = btn.Group,
                    ButtonLabel = btn.ButtonLabel,
                    OnClick     = () => RequestOpenDynamicTableDialog?.Invoke(element)
                });
            }
            else
            {
                list.Add(d);
            }
        }
        return list;
    }

    private IReadOnlyList<Models.PropertyDescriptor> EnhanceStopListEditor(
        IReadOnlyList<Models.PropertyDescriptor> descriptors,
        StopListElement element)
    {
        var list = new List<Models.PropertyDescriptor>(descriptors.Count);
        foreach (var d in descriptors)
        {
            if (d is Models.ButtonPropertyDescriptor btn && btn.ActionId == Models.ButtonPropertyDescriptor.ActionOpenStopColumnsEditor)
            {
                list.Add(new Models.ButtonPropertyDescriptor
                {
                    Label       = btn.Label,
                    Group       = btn.Group,
                    ButtonLabel = btn.ButtonLabel,
                    OnClick     = () => RequestOpenStopColumnsDialog?.Invoke(element)
                });
            }
            else
            {
                list.Add(d);
            }
        }
        return list;
    }

    private IReadOnlyList<Models.PropertyDescriptor> EnhanceImageEditor(
        IReadOnlyList<Models.PropertyDescriptor> descriptors,
        ImageElement element)
    {
        var list = new List<Models.PropertyDescriptor>(descriptors.Count);
        foreach (var d in descriptors)
        {
            if (d is Models.ButtonPropertyDescriptor btn && btn.ActionId == Models.ButtonPropertyDescriptor.ActionUploadImage)
            {
                list.Add(new Models.ButtonPropertyDescriptor
                {
                    Label       = btn.Label,
                    Group       = btn.Group,
                    ButtonLabel = btn.ButtonLabel,
                    OnClick     = () =>
                    {
                        var path = RequestPickFile?.Invoke(
                            "Nahrát obrázek",
                            "Obrázky|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Všechny soubory|*.*");
                        if (path is null) return;

                        byte[] data;
                        try { data = System.IO.File.ReadAllBytes(path); }
                        catch (Exception ex)
                        {
                            PopupDisplayer.Error(ex);
                            return;
                        }

                        string? base64;
                        if (data.Length > ImageElement.MaxImageBytes)
                        {
                            if (!PopupDisplayer.Confirm(
                                $"Obrázek má {data.Length / 1024 / 1024} MB (limit {ImageElement.MaxImageBytes / 1024 / 1024} MB). Zmenšit automaticky?"))
                                return;
                            base64 = ImageElement.EncodeAndResize(data);
                            if (base64 is null)
                            {
                                PopupDisplayer.Info("Obrázek se nepodařilo dostatečně zmenšit.");
                                return;
                            }
                        }
                        else
                        {
                            base64 = Convert.ToBase64String(data);
                        }

                        _state.Execute(new Commands.Implementations.SetPropertyCommand(
                            "Obrázek", element.ImageBase64, base64,
                            v => element.ImageBase64 = (string?)v));
                        _currentInvalidate();
                    }
                });
            }
            else if (d is Models.ButtonPropertyDescriptor btn2 && btn2.ActionId == Models.ButtonPropertyDescriptor.ActionMatchImageAspect)
            {
                list.Add(new Models.ButtonPropertyDescriptor
                {
                    Label       = btn2.Label,
                    Group       = btn2.Group,
                    ButtonLabel = btn2.ButtonLabel,
                    OnClick     = () =>
                    {
                        float? aspect = element.GetImageAspectRatio();
                        if (aspect is null)
                        {
                            PopupDisplayer.Info("Nejdřív nahrajte obrázek.");
                            return;
                        }
                        // Šířku zachováme, výšku přepočítáme. Composite command pro undo/redo jedním krokem.
                        float newHeight = element.Width / aspect.Value;
                        float oldHeight = element.Height ?? element.EffectiveDesignerHeight;
                        if (MathF.Abs(newHeight - oldHeight) < 0.01f) return;
                        _state.Execute(new Commands.Implementations.SetPropertyCommand(
                            "Přizpůsobit poměru obrázku",
                            oldHeight, newHeight,
                            v => element.Height = (float)v!));
                        _currentInvalidate();
                    }
                });
            }
            else
            {
                list.Add(d);
            }
        }
        return list;
    }

    private IReadOnlyList<Models.PropertyDescriptor> EnhanceDepartureGroupEditor(
        IReadOnlyList<Models.PropertyDescriptor> descriptors,
        DepartureGridElement element)
    {
        var list = new List<Models.PropertyDescriptor>(descriptors.Count);
        foreach (var d in descriptors)
        {
            if (d is Models.ButtonPropertyDescriptor btn && btn.ActionId == Models.ButtonPropertyDescriptor.ActionOpenDepartureGridEditor)
            {
                list.Add(new Models.ButtonPropertyDescriptor
                {
                    Label       = btn.Label,
                    Group       = btn.Group,
                    ButtonLabel = btn.ButtonLabel,
                    OnClick     = () => RequestOpenDepartureGridDialog?.Invoke(element)
                });
            }
            else
            {
                list.Add(d);
            }
        }
        return list;
    }

    private static IReadOnlyList<Models.PropertyDescriptor> EnhanceWithFieldPicker(
        IReadOnlyList<Models.PropertyDescriptor> descriptors,
        IReadOnlyList<DataField> fields)
    {
        var list = new List<Models.PropertyDescriptor>(descriptors.Count);
        foreach (var d in descriptors)
        {
            if (d is StringPropertyDescriptor sd && sd.Label == "Text" && sd.MultiLine)
            {
                list.Add(new TextWithPickerPropertyDescriptor
                {
                    Label           = sd.Label,
                    Group           = sd.Group,
                    Get             = sd.Get,
                    Set             = sd.Set,
                    MultiLine       = sd.MultiLine,
                    AvailableFields = fields
                });
            }
            else
            {
                list.Add(d);
            }
        }
        return list;
    }

    // Předdefinované formáty stránek (kratší strana × delší strana)
    private static readonly (string Label, float Short, float Long)[] PageFormats =
    [
        ("A2 (420 × 594 mm)", 420f, 594f),
        ("A3 (297 × 420 mm)", 297f, 420f),
        ("A4 (210 × 297 mm)", 210f, 297f),
        ("A5 (148 × 210 mm)", 148f, 210f),
    ];

    private static string DetectPageFormat(float w, float h)
    {
        float shorter = Math.Min(w, h);
        float longer  = Math.Max(w, h);
        foreach (var (label, s, l) in PageFormats)
            if (Math.Abs(shorter - s) < 0.5f && Math.Abs(longer - l) < 0.5f)
                return label;
        return "Vlastní";
    }

    private static string DetectOrientation(float w, float h) =>
        w <= h ? "Na výšku" : "Na šířku";

    /// <summary>
    /// Při změně rozměrů stránky proporcionálně upraví DesignerHeight
    /// obsahových sekcí (PrintBehavior = Once), aby se vešly na novou stránku.
    /// Hlavičky, patičky a ostatní speciální sekce zůstávají beze změny.
    /// </summary>
    private static void ScaleBodySections(Template template, float oldPrintableHeight)
    {
        float newPrintableHeight = template.PageSize.PrintableHeight;
        if (newPrintableHeight <= 0 || oldPrintableHeight <= 0) return;
        if (Math.Abs(newPrintableHeight - oldPrintableHeight) < 0.1f) return;

        // Spočítat kolik zabírají fixní sekce (hlavičky, patičky, ...)
        float fixedHeight = 0f;
        float bodyHeight = 0f;
        foreach (var s in template.Sections)
        {
            if (s.DesignerHeight is null) continue; // dynamické sekce nešálujeme
            if (s.PrintBehavior == SectionPrintBehavior.Once)
                bodyHeight += s.DesignerHeight.Value;
            else
                fixedHeight += s.DesignerHeight.Value;
        }

        if (bodyHeight <= 0) return;

        float availableForBody = newPrintableHeight - fixedHeight;
        if (availableForBody <= 0) return;

        float scale = availableForBody / bodyHeight;
        // Nezvětšovat nad rozumnou mez (max 2×), jen zmenšovat nebo mírně zvětšovat
        scale = Math.Clamp(scale, 0.1f, 2f);

        foreach (var s in template.Sections)
        {
            if (s.DesignerHeight is null) continue;
            if (s.PrintBehavior != SectionPrintBehavior.Once) continue;
            s.DesignerHeight = MmMath.Round(s.DesignerHeight.Value * scale);
        }
    }

    private IReadOnlyList<Models.PropertyDescriptor> BuildPageDescriptors(Template template)
    {
        var ps = template.PageSize;
        return
        [
            new StringPropertyDescriptor
            {
                Label = "Kód agendy", Group = "Šablona",
                Get   = () => template.AgendaCode ?? "",
                Set   = v  => template.AgendaCode = string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant()
            },
            new EnumPropertyDescriptor
            {
                Label = "Formát čísel a datumů", Group = "Šablona",
                Get   = () => (object)(template.CultureCode ?? "cs-CZ"),
                Set   = v  => template.CultureCode = (string)v switch { "cs-CZ" => null, var s => s },
                Options =
                [
                    ("Čeština (cs-CZ)",     (object)"cs-CZ"),
                    ("Slovenština (sk-SK)", (object)"sk-SK"),
                    ("Angličtina (en-US)",  (object)"en-US"),
                    ("Němčina (de-DE)",     (object)"de-DE"),
                ]
            },
            new EnumPropertyDescriptor
            {
                Label = "Formát stránky", Group = "Rozměry stránky",
                Get   = () => (object)DetectPageFormat(ps.Width, ps.Height),
                Set   = v  =>
                {
                    var label = (string)v;
                    if (label == "Vlastní") return;
                    float oldPH = ps.PrintableHeight;
                    foreach (var (fl, s, l) in PageFormats)
                    {
                        if (fl != label) continue;
                        bool landscape = ps.Width > ps.Height;
                        ps.Width  = landscape ? l : s;
                        ps.Height = landscape ? s : l;
                        break;
                    }
                    ScaleBodySections(template, oldPH);
                },
                Options =
                [
                    ("A2 (420 × 594 mm)", (object)"A2 (420 × 594 mm)"),
                    ("A3 (297 × 420 mm)", (object)"A3 (297 × 420 mm)"),
                    ("A4 (210 × 297 mm)", (object)"A4 (210 × 297 mm)"),
                    ("A5 (148 × 210 mm)", (object)"A5 (148 × 210 mm)"),
                    ("Vlastní",           (object)"Vlastní"),
                ]
            },
            new EnumPropertyDescriptor
            {
                Label = "Orientace", Group = "Rozměry stránky",
                Get   = () => (object)DetectOrientation(ps.Width, ps.Height),
                Set   = v  =>
                {
                    var orient = (string)v;
                    bool wantLandscape = orient == "Na šířku";
                    bool isLandscape   = ps.Width > ps.Height;
                    if (wantLandscape != isLandscape)
                    {
                        float oldPH = ps.PrintableHeight;
                        (ps.Width, ps.Height) = (ps.Height, ps.Width);
                        ScaleBodySections(template, oldPH);
                    }
                },
                Options =
                [
                    ("Na výšku",  (object)"Na výšku"),
                    ("Na šířku",  (object)"Na šířku"),
                ]
            },
            new FloatPropertyDescriptor
            {
                Label = "Šířka",  Group = "Rozměry stránky",
                Get   = () => ps.Width,
                Set   = v  => ps.Width = v,
                Min   = 50f, Max = 1200f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Výška",  Group = "Rozměry stránky",
                Get   = () => ps.Height,
                Set   = v  => ps.Height = v,
                Min   = 50f, Max = 1200f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Nahoře",  Group = "Okraje",
                Get   = () => ps.MarginTop,
                Set   = v  => ps.MarginTop = v,
                Min   = 0f, Max = 100f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Dole",    Group = "Okraje",
                Get   = () => ps.MarginBottom,
                Set   = v  => ps.MarginBottom = v,
                Min   = 0f, Max = 100f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Vlevo",   Group = "Okraje",
                Get   = () => ps.MarginLeft,
                Set   = v  => ps.MarginLeft = v,
                Min   = 0f, Max = 100f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Vpravo",  Group = "Okraje",
                Get   = () => ps.MarginRight,
                Set   = v  => ps.MarginRight = v,
                Min   = 0f, Max = 100f, Unit = "mm"
            },
        ];
    }

    /// <summary>Maximální výška sekce v mm tak, aby nepřekryla ostatní sekce na stránce.</summary>
    private float CalculateMaxSectionHeight(Section section)
    {
        var template = _state.CurrentTemplate;
        float printableH = template.PageSize.PrintableHeight;

        // Odečíst výšku všech ostatních sekcí (+ prostor pro záhlaví sekce v designeru ~5mm per sekce)
        const float sectionHeaderMm = 5f; // přibližný ekvivalent 20px záhlaví v mm
        float usedByOthers = 0f;
        foreach (var s in template.Sections)
        {
            if (s == section) continue;
            usedByOthers += sectionHeaderMm + s.EffectiveDesignerHeight;
        }
        // Záhlaví této sekce
        usedByOthers += sectionHeaderMm;

        return Math.Max(5f, printableH - usedByOthers);
    }

    private IReadOnlyList<Models.PropertyDescriptor> BuildSectionDescriptors(Section section)
    {
        var list = new List<Models.PropertyDescriptor>
        {
            new StringPropertyDescriptor
            {
                Label = "Název", Group = "Sekce",
                Get   = () => section.Name,
                Set   = v  => section.Name = v
            }
        };

        if (section.DesignerHeight.HasValue)
        {
            list.Add(new FloatPropertyDescriptor
            {
                Label = "Výška", Group = "Sekce",
                Get   = () => section.DesignerHeight ?? 30f,
                Set   = v  =>
                {
                    // Clamp: výška nesmí překrýt ostatní sekce
                    float maxH = CalculateMaxSectionHeight(section);
                    section.DesignerHeight = Math.Min(v, maxH);
                },
                Min   = 5f, Max = 9999f, Unit = "mm"
            });
        }

        list.Add(new EnumPropertyDescriptor
        {
            Label   = "Tisk",    Group = "Sekce",
            Get     = () => section.PrintBehavior,
            Set     = v  => section.PrintBehavior = (SectionPrintBehavior)v,
            Options =
            [
                ("Jednou",          (object)SectionPrintBehavior.Once),
                ("Každá stránka",   (object)SectionPrintBehavior.OnEveryPage),
                ("Jen první",       (object)SectionPrintBehavior.OnFirstPageOnly),
                ("Jen poslední",    (object)SectionPrintBehavior.OnLastPageOnly),
                ("Kromě první",     (object)SectionPrintBehavior.OnEveryPageExceptFirst),
            ]
        });

        list.Add(new EnumPropertyDescriptor
        {
            Label   = "Umístění", Group = "Sekce",
            Get     = () => section.Role,
            Set     = v  => section.Role = (SectionRole)v,
            Options =
            [
                ("Obsah",   (object)SectionRole.Body),
                ("Hlavička", (object)SectionRole.Header),
                ("Patička",  (object)SectionRole.Footer),
            ]
        });

        list.Add(new EnumPropertyDescriptor
        {
            Label   = "Přetečení", Group = "Sekce",
            Get     = () => section.OverflowBehavior,
            Set     = v  => section.OverflowBehavior = (SectionOverflowBehavior)v,
            Options =
            [
                ("Nepřerušovat",  (object)SectionOverflowBehavior.KeepTogether),
                ("Přes stránky",  (object)SectionOverflowBehavior.SplitAcrossPages),
                ("Povolit rozdělení", (object)SectionOverflowBehavior.AllowSplit),
            ]
        });

        list.Add(new BoolPropertyDescriptor
        {
            Label = "Rámeček", Group = "Vzhled",
            Get   = () => section.ShowBorder,
            Set   = v  => section.ShowBorder = v
        });

        list.Add(new ColorPropertyDescriptor
        {
            Label = "Barva rámečku", Group = "Vzhled",
            Get   = () => section.BorderColor,
            Set   = v  => section.BorderColor = v
        });

        list.Add(new FloatPropertyDescriptor
        {
            Label = "Tloušťka rámečku", Group = "Vzhled",
            Get   = () => section.BorderWidth,
            Set   = v  => section.BorderWidth = v,
            Min   = 0.1f, Max = 5f, Unit = "mm"
        });

        list.Add(new StringPropertyDescriptor
        {
            Label = "Podmínka viditelnosti", Group = "Podmínky",
            Get   = () => section.VisibilityExpression ?? "",
            Set   = v  => section.VisibilityExpression = string.IsNullOrWhiteSpace(v) ? null : v.Trim(),
            Validate = Models.Expressions.ExpressionEvaluator.Validate
        });

        if (section.PrintBehavior == SectionPrintBehavior.PerDataRow)
        {
            list.Add(new StringPropertyDescriptor
            {
                Label = "Datový zdroj", Group = "Tabulka",
                Get   = () => section.DataSource ?? "",
                Set   = v  => section.DataSource = v
            });
            list.Add(new FloatPropertyDescriptor
            {
                Label = "Výška řádku", Group = "Tabulka",
                Get   = () => section.RowHeight ?? 8f,
                Set   = v  => section.RowHeight = v,
                Min   = 3f, Max = 100f, Unit = "mm"
            });
        }

        return list;
    }

    // ── Multi-element společné vlastnosti ───────────────────────────────

    private IReadOnlyList<Models.PropertyDescriptor> BuildMultiElementDescriptors(List<TemplateElement> elements)
    {
        var first = elements[0];

        // Helper: vytvoří setter, který změní property na všech elementech
        // Čte aktuální hodnotu z prvního elementu, nastaví na všechny
        return
        [
            new ColorPropertyDescriptor
            {
                Label = "Barva rámečku", Group = "Rámeček",
                Get = () => first.BorderColor,
                Set = v => { foreach (var el in elements) el.BorderColor = v; }
            },
            new FloatPropertyDescriptor
            {
                Label = "Tloušťka rámečku", Group = "Rámeček",
                Get = () => first.BorderWidth,
                Set = v => { foreach (var el in elements) el.BorderWidth = v; },
                Min = 0, Max = 5, Unit = "mm", Format = "0.0#"
            },
            new FloatPropertyDescriptor
            {
                Label = "ZIndex", Group = "Pozice",
                Get = () => first.ZIndex,
                Set = v => { foreach (var el in elements) el.ZIndex = (int)Math.Round(v); },
                Min = 0, Max = 100, Format = "0"
            },
            new BoolPropertyDescriptor
            {
                Label = "Uzamčeno", Group = "Pozice",
                Get = () => first.IsLocked,
                Set = v => { foreach (var el in elements) el.IsLocked = v; }
            },
        ];
    }
}
