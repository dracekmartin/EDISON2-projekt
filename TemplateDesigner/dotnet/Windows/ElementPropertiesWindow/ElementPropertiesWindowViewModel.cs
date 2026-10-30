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
    /// v MainWindow. Používá se při interní změně panelu (např. přepnutí, které mění sadu polí).
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

    /// <summary>Callback pro výběr souboru (title, filter → cesta / null). View drží OpenFileDialog.</summary>
    public Func<string, string, string?>? RequestPickFile { get; set; }

    /// <summary>Zruší změny a zavře dialog (DialogResult = false).</summary>
    public RelayCommand CancelCommand { get; }

    // ── Konstruktor ───────────────────────────────────────────────────────

    public ElementPropertiesWindowViewModel(
        DesignerState state,
        Action invalidate,
        Func<IReadOnlyList<DataField>?>? getAvailableFields = null,
        Func<IReadOnlyList<DataFieldCollection>?>? getAvailableGroups = null)
    {
        _state                 = state;
        _invalidate            = invalidate;
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
        WindowTitle        = ElementNames.TitulekVlastnosti(element);

        var descriptors = element.GetPropertyDescriptors();

        if (element is TextElement)
        {
            var fields = _getAvailableFields?.Invoke();
            if (fields is { Count: > 0 })
                descriptors = EnhanceWithFieldPicker(descriptors, fields);
        }

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
        WindowTitle        = $"Společné vlastnosti ({elements.Count} {TemplateTreeNames.Prvku(elements.Count)})";

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
            if (d is StringPropertyDescriptor sd && sd.Label == TextElement.ObsahLabel && sd.MultiLine)
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

    // ── Multi-element společné vlastnosti ───────────────────────────────

    /// <summary>Okno ukazuje hodnotu prvního prvku a zápis jde do všech.</summary>
    private static IReadOnlyList<Models.PropertyDescriptor> BuildMultiElementDescriptors(List<TemplateElement> elements)
    {
        var poPrvcich = elements.Select(e => e.MultiEditDescriptors()).ToList();
        return [.. Enumerable.Range(0, poPrvcich[0].Count).Select(i => ProVsechnyPrvky([.. poPrvcich.Select(d => d[i])]))];
    }

    /// <summary>Jedna vlastnost pro všechny vybrané prvky. Zápis je jeden vratný krok složený z kroku
    /// každého prvku, takže Ctrl+Z vrátí každému jeho původní hodnotu - do 30. 10. dostaly všechny
    /// hodnotu prvního prvku a rámečky nebo vrstvy, které se lišily, zůstaly srovnané.</summary>
    private static Models.PropertyDescriptor ProVsechnyPrvky(IReadOnlyList<Models.PropertyDescriptor> stejne)
    {
        var prvni = stejne[0];

        IDesignerCommand Zapis(object? hodnota)
        {
            var prikaz = new CompositeCommand($"Změna: {prvni.Label}",
                stejne.Select(d => new SetPropertyCommand(d.Label, Hodnota(d), hodnota, v => Nastav(d, v))));
            prikaz.Execute();
            return prikaz;
        }

        void NastavVsem(object? hodnota)
        {
            foreach (var d in stejne) Nastav(d, hodnota);
        }

        return prvni switch
        {
            FloatPropertyDescriptor f => new FloatPropertyDescriptor
            {
                Label = f.Label, Group = f.Group, Min = f.Min, Max = f.Max, Unit = f.Unit, Format = f.Format,
                Get = f.Get, Set = v => NastavVsem(v), MakeCommand = Zapis
            },
            BoolPropertyDescriptor b => new BoolPropertyDescriptor
            {
                Label = b.Label, Group = b.Group, Get = b.Get, Set = v => NastavVsem(v), MakeCommand = Zapis
            },
            ColorPropertyDescriptor c => new ColorPropertyDescriptor
            {
                Label = c.Label, Group = c.Group, Get = c.Get, Set = v => NastavVsem(v), MakeCommand = Zapis
            },
            _ => throw new NotSupportedException($"Vlastnost „{prvni.Label}\" okno společných vlastností neumí."),
        };
    }

    private static object? Hodnota(Models.PropertyDescriptor d) => d switch
    {
        FloatPropertyDescriptor f => f.Get(),
        BoolPropertyDescriptor b  => b.Get(),
        ColorPropertyDescriptor c => c.Get(),
        _ => throw new NotSupportedException(d.Label),
    };

    private static void Nastav(Models.PropertyDescriptor d, object? hodnota)
    {
        switch (d)
        {
            case FloatPropertyDescriptor f: f.Set((float)hodnota!); break;
            case BoolPropertyDescriptor b:  b.Set((bool)hodnota!);  break;
            case ColorPropertyDescriptor c: c.Set((string)hodnota!); break;
            default: throw new NotSupportedException(d.Label);
        }
    }
}
