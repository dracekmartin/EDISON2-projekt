using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

/// <summary>
/// ViewModel pro jednu TextStyle zónu (záhlaví NEBO data) v dialozích sloupců.
///
/// Všechny editovatelné vlastnosti jsou nullable (null = dědit z rodiče v kaskádě).
/// <see cref="Inherited"/> drží vyřešený rodičovský styl - UI ho zobrazuje jako placeholder/default
/// u každé vlastnosti, aby uživatel viděl, co bude skutečně platit, když nic nepřepíše.
///
/// Sdíleno všemi dialogy sloupců (DepartureGrid / StopList / TableElement).
/// </summary>
public class CellStyleVM : INotifyPropertyChanged
{
    // ── Editovatelné vlastnosti (null = dědit) ────────────────────────────

    private string?                    _fontFamily;
    private float?                     _fontSize;
    private int?                       _fontWeight;
    private bool?                      _bold;
    private bool?                      _italic;
    private bool?                      _underline;
    private bool?                      _strikethrough;
    private string?                    _textColor;
    private string?                    _bgColor;
    private TextHorizontalAlignment?   _hAlign;
    private TextVerticalAlignment?     _vAlign;
    private float?                     _paddingTop;
    private float?                     _paddingRight;
    private float?                     _paddingBottom;
    private float?                     _paddingLeft;

    public string?                    FontFamily    { get => _fontFamily;    set { _fontFamily    = NullIfEmpty(value); OnChanged(); OnChanged(nameof(HasExplicitFontFamily)); RefreshWeightOptions(); } }
    public float?                     FontSize      { get => _fontSize;      set { _fontSize      = value;              OnChanged(); OnChanged(nameof(HasExplicitFontSize)); } }
    public int?                       FontWeight    { get => _fontWeight;    set { _fontWeight    = value;              OnChanged(); OnChanged(nameof(HasExplicitFontWeight)); } }
    public bool?                      Bold          { get => _bold;          set { _bold          = value;              OnChanged(); OnChanged(nameof(EffectiveBold));          OnChanged(nameof(HasExplicitBold)); } }
    public bool?                      Italic        { get => _italic;        set { _italic        = value;              OnChanged(); OnChanged(nameof(EffectiveItalic));        OnChanged(nameof(HasExplicitItalic)); } }
    public bool?                      Underline     { get => _underline;     set { _underline     = value;              OnChanged(); OnChanged(nameof(EffectiveUnderline));     OnChanged(nameof(HasExplicitUnderline)); } }
    public bool?                      Strikethrough { get => _strikethrough; set { _strikethrough = value;              OnChanged(); OnChanged(nameof(EffectiveStrikethrough)); OnChanged(nameof(HasExplicitStrikethrough)); } }
    public string?                    TextColor     { get => _textColor;     set { SetColor(value, ref _textColor); OnChanged(); } }
    public string?                    BgColor       { get => _bgColor;       set { SetColor(value, ref _bgColor);   OnChanged(); } }
    public TextHorizontalAlignment?   HAlign        { get => _hAlign;        set { _hAlign        = value;              OnChanged(); OnChanged(nameof(HasExplicitHAlign)); } }
    public TextVerticalAlignment?     VAlign        { get => _vAlign;        set { _vAlign        = value;              OnChanged(); OnChanged(nameof(HasExplicitVAlign)); } }
    public float?                     PaddingTop    { get => _paddingTop;    set { _paddingTop    = value;              OnChanged(); } }
    public float?                     PaddingRight  { get => _paddingRight;  set { _paddingRight  = value;              OnChanged(); } }
    public float?                     PaddingBottom { get => _paddingBottom; set { _paddingBottom = value;              OnChanged(); } }
    public float?                     PaddingLeft   { get => _paddingLeft;   set { _paddingLeft   = value;              OnChanged(); } }

    // ── Derived: efektivní bool (explicit ?? inherited) pro 2-state checkboxy ──
    // Setter nastaví explicit hodnotu (user kliká → explicit override).
    // Reset na "dědí" = samostatný command (ResetFieldCommand s parametrem).

    public bool EffectiveBold          { get => _bold          ?? Inherited.Bold;          set => Bold          = value; }
    public bool EffectiveItalic        { get => _italic        ?? Inherited.Italic;        set => Italic        = value; }
    public bool EffectiveUnderline     { get => _underline     ?? Inherited.Underline;     set => Underline     = value; }
    public bool EffectiveStrikethrough { get => _strikethrough ?? Inherited.Strikethrough; set => Strikethrough = value; }

