using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using ALWA.SYS;
using EDControls.EDInputForm;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

/// <summary>
/// PoC dialog pro editaci TextElement properties přes EDInputForm. Schema je
/// server-side v <c>ALVA.TemplateDesigner.EDInputForm.TextElementEdit</c>.
/// Dialog negeneruje přímé mutace — místo toho vrátí <see cref="ResultCommand"/>,
/// který volající aplikuje přes <c>DesignerState.Execute</c> (kvůli undo/redo).
/// </summary>
public partial class TextElementEditDialog : Window
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly TextElement _element;

    /// <summary>Composite command s změnami; null pokud user dal Cancel nebo se nic nezměnilo.</summary>
    public IDesignerCommand? ResultCommand { get; private set; }

    public TextElementEditDialog(TextElement element)
    {
        _element = element;
        InitializeComponent();

        // Pre-fill formuláře z aktuálních hodnot elementu (přes IN params do Caché Init).
        var init = new CVar();
        init["text"]                 = new CVar(element.Text ?? "");
        init["wordWrap"]             = new CVar(element.WordWrap ? "1" : "0");
        init["fontFamily"]           = new CVar(element.Style.FontFamily ?? "Arial");
        init["fontSize"]             = new CVar((element.Style.FontSize ?? 9f).ToString(Inv));
        init["bold"]                 = new CVar(element.Style.Bold == true ? "1" : "0");
        init["italic"]               = new CVar(element.Style.Italic == true ? "1" : "0");
        init["underline"]            = new CVar(element.Style.Underline == true ? "1" : "0");
        init["strikethrough"]        = new CVar(element.Style.Strikethrough == true ? "1" : "0");
        init["textColor"]            = new CVar(element.Style.TextColor ?? "#FF000000");
        init["backgroundColor"]      = new CVar(element.BackgroundColor ?? "#00FFFFFF");
        init["borderColor"]          = new CVar(element.BorderColor ?? "#FF000000");
        init["borderWidth"]          = new CVar(element.BorderWidth.ToString(Inv));
        init["hAlign"]               = new CVar(element.Style.HAlign?.ToString() ?? "Left");
        init["vAlign"]               = new CVar(element.Style.VAlign?.ToString() ?? "Middle");
        init["x"]                    = new CVar(element.X.ToString(Inv));
        init["y"]                    = new CVar(element.Y.ToString(Inv));
        init["width"]                = new CVar(element.Width.ToString(Inv));
        init["height"]               = new CVar(element.Height?.ToString(Inv) ?? "");
        init["isLocked"]             = new CVar(element.IsLocked ? "1" : "0");
        init["visibilityExpression"] = new CVar(element.VisibilityExpression ?? "");

        TheForm.InitParams = init;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TheForm.Save()) return;

        var fields = (TheForm.Items ?? []).OfType<Field>().ToDictionary(f => f.Key, f => f.Value ?? "");
        var commands = new List<IDesignerCommand>();

        // Text
        Add(commands, "Text",
            _element.Text, fields.GetValueOrDefault("text", ""),
            v => _element.Text = (string)v!);
        Add(commands, "Zalamování",
            _element.WordWrap, fields.GetValueOrDefault("wordWrap") == "1",
            v => _element.WordWrap = (bool)v!);

        // Font
        Add(commands, "Písmo",
            _element.Style.FontFamily, fields.GetValueOrDefault("fontFamily", "Arial"),
            v => _element.Style.FontFamily = (string?)v);
        if (TryParseFloat(fields.GetValueOrDefault("fontSize"), out var fs))
            Add(commands, "Velikost", _element.Style.FontSize, fs, v => _element.Style.FontSize = (float?)v);
        Add(commands, "Tučně",
            _element.Style.Bold ?? false, fields.GetValueOrDefault("bold") == "1",
            v => _element.Style.Bold = (bool)v!);
        Add(commands, "Kurzíva",
            _element.Style.Italic ?? false, fields.GetValueOrDefault("italic") == "1",
            v => _element.Style.Italic = (bool)v!);
        Add(commands, "Podtržené",
            _element.Style.Underline ?? false, fields.GetValueOrDefault("underline") == "1",
            v => _element.Style.Underline = (bool)v!);
        Add(commands, "Přeškrtnuté",
            _element.Style.Strikethrough ?? false, fields.GetValueOrDefault("strikethrough") == "1",
            v => _element.Style.Strikethrough = (bool)v!);

        // Barvy a rámeček
        Add(commands, "Barva textu",
            _element.Style.TextColor, fields.GetValueOrDefault("textColor", "#FF000000"),
            v => _element.Style.TextColor = (string?)v);
        Add(commands, "Barva pozadí",
            _element.BackgroundColor, fields.GetValueOrDefault("backgroundColor", "#00FFFFFF"),
            v => _element.BackgroundColor = (string)v!);
        Add(commands, "Barva rámečku",
            _element.BorderColor, fields.GetValueOrDefault("borderColor", "#FF000000"),
            v => _element.BorderColor = (string)v!);
        if (TryParseFloat(fields.GetValueOrDefault("borderWidth"), out var bw))
            Add(commands, "Tloušťka rámečku", _element.BorderWidth, bw, v => _element.BorderWidth = (float)v!);

        // Zarovnání
        if (System.Enum.TryParse<TextHorizontalAlignment>(fields.GetValueOrDefault("hAlign", "Left"), out var hAlign))
            Add(commands, "Horizontálně", _element.Style.HAlign, hAlign,
                v => _element.Style.HAlign = (TextHorizontalAlignment?)v);
        if (System.Enum.TryParse<TextVerticalAlignment>(fields.GetValueOrDefault("vAlign", "Middle"), out var vAlign))
            Add(commands, "Vertikálně", _element.Style.VAlign, vAlign,
                v => _element.Style.VAlign = (TextVerticalAlignment?)v);

        // Pozice a velikost
        if (TryParseFloat(fields.GetValueOrDefault("x"), out var x))
            Add(commands, "X", _element.X, x, v => _element.X = (float)v!);
        if (TryParseFloat(fields.GetValueOrDefault("y"), out var y))
            Add(commands, "Y", _element.Y, y, v => _element.Y = (float)v!);
        if (TryParseFloat(fields.GetValueOrDefault("width"), out var w))
            Add(commands, "Šířka", _element.Width, w, v => _element.Width = (float)v!);
        var heightStr = fields.GetValueOrDefault("height", "");
        float? newHeight = TryParseFloat(heightStr, out var h) && h > 0 ? h : null;
        Add(commands, "Výška", _element.Height, newHeight, v => _element.Height = (float?)v);

        // Pokročilé
        Add(commands, "Uzamčeno",
            _element.IsLocked, fields.GetValueOrDefault("isLocked") == "1",
            v => _element.IsLocked = (bool)v!);
        var visExpr = fields.GetValueOrDefault("visibilityExpression", "");
        string? newVisExpr = string.IsNullOrWhiteSpace(visExpr) ? null : visExpr;
        Add(commands, "Podmínka viditelnosti",
            _element.VisibilityExpression, newVisExpr,
            v => _element.VisibilityExpression = (string?)v);

        ResultCommand = commands.Count switch
        {
            0 => null,
            1 => commands[0],
            _ => new CompositeCommand("Změna vlastností textu", commands)
        };

        DialogResult = true;
        Close();
    }

    /// <summary>Přidá SetPropertyCommand do listu pokud se hodnota změnila.</summary>
    private static void Add(List<IDesignerCommand> list, string label, object? oldValue, object? newValue, System.Action<object?> apply)
    {
        if (Equals(oldValue, newValue)) return;
        list.Add(new SetPropertyCommand(label, oldValue, newValue, apply));
    }

    private static bool TryParseFloat(string? s, out float val) =>
        float.TryParse(s, NumberStyles.Float, Inv, out val);
}
