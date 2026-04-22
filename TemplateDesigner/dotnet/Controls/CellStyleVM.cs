using System.ComponentModel;
using System.Windows.Input;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

/// <summary>
/// ViewModel pro jednu TextStyle zónu (záhlaví NEBO data) v dialozích sloupců.
///
/// Všechny editovatelné vlastnosti jsou nullable (null = dědit z rodiče v kaskádě).
/// <see cref="Inherited"/> drží vyřešený rodičovský styl — UI ho zobrazuje jako placeholder/default
/// u každé vlastnosti, aby uživatel viděl, co bude skutečně platit, když nic nepřepíše.
///
/// Sdíleno všemi dialogy sloupců (DepartureGrid / StopList / TableElement).
/// </summary>
public class CellStyleVM : INotifyPropertyChanged
{
    // ── Editovatelné vlastnosti (null = dědit) ────────────────────────────

    private string?                    _fontFamily;
    private float?                     _fontSize;
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
    private float?                     _lineSpacing;

    public string?                    FontFamily    { get => _fontFamily;    set { _fontFamily    = NullIfEmpty(value); OnChanged(); OnChanged(nameof(HasExplicitFontFamily)); } }
    public float?                     FontSize      { get => _fontSize;      set { _fontSize      = value;              OnChanged(); OnChanged(nameof(HasExplicitFontSize)); } }
    public bool?                      Bold          { get => _bold;          set { _bold          = value;              OnChanged(); OnChanged(nameof(EffectiveBold));          OnChanged(nameof(HasExplicitBold)); } }
    public bool?                      Italic        { get => _italic;        set { _italic        = value;              OnChanged(); OnChanged(nameof(EffectiveItalic));        OnChanged(nameof(HasExplicitItalic)); } }
    public bool?                      Underline     { get => _underline;     set { _underline     = value;              OnChanged(); OnChanged(nameof(EffectiveUnderline));     OnChanged(nameof(HasExplicitUnderline)); } }
    public bool?                      Strikethrough { get => _strikethrough; set { _strikethrough = value;              OnChanged(); OnChanged(nameof(EffectiveStrikethrough)); OnChanged(nameof(HasExplicitStrikethrough)); } }
    public string?                    TextColor     { get => _textColor;     set { _textColor     = NullIfEmpty(value); OnChanged(); } }
    public string?                    BgColor       { get => _bgColor;       set { _bgColor       = NullIfEmpty(value); OnChanged(); } }
    public TextHorizontalAlignment?   HAlign        { get => _hAlign;        set { _hAlign        = value;              OnChanged(); OnChanged(nameof(HasExplicitHAlign)); } }
    public TextVerticalAlignment?     VAlign        { get => _vAlign;        set { _vAlign        = value;              OnChanged(); OnChanged(nameof(HasExplicitVAlign)); } }
    public float?                     PaddingTop    { get => _paddingTop;    set { _paddingTop    = value;              OnChanged(); } }
    public float?                     PaddingRight  { get => _paddingRight;  set { _paddingRight  = value;              OnChanged(); } }
    public float?                     PaddingBottom { get => _paddingBottom; set { _paddingBottom = value;              OnChanged(); } }
    public float?                     PaddingLeft   { get => _paddingLeft;   set { _paddingLeft   = value;              OnChanged(); } }
    public float?                     LineSpacing   { get => _lineSpacing;   set { _lineSpacing   = value;              OnChanged(); } }

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
    public bool HasExplicitBold          => _bold          is not null;
    public bool HasExplicitItalic        => _italic        is not null;
    public bool HasExplicitUnderline     => _underline     is not null;
    public bool HasExplicitStrikethrough => _strikethrough is not null;
    public bool HasExplicitHAlign        => _hAlign        is not null;
    public bool HasExplicitVAlign        => _vAlign        is not null;

    // ── Rodičovský (zděděný) styl — read-only, pro placeholder zobrazení ──

    /// <summary>
    /// Vyřešený rodičovský styl (element-level resolved). UI ho ukazuje jako
    /// placeholder u prázdných polí — uživatel vidí, co bude platit, když nic nepřepíše.
    /// </summary>
    public ResolvedTextStyle Inherited { get; }

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
                case nameof(LineSpacing):   LineSpacing   = null; break;
            }
        });
    }

    /// <summary>
    /// Command pro reset jednoho pole na „dědit" (null). CommandParameter = název property.
    /// </summary>
    public ICommand ResetFieldCommand { get; }

    /// <summary>
    /// Vytvoří VM z volitelného explicit stylu + povinného rodičovského (vyřešeného) stylu.
    /// null explicit → všechna pole VM jsou null (čistě dědí).
    /// </summary>
    public static CellStyleVM FromModel(TextStyle? style, ResolvedTextStyle inherited) => new(inherited)
    {
        _fontFamily    = style?.FontFamily,
        _fontSize      = style?.FontSize,
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
        _lineSpacing   = style?.LineSpacing,
    };

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
            LineSpacing     = _lineSpacing,
        };
        return s.IsEmpty ? null : s;
    }

    /// <summary>Resetuje všechny explicitní hodnoty na null (→ plně se dědí z Inherited).</summary>
    public void Reset()
    {
        FontFamily    = null;
        FontSize      = null;
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
        LineSpacing   = null;
    }

    // ── INPC ──────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
