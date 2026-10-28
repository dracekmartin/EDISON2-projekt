using System.ComponentModel;
using System.Runtime.CompilerServices;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Controls;

/// <summary>
/// Řádek sloupce na sdílené záložce Sloupce (<see cref="ColumnsTab"/>). Drží to, co mají sloupce všech
/// tabulek společné - záhlaví, šířku, viditelnost a styl záhlaví i buněk - a schopnosti, kterými řádek
/// řekne, co u něj jde (skrýt, posunout, počítat do šířky prvku). Převod z a do modelu prvku má
/// podtřída, protože každý prvek ukládá sloupec jinou třídou (`elementy-architektura.md` §4).
/// </summary>
public abstract class ColumnRowVM : INotifyPropertyChanged, IColumnRowVM
{
    private string _header = "";
    private float? _width;
    private bool _visible = true;

    protected ColumnRowVM(CellStyleVM headerStyle, CellStyleVM dataStyle)
    {
        HeaderStyle = headerStyle;
        DataStyle = dataStyle;
        HeaderStyle.PropertyChanged += OnStyleChanged;
        DataStyle.PropertyChanged += OnStyleChanged;
    }

    /// <summary>Text záhlaví sloupce. U skupiny dnů je to přepis, prázdné = text z dat.</summary>
    public string Header { get => _header; set { _header = value; OnChanged(); OnChanged(nameof(DisplayName)); } }

    /// <summary>Šířka; null = automaticky (co přesně to znamená, říká <see cref="WidthHint"/>).</summary>
    public float? Width { get => _width; set { _width = value; OnChanged(); OnChanged(nameof(WidthText)); OnChanged(nameof(ColumnWidth)); } }

    public string WidthText
    {
        get => ColumnWidthText.Format(_width);
        set => Width = ColumnWidthText.Parse(value);
    }

    public bool Visible { get => _visible; set { _visible = value; OnChanged(); } }

    public CellStyleVM HeaderStyle { get; }
    public CellStyleVM DataStyle { get; }

    public bool HasAnyOverride => HeaderStyle.HasAnyExplicit || DataStyle.HasAnyExplicit;

    /// <summary>Název v seznamu - čitelný název sloupce, ne tištěná zkratka záhlaví.</summary>
    public virtual string DisplayName => Header;

    public float? ColumnWidth => Width;

    // ── Schopnosti řádku ─────────────────────────────────────────────────

    /// <summary>False = sloupec se tiskne vždy (hodiny, sloupce spojů); zaškrtávátko je vypnuté.</summary>
    public virtual bool CanHide => true;

    /// <summary>False = sloupec stojí pevně (hodiny vždy první, spoje za popisnými sloupci).</summary>
    public virtual bool CanMove => true;

    /// <summary>Počítá se šířka sloupce do přesahu přes šířku prvku?</summary>
    public virtual bool CountsTowardWidth => true;

    /// <summary>Jednotka šířky v popisku pole.</summary>
    public virtual string WidthUnit => "mm";

    /// <summary>Pevná šířka v mm pro součet proti šířce prvku; automatická = 0.</summary>
    public virtual float FixedWidthMm(float elementWidthMm) => Width ?? 0f;

    public string WidthLabel => $"Šířka ({WidthUnit}):";

    /// <summary>Nápověda pod šířkou; null = obecná nápověda záložky.</summary>
    public virtual string? WidthHint => null;

    /// <summary>Nápověda pod záhlavím a šířkou, specifická pro sloupec (rozvinutí podle dat…).</summary>
    public virtual string ColumnHint => "";
    public bool HasColumnHint => ColumnHint.Length > 0;

    public virtual CellStyleEditOptions HeaderEditOptions => CellStyleEditOptions.AllEnabled;

    /// <summary>Nadpis datové zóny - u sloupce, kde se nekreslí text (šipka), jiný.</summary>
    public virtual string DataZoneTitle => "Datové buňky";
    public virtual string DataZoneHint => "";
    public bool HasDataZoneHint => DataZoneHint.Length > 0;
    public virtual CellStyleEditOptions DataEditOptions => CellStyleEditOptions.AllEnabled;

    public void ResetStyle()
    {
        HeaderStyle.Reset();
        DataStyle.Reset();
    }

    // ── Formulář stylu sloupce (TableColumnEdit.cls) ─────────────────────

    /// <summary>Serverový formulář stylu sloupce - táž sada stylu jako ve formuláři prvku.</summary>
    public const string StyleFormClass = "ALVA.TemplateDesigner.EDInputForm.TableColumnEdit";

    /// <summary>Cíl formuláře: kopie explicitních hodnot stylu, do které formulář zapisuje reflexí
    /// (klíče <c>Header.*</c>, <c>Data.*</c>). Zpět do řádku se přenese přes <see cref="ApplyStyle"/>.</summary>
    public ColumnVisualStyle CreateStyleTarget() =>
        new() { Header = HeaderStyle.ToModel(), Data = DataStyle.ToModel() };

    /// <summary>Hodnoty, které sloupec bez vlastního nastavení zdědí, ve tvaru polí formuláře. Formulář
    /// jimi ukáže, jak se sloupec vytiskne, a zapíše jen to, co uživatel změní.</summary>
    public Dictionary<string, string> InheritedStyleValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in typeof(ResolvedTextStyle).GetProperties())
        {
            if (property.GetIndexParameters().Length > 0) continue;
            values["Header." + property.Name] = ElementPropertyAccessor.ToFormValue(property.GetValue(HeaderStyle.Inherited));
            values["Data." + property.Name] = ElementPropertyAccessor.ToFormValue(property.GetValue(DataStyle.Inherited));
        }
        return values;
    }

    public void ApplyStyle(ColumnVisualStyle target)
    {
        HeaderStyle.Load(target.Header);
        DataStyle.Load(target.Data);
    }

    private void OnStyleChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CellStyleVM.HasAnyExplicit))
            OnChanged(nameof(HasAnyOverride));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