    // ── Derived: "má uživatel explicit override?" (pro viditelnost × tlačítek) ──

    public bool HasExplicitFontFamily    => _fontFamily    is not null;
    public bool HasExplicitFontSize      => _fontSize      is not null;
    public bool HasExplicitFontWeight    => _fontWeight    is not null;
    public bool HasExplicitBold          => _bold          is not null;
    public bool HasExplicitItalic        => _italic        is not null;
    public bool HasExplicitUnderline     => _underline     is not null;
    public bool HasExplicitStrikethrough => _strikethrough is not null;
    public bool HasExplicitHAlign        => _hAlign        is not null;
    public bool HasExplicitVAlign        => _vAlign        is not null;

    /// <summary>True pokud VM má aspoň jednu explicitně nastavenou vlastnost (= override).</summary>
    public bool HasAnyExplicit =>
        _fontFamily is not null || _fontSize is not null || _fontWeight is not null ||
        _bold is not null || _italic is not null ||
        _underline is not null || _strikethrough is not null ||
        _textColor is not null || _bgColor is not null ||
        _hAlign is not null || _vAlign is not null ||
        _paddingTop is not null || _paddingRight is not null ||
        _paddingBottom is not null || _paddingLeft is not null;

    // ── Rodičovský (zděděný) styl - read-only, pro placeholder zobrazení ──

    /// <summary>
    /// Vyřešený rodičovský styl (element-level resolved). UI ho ukazuje jako
    /// placeholder u prázdných polí - uživatel vidí, co bude platit, když nic nepřepíše.
    /// </summary>
    public ResolvedTextStyle Inherited { get; }

    // ── Řez (váha) - dynamická nabídka reálných řezů fontu ────────────────

    /// <summary>Jedna položka nabídky řezu. Value=null → „dědit" (auto dle Tučné).</summary>
    public sealed record WeightOption(int? Value, string Label);

    /// <summary>Nabídka řezů pro combobox - přepočítává se dle efektivní rodiny (explicit ?? zděděná).
    /// První položka = „Automaticky (dědit)".</summary>
    public IReadOnlyList<WeightOption> WeightOptions { get; private set; } = [];

    private void RefreshWeightOptions()
    {
        var family = _fontFamily ?? Inherited.FontFamily;
        var opts = new List<WeightOption> { new(null, "Automaticky") };
        opts.AddRange(FontWeightCatalog.Items(family).Select(x => new WeightOption(x.Weight, x.Label)));
        WeightOptions = opts;
        OnChanged(nameof(WeightOptions));
    }

    // ── Konstrukce ────────────────────────────────────────────────────────

    private CellStyleVM(ResolvedTextStyle inherited)
    {
        Inherited = inherited;

        // Reset pole na null (= dědit). Parametr = název property (např. "Bold").
        ResetFieldCommand = new RelayCommand<string?>(name =>
        {
            if (name is null) return;
            switch (name)
            {
                case nameof(FontFamily):    FontFamily    = null; break;
                case nameof(FontSize):      FontSize      = null; break;
                case nameof(FontWeight):    FontWeight    = null; break;
                case nameof(Bold):          Bold          = null; break;
                case nameof(Italic):        Italic        = null; break;
                case nameof(Underline):     Underline     = null; break;
                case nameof(Strikethrough): Strikethrough = null; break;
                case nameof(TextColor):     TextColor     = null; break;
                case nameof(BgColor):       BgColor       = null; break;
                case nameof(HAlign):        HAlign        = null; break;
                case nameof(VAlign):        VAlign        = null; break;
                case nameof(PaddingTop):    PaddingTop    = null; break;
                case nameof(PaddingRight):  PaddingRight  = null; break;
                case nameof(PaddingBottom): PaddingBottom = null; break;
                case nameof(PaddingLeft):   PaddingLeft   = null; break;
            }
        });

        PickTextColorCommand  = new RelayCommand(() => PickColor(TextColor, v => TextColor = v));
        ClearTextColorCommand = new RelayCommand(() => TextColor = null);
        PickBgColorCommand    = new RelayCommand(() => PickColor(BgColor, v => BgColor = v));
        ClearBgColorCommand   = new RelayCommand(() => BgColor = null);
    }

    private void PickColor(string? soucasna, Action<string?> zapis)
    {
        if (RequestColorPicker?.Invoke(soucasna ?? "") is { } zvolena) zapis(zvolena);
    }

    /// <summary>
    /// Command pro reset jednoho pole na „dědit" (null). CommandParameter = název property.
    /// </summary>
    public ICommand ResetFieldCommand { get; }

