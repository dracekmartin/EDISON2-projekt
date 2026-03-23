using System.ComponentModel;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

/// <summary>
/// ViewModel pro jednu CellStyle zónu (záhlaví NEBO data).
/// Všechny properties jsou nullable/defaultní — null = dědit z elementu.
/// Sdílený across all column-style dialogs (DepartureGrid, StopList, budoucí).
/// </summary>
public class CellStyleVM : INotifyPropertyChanged
{
    private string  _fontFamily = "";
    private float?  _fontSize;
    private bool    _bold;
    private bool    _italic;
    private string  _textColor = "";
    private string  _bgColor = "";
    private int     _hAlignmentIndex;   // 0=Vlevo, 1=Střed, 2=Vpravo, -1=(nenastaveno)
    private float?  _paddingTop;
    private float?  _paddingRight;
    private float?  _paddingBottom;
    private float?  _paddingLeft;

    public string  FontFamily      { get => _fontFamily;      set { _fontFamily = value;      OnChanged(); } }
    public float?  FontSize        { get => _fontSize;        set { _fontSize = value;        OnChanged(); } }
    public bool    Bold            { get => _bold;            set { _bold = value;            OnChanged(); } }
    public bool    Italic          { get => _italic;          set { _italic = value;          OnChanged(); } }
    public string  TextColor       { get => _textColor;       set { _textColor = value;       OnChanged(); } }
    public string  BgColor         { get => _bgColor;         set { _bgColor = value;         OnChanged(); } }
    public int     HAlignmentIndex { get => _hAlignmentIndex; set { _hAlignmentIndex = value; OnChanged(); } }
    public float?  PaddingTop      { get => _paddingTop;      set { _paddingTop = value;      OnChanged(); } }
    public float?  PaddingRight    { get => _paddingRight;    set { _paddingRight = value;    OnChanged(); } }
    public float?  PaddingBottom   { get => _paddingBottom;   set { _paddingBottom = value;   OnChanged(); } }
    public float?  PaddingLeft     { get => _paddingLeft;     set { _paddingLeft = value;     OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    // ── Mapovací tabulky ──────────────────────────────────────────────────

    private static readonly TextHorizontalAlignment[] HAlignMap =
        [TextHorizontalAlignment.Left, TextHorizontalAlignment.Center, TextHorizontalAlignment.Right];

    // ── Konverze Model ↔ VM ───────────────────────────────────────────────

    public CellStyle? ToModel()
    {
        var s = new CellStyle
        {
            FontFamily      = NullIfEmpty(FontFamily),
            FontSize        = FontSize,
            Bold            = _bold ? true : null,
            Italic          = _italic ? true : null,
            TextColor       = NullIfEmpty(TextColor),
            BackgroundColor = NullIfEmpty(BgColor),
            HAlignment      = _hAlignmentIndex >= 0 && _hAlignmentIndex < HAlignMap.Length ? HAlignMap[_hAlignmentIndex] : null,
            PaddingTop      = PaddingTop,
            PaddingRight    = PaddingRight,
            PaddingBottom   = PaddingBottom,
            PaddingLeft     = PaddingLeft,
        };
        return s.IsEmpty ? null : s;
    }

    public static CellStyleVM FromModel(CellStyle? s, bool defaultBold = false) => new()
    {
        FontFamily      = s?.FontFamily ?? "",
        FontSize        = s?.FontSize,
        Bold            = s?.Bold ?? defaultBold,
        Italic          = s?.Italic ?? false,
        TextColor       = s?.TextColor ?? "",
        BgColor         = s?.BackgroundColor ?? "",
        HAlignmentIndex = s?.HAlignment switch
        {
            TextHorizontalAlignment.Left   => 0,
            TextHorizontalAlignment.Center => 1,
            TextHorizontalAlignment.Right  => 2,
            _ => -1
        },
        PaddingTop    = s?.PaddingTop,
        PaddingRight  = s?.PaddingRight,
        PaddingBottom = s?.PaddingBottom,
        PaddingLeft   = s?.PaddingLeft,
    };

    /// <summary>Resetuje všechny properties na výchozí (null/prázdné).</summary>
    public void Reset()
    {
        FontFamily = "";
        FontSize = null;
        Bold = false;
        Italic = false;
        TextColor = "";
        BgColor = "";
        HAlignmentIndex = -1;
        PaddingTop = null;
        PaddingRight = null;
        PaddingBottom = null;
        PaddingLeft = null;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
