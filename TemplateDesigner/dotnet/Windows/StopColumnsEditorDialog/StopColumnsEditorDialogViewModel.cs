using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

// ─── ViewModel pro řádek sloupce ────────────────────────────────────────────

public class StopColumnVM : Controls.ColumnRowVM
{
    private StopColumnVM(StopColumn c, CellStyleVM headerStyle, CellStyleVM dataStyle) : base(headerStyle, dataStyle)
    {
        Key       = c.Key;
        Alignment = c.Alignment;
        Header    = c.Header;
        Width     = c.Width;
        Visible   = c.Visible;
    }

    public string Key { get; }

    /// <summary>Záložní zarovnání sloupce (použije se, když ho nenastaví styl sloupce ani prvku).
    /// Okno ho nenabízí, jen ho vrací beze změny.</summary>
    public TextHorizontalAlignment Alignment { get; }

    // Srozumitelný název dle Key (Header zůstává tištěná zkratka).
    public override string DisplayName => FriendlyName(Key, Header);

    /// <summary>Čitelný název sloupce do editoru - odvozený z field ID, ať uživatel nemusí luštit zkratku záhlaví.</summary>
    private static string FriendlyName(string key, string header) => key switch
    {
        StopFields.TravelTime         => "Čas jízdy",
        StopFields.TravelTimeVariants => "Varianty tras",
        StopFields.StopName           => "Název zastávky",
        StopFields.Zone               => "Zóny",
        StopFields.Poradi             => "Pořadí",
        StopFields.JeAktualni         => "Je aktuální zastávka",
        _                             => string.IsNullOrWhiteSpace(header) ? key : header,
    };

    /// <summary>Jedna položka seznamu, ze které při tisku vznikne víc sloupců podle dat.
    /// Uživatel to musí vidět dřív, než se diví, proč mu jedno zaškrtnutí udělalo dvanáct sloupců.</summary>
    public bool ExpandsByData => Key == StopFields.TravelTimeVariants;

    // `B150`: záhlaví rozvíjené položky je od 11. 9. editovatelné (dřív zamčené) - je to společný
    // popisek celého bloku pro případ, že jsou písmena variant vypnutá.
    public override string ColumnHint => ExpandsByData
        ? "Rozvine se podle dat: linka dostane tolik sloupců, kolik má variant jízdních dob, a každý "
          + "bude takto široký (i když má varianta jednu). Záhlaví nesou značky variant; zadané "
          + "záhlaví se použije jako jeden společný popisek přes celý blok, když v parametrech sestavy "
          + "vypnete Písmena variant tras. Vypněte si k tomu sloupec Čas jízdy - je to týž údaj a "
          + "zapnutý vedle by se opakoval. Součet šířek vlevo počítá jeden."
        : "";

    public StopColumn ToModel() => new()
    {
        Key       = Key,
        Header    = Header,
        Width     = Width,
        Visible   = Visible,
        Alignment = Alignment,
        Style     = BuildStyle()
    };

    private ColumnVisualStyle? BuildStyle()
    {
        var style = new ColumnVisualStyle { Header = HeaderStyle.ToModel(), Data = DataStyle.ToModel() };
        return style.IsEmpty ? null : style;
    }

    /// <summary>Zděděná vrstva počítá se záložním zarovnáním sloupce stejně jako tisk
    /// (<see cref="TableLayout.ResolveCascade"/>) - jinak by šedá nápověda ukazovala jiné zarovnání,
    /// než se vytiskne.</summary>
    public static StopColumnVM FromModel(StopColumn c, StopListElement element) => new(c,
        CellStyleVM.FromModel(c.Style?.Header, TableLayout.ResolveCascade(null, element.ResolveColumnHeaderParent(),
            c.Alignment, elementDefinesHAlign: element.HeaderStyle.HAlign is not null)),
        CellStyleVM.FromModel(c.Style?.Data, TableLayout.ResolveCascade(null, element.ResolveColumnDataParent(),
            c.Alignment, elementDefinesHAlign: element.DataStyle.HAlign is not null)));
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class StopColumnsEditorDialogViewModel : EDViewModel
{
    // Tab 1 „Obecné" = embedded EDInputForm (StopListElementEdit.cls) - řeší code-behind
    // přes ElementFormBinder. VM drží jen záložku Sloupce (sdílená ColumnsTab).

    public Controls.ColumnsTabViewModel Columns { get; }

    /// <summary>Výsledné sloupce po OK - čteny volajícím.</summary>
    public List<StopColumn> ResultColumns { get; private set; } = [];

    /// <summary>Vypnuté sloupce, ke kterým agenda nemá data - nenabízejí se, ale ze šablony nezmizí.</summary>
    private readonly List<StopColumn> _nenabizene = [];

    /// <param name="nabizetSloupec">Které sloupce nabídnout (podle dat agendy); null = všechny.
    /// Zapnutý sloupec se nabídne vždy, aby šel vypnout.</param>
    public StopColumnsEditorDialogViewModel(
        StopListElement element,
        List<StopColumn> columns,
        Func<string, bool>? nabizetSloupec = null)
    {
        var rows = new List<StopColumnVM>();
        foreach (var col in columns)
        {
            if (!col.Visible && nabizetSloupec?.Invoke(col.Key) == false) { _nenabizene.Add(col); continue; }
            rows.Add(StopColumnVM.FromModel(col, element));
        }

        // Doplň chybějící známé sloupce (uložená šablona může mít jen podmnožinu) - ať uživatel
        // vidí VŠECHNY dostupné sloupce a může je zapnout checkboxem, místo aby zmizely z nabídky
        // a daly se vrátit jen přes "Výchozí". Doplněné jsou skryté (nezmění render, dokud je nezapne).
        var presentKeys = columns.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var def in StopListElement.GetDefaultColumns())
            if (!presentKeys.Contains(def.Key) && nabizetSloupec?.Invoke(def.Key) != false)
            {
                def.Visible = false;
                rows.Add(StopColumnVM.FromModel(def, element));
            }

        // Seznam zastávek se při přesahu zmenší (TableLayout - nejdřív pevné šířky, pak zmenšení).
        Columns = new Controls.ColumnsTabViewModel(rows)
        {
            ElementWidth = () => element.Width,
            CanFit = true,
            MultilineHeader = true,
            UsesStyleForm = true,
        };
    }

    protected override void OnOk()
    {
        // Element-level styl + ostatní general props řeší embedded EDInputForm (code-behind).
        ResultColumns = [.. Columns.Rows.Cast<StopColumnVM>().Select(vm => vm.ToModel()), .. _nenabizene];
        CloseDialog(true);
    }
}
