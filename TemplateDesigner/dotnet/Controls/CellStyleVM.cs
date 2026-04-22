using System.ComponentModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

/// <summary>
/// ViewModel pro jednu TextStyle zónu (záhlaví NEBO data).
/// Všechny properties jsou nullable/defaultní — null = dědit z elementu.
/// Sdílený across all column-style dialogs (DepartureGrid, StopList, budoucí).
/// </summary>
public class CellStyleVM : INotifyPropertyChanged
{
    private string  _fontFamily = "";
    private float?  _fontSize;
    private bool    _bold;
    private bool    _italic;
    private bool    _underline;
    private bool    _strikethrough;
    private string  _textColor = "";
    private string  _bgColor = "";
    private int     _hAlignmentIndex;   // 0=Vlevo, 1=Střed, 2=Vpravo, -1=(nenastaveno)
    private int     _vAlignmentIndex;   // 0=Nahoru, 1=Střed, 2=Dolů,   -1=(nenastaveno)
    private float?  _paddingTop;
    private float?  _paddingRight;
    private float?  _paddingBottom;
    private float?  _paddingLeft;
    private float?  _lineSpacing;

    public string  FontFamily      { get => _fontFamily;      set { _fontFamily = value;      OnChanged(); } }
    public float?  FontSize        { get => _fontSize;        set { _fontSize = value;        OnChanged(); } }
    public bool    Bold            { get => _bold;            set { _bold = value;            OnChanged(); } }
    public bool    Italic          { get => _italic;          set { _italic = value;          OnChanged(); } }
    public bool    Underline       { get => _underline;       set { _underline = value;       OnChanged(); } }
    public bool    Strikethrough   { get => _strikethrough;   set { _strikethrough = value;   OnChanged(); } }
    public string  TextColor       { get => _textColor;       set { _textColor = value;       OnChanged(); } }
    public string  BgColor         { get => _bgColor;         set { _bgColor = value;         OnChanged(); } }
    public int     HAlignmentIndex { get => _hAlignmentIndex; set { _hAlignmentIndex = value; OnChanged(); } }
    public int     VAlignmentIndex { get => _vAlignmentIndex; set { _vAlignmentIndex = value; OnChanged(); } }
    public float?  PaddingTop      { get => _paddingTop;      set { _paddingTop = value;      OnChanged(); } }
    public float?  PaddingRight    { get => _paddingRight;    set { _paddingRight = value;    OnChanged(); } }
    public float?  PaddingBottom   { get => _paddingBottom;   set { _paddingBottom = value;   OnChanged(); } }
    public float?  PaddingLeft     { get => _paddingLeft;     set { _paddingLeft = value;     OnChanged(); } }
    public float?  LineSpacing     { get => _lineSpacing;     set { _lineSpacing = value;     OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    // ── Mapovací tabulky ──────────────────────────────────────────────────

    private static readonly TextHorizontalAlignment[] HAlignMap =
        [TextHorizontalAlignment.Left, TextHorizontalAlignment.Center, TextHorizontalAlignment.Right];

    private static readonly TextVerticalAlignment[] VAlignMap =
        [TextVerticalAlignment.Top, TextVerticalAlignment.Middle, TextVerticalAlignment.Bottom];

    // ── Konverze Model ↔ VM ───────────────────────────────────────────────

    public TextStyle? ToModel()
    {
        var s = new TextStyle
        {
            FontFamily      = NullIfEmpty(FontFamily),
            FontSize        = FontSize,
            // Explicit false (not null) — user must be able to override parent cascade that sets bold/italic=true
            Bold            = _bold,
            Italic          = _italic,
            Underline       = _underline,
            Strikethrough   = _strikethrough,
            TextColor       = NullIfEmpty(TextColor),
            BackgroundColor = NullIfEmpty(BgColor),
            HAlign          = _hAlignmentIndex >= 0 && _hAlignmentIndex < HAlignMap.Length ? HAlignMap[_hAlignmentIndex] : null,
            VAlign          = _vAlignmentIndex >= 0 && _vAlignmentIndex < VAlignMap.Length ? VAlignMap[_vAlignmentIndex] : null,
            PaddingTop      = PaddingTop,
            PaddingRight    = PaddingRight,
            PaddingBottom   = PaddingBottom,
            PaddingLeft     = PaddingLeft,
            LineSpacing     = LineSpacing,
        };
        return s.IsEmpty ? null : s;
    }

    public static CellStyleVM FromModel(TextStyle? s, bool defaultBold = false,
        string? defaultFontFamily = null, float? defaultFontSize = null) => new()
    {
        FontFamily      = s?.FontFamily ?? defaultFontFamily ?? "",
        FontSize        = s?.FontSize ?? defaultFontSize,
        Bold            = s?.Bold ?? defaultBold,
        Italic          = s?.Italic ?? false,
        Underline       = s?.Underline ?? false,
        Strikethrough   = s?.Strikethrough ?? false,
        TextColor       = s?.TextColor ?? "",
        BgColor         = s?.BackgroundColor ?? "",
        HAlignmentIndex = s?.HAlign switch
        {
            TextHorizontalAlignment.Left   => 0,
            TextHorizontalAlignment.Center => 1,
            TextHorizontalAlignment.Right  => 2,
            _ => -1
        },
        VAlignmentIndex = s?.VAlign switch
        {
            TextVerticalAlignment.Top    => 0,
            TextVerticalAlignment.Middle => 1,
            TextVerticalAlignment.Bottom => 2,
            _ => -1
        },
        PaddingTop    = s?.PaddingTop,
        PaddingRight  = s?.PaddingRight,
        PaddingBottom = s?.PaddingBottom,
        PaddingLeft   = s?.PaddingLeft,
        LineSpacing   = s?.LineSpacing,
    };

    /// <summary>Resetuje všechny properties na výchozí (null/prázdné).</summary>
    public void Reset()
    {
        FontFamily = "";
        FontSize = null;
        Bold = false;
        Italic = false;
        Underline = false;
        Strikethrough = false;
        TextColor = "";
        BgColor = "";
        HAlignmentIndex = -1;
        VAlignmentIndex = -1;
        PaddingTop = null;
        PaddingRight = null;
        PaddingBottom = null;
        PaddingLeft = null;
        LineSpacing = null;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
