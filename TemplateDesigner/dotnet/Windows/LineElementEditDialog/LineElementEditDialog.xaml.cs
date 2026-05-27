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
/// Editor LineElement properties přes EDInputForm. Schema server-side v
/// <c>ALVA.TemplateDesigner.EDInputForm.LineElementEdit</c>.
/// Pre-fill přes InitParams; na Save vyrobí CompositeCommand z SetPropertyCommandů.
/// </summary>
public partial class LineElementEditDialog : Window
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly LineElement _element;

    public IDesignerCommand? ResultCommand { get; private set; }

    public LineElementEditDialog(LineElement element)
    {
        _element = element;
        InitializeComponent();

        var init = new CVar();
        init["orientation"]          = new CVar(element.Orientation.ToString());
        init["color"]                = new CVar(element.Color ?? "#FF000000");
        init["strokeWidth"]          = new CVar(element.StrokeWidth.ToString(Inv));
        init["style"]                = new CVar(element.Style.ToString());
        init["dashMm"]               = new CVar(element.CustomDashMm?.ToString(Inv) ?? "");
        init["gapMm"]                = new CVar(element.CustomGapMm?.ToString(Inv) ?? "");
        init["x"]                    = new CVar(element.X.ToString(Inv));
        init["y"]                    = new CVar(element.Y.ToString(Inv));
        init["width"]                = new CVar(element.Width.ToString(Inv));
        init["height"]               = new CVar((element.Height ?? 0).ToString(Inv));
        init["rotation"]             = new CVar(element.Rotation.ToString(Inv));
        init["zIndex"]               = new CVar(element.ZIndex.ToString(Inv));
        init["isLocked"]             = new CVar(element.IsLocked ? "1" : "0");
        init["visibilityExpression"] = new CVar(element.VisibilityExpression ?? "");

        TheForm.InitParams = init;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TheForm.Save()) return;

        var fields = (TheForm.Items ?? []).OfType<Field>().ToDictionary(f => f.Key, f => f.Value ?? "");
        var commands = new List<IDesignerCommand>();

        // Čára
        if (System.Enum.TryParse<LineOrientation>(fields.GetValueOrDefault("orientation", "Horizontal"), out var orient))
            Add(commands, "Orientace", _element.Orientation, orient, v => _element.Orientation = (LineOrientation)v!);
        Add(commands, "Barva",
            _element.Color, fields.GetValueOrDefault("color", "#FF000000"),
            v => _element.Color = (string)v!);
        if (TryParseFloat(fields.GetValueOrDefault("strokeWidth"), out var sw))
            Add(commands, "Tloušťka", _element.StrokeWidth, sw, v => _element.StrokeWidth = (float)v!);
        if (System.Enum.TryParse<LineStyle>(fields.GetValueOrDefault("style", "Solid"), out var style))
            Add(commands, "Styl", _element.Style, style, v => _element.Style = (LineStyle)v!);
        float? newDashMm = TryParseFloat(fields.GetValueOrDefault("dashMm", ""), out var dm) && dm > 0 ? dm : null;
        Add(commands, "Délka čárky", _element.CustomDashMm, newDashMm, v => _element.CustomDashMm = (float?)v);
        float? newGapMm = TryParseFloat(fields.GetValueOrDefault("gapMm", ""), out var gm) && gm > 0 ? gm : null;
        Add(commands, "Délka mezery", _element.CustomGapMm, newGapMm, v => _element.CustomGapMm = (float?)v);

        // Pozice a velikost
        if (TryParseFloat(fields.GetValueOrDefault("x"), out var x))
            Add(commands, "X", _element.X, x, v => _element.X = (float)v!);
        if (TryParseFloat(fields.GetValueOrDefault("y"), out var y))
            Add(commands, "Y", _element.Y, y, v => _element.Y = (float)v!);
        if (TryParseFloat(fields.GetValueOrDefault("width"), out var w))
            Add(commands, "Šířka", _element.Width, w, v => _element.Width = (float)v!);
        float? newHeight = TryParseFloat(fields.GetValueOrDefault("height", ""), out var h) && h > 0 ? h : null;
        Add(commands, "Výška", _element.Height, newHeight, v => _element.Height = (float?)v);
        if (TryParseFloat(fields.GetValueOrDefault("rotation"), out var rot))
            Add(commands, "Rotace", _element.Rotation, rot, v => _element.Rotation = (float)v!);
        if (int.TryParse(fields.GetValueOrDefault("zIndex"), out var z))
            Add(commands, "ZIndex", _element.ZIndex, z, v => _element.ZIndex = (int)v!);

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
            _ => new CompositeCommand("Změna vlastností čáry", commands)
        };

        DialogResult = true;
        Close();
    }

    private static void Add(List<IDesignerCommand> list, string label, object? oldValue, object? newValue, System.Action<object?> apply)
    {
        if (Equals(oldValue, newValue)) return;
        list.Add(new SetPropertyCommand(label, oldValue, newValue, apply));
    }

    private static bool TryParseFloat(string? s, out float val) =>
        float.TryParse(s, NumberStyles.Float, Inv, out val);
}