    /// <summary>
    /// Jak vyvolat výběr barvy (vstup = současná barva, výstup = zvolená nebo null při storno).
    /// Dosadí ho <see cref="CellStyleEditor"/>, protože jediný ví, v jakém okně sedí; nenastavené
    /// = tlačítka barev nic neudělají (návrhový režim, testy).
    /// </summary>
    public Func<string, string?>? RequestColorPicker { get; set; }

    /// <summary>
    /// Výběr a smazání barvy textu i pozadí. Patří sem, protože hodnoty drží tenhle VM - do 21. 9.
    /// si je psal každý dialog sloupců sám (8 příkazů + pomocná metoda), takže každý další dialog
    /// tu kopii zaplatil znovu.
    /// </summary>
    public ICommand PickTextColorCommand { get; }
    public ICommand ClearTextColorCommand { get; }
    public ICommand PickBgColorCommand { get; }
    public ICommand ClearBgColorCommand { get; }

    /// <summary>
    /// Vytvoří VM z volitelného explicit stylu + povinného rodičovského (vyřešeného) stylu.
    /// null explicit → všechna pole VM jsou null (čistě dědí).
    /// </summary>
    public static CellStyleVM FromModel(TextStyle? style, ResolvedTextStyle inherited)
    {
        var vm = new CellStyleVM(inherited)
        {
            _fontFamily    = style?.FontFamily,
            _fontSize      = style?.FontSize,
            _fontWeight    = style?.FontWeight,
            _bold          = style?.Bold,
            _italic        = style?.Italic,
            _underline     = style?.Underline,
            _strikethrough = style?.Strikethrough,
            _textColor     = style?.TextColor,
            _bgColor       = style?.BackgroundColor,
            _hAlign        = style?.HAlign,
            _vAlign        = style?.VAlign,
            _paddingTop    = style?.PaddingTop,
            _paddingRight  = style?.PaddingRight,
            _paddingBottom = style?.PaddingBottom,
            _paddingLeft   = style?.PaddingLeft,
        };
        vm.RefreshWeightOptions();   // nabídka řezů dle (explicit ?? zděděné) rodiny
        return vm;
    }

    /// <summary>
    /// Vytvoří TextStyle s pouze těmi vlastnostmi, které uživatel explicitně nastavil.
    /// Pokud uživatel nic nepřepsal, vrátí null.
    /// </summary>
    public TextStyle? ToModel()
    {
        var s = new TextStyle
        {
            FontFamily      = _fontFamily,
            FontSize        = _fontSize,
            FontWeight      = _fontWeight,
            Bold            = _bold,
            Italic          = _italic,
            Underline       = _underline,
            Strikethrough   = _strikethrough,
            TextColor       = _textColor,
            BackgroundColor = _bgColor,
            HAlign          = _hAlign,
            VAlign          = _vAlign,
            PaddingTop      = _paddingTop,
            PaddingRight    = _paddingRight,
            PaddingBottom   = _paddingBottom,
            PaddingLeft     = _paddingLeft,
        };
        return s.IsEmpty ? null : s;
    }

    /// <summary>Resetuje všechny explicitní hodnoty na null (→ plně se dědí z Inherited).</summary>
    public void Reset()
    {
        FontFamily    = null;
        FontSize      = null;
        FontWeight    = null;
        Bold          = null;
        Italic        = null;
        Underline     = null;
        Strikethrough = null;
        TextColor     = null;
        BgColor       = null;
        HAlign        = null;
        VAlign        = null;
        PaddingTop    = null;
        PaddingRight  = null;
        PaddingBottom = null;
        PaddingLeft   = null;
    }

    // ── INPC ──────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        // Jakákoli změna mění HasAnyExplicit → notifikovat vždy (ne jen u explicit fields)
        if (p != nameof(HasAnyExplicit))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAnyExplicit)));
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>Zapíše barvu, jen když je to platná hodnota (hex nebo barva z dat) - jinak nechá
    /// předchozí. Barva se od zavedení textového zápisu dá napsat rukou, a neplatná uložená hodnota
    /// se dřív projevila až při kreslení a šablona pak vypadala rozbitá (viz <see cref="ColorHex"/>).
    /// Notifikaci pošle volající vždy, takže se pole ve formuláři vrátí na platnou hodnotu.</summary>
    private static void SetColor(string? value, ref string? target)
    {
        var normalized = NullIfEmpty(value);
        if (normalized is null || ColorHex.IsValid(normalized)) target = normalized;
    }
}
