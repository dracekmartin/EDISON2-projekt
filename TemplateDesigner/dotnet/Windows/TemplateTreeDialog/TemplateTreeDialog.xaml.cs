using System.Windows;
using System.Windows.Controls;
using TemplateDesigner.Models;

namespace TemplateDesigner;

public partial class TemplateTreeDialog : Window
{
    public TemplateTreeDialog(Template template)
    {
        InitializeComponent();
        BuildTree(template);
    }

    private void BuildTree(Template template)
    {
        var root = new TreeViewItem
        {
            Header = $"Template: {template.Name}",
            FontWeight = FontWeights.Bold
        };

        // Page size
        var ps = template.PageSize;
        root.Items.Add(new TreeViewItem
        {
            Header = $"PageSize: {ps.Width}x{ps.Height} mm  (okraje: L={ps.MarginLeft} T={ps.MarginTop} R={ps.MarginRight} B={ps.MarginBottom})"
        });

        if (!string.IsNullOrEmpty(template.AgendaCode))
            root.Items.Add(new TreeViewItem { Header = $"Agenda: {template.AgendaCode}" });

        if (!string.IsNullOrEmpty(template.CultureCode))
            root.Items.Add(new TreeViewItem { Header = $"Culture: {template.CultureCode}" });

        // Sections
        var sectionsNode = new TreeViewItem
        {
            Header = $"Sections ({template.Sections.Count})",
            FontWeight = FontWeights.SemiBold
        };

        foreach (var section in template.Sections)
        {
            var sectionNode = new TreeViewItem
            {
                Header = $"[{section.PrintBehavior}] {section.Name}  (výška: {section.DesignerHeight?.ToString("0.#") ?? "auto"} mm)",
                FontWeight = FontWeights.Normal
            };

            if (!string.IsNullOrEmpty(section.VisibilityExpression))
                sectionNode.Items.Add(new TreeViewItem { Header = $"Visibility: {section.VisibilityExpression}" });

            if (!string.IsNullOrEmpty(section.DataSource))
                sectionNode.Items.Add(new TreeViewItem { Header = $"DataSource: {section.DataSource}" });

            // Elements
            var elementsNode = new TreeViewItem
            {
                Header = $"Elements ({section.Elements.Count})",
                FontWeight = FontWeights.Normal
            };

            foreach (var el in section.Elements.OrderBy(e => e.ZIndex))
            {
                var elNode = BuildElementNode(el);
                elementsNode.Items.Add(elNode);
            }

            sectionNode.Items.Add(elementsNode);
            sectionsNode.Items.Add(sectionNode);
        }

        root.Items.Add(sectionsNode);
        Tree.Items.Add(root);
    }

    private static TreeViewItem BuildElementNode(TemplateElement el)
    {
        string typeName = el.GetType().Name;
        string pos = $"({el.X:0.#}, {el.Y:0.#})  {el.Width:0.#}x{(el.Height?.ToString("0.#") ?? "auto")} mm";
        string details = GetElementDetails(el);

        var node = new TreeViewItem
        {
            Header = $"[Z{el.ZIndex}] {typeName}  {pos}"
        };

        if (!string.IsNullOrEmpty(details))
            node.Items.Add(new TreeViewItem { Header = details });

        if (el.IsLocked)
            node.Items.Add(new TreeViewItem { Header = "Locked" });

        if (!string.IsNullOrEmpty(el.VisibilityExpression))
            node.Items.Add(new TreeViewItem { Header = $"Visibility: {el.VisibilityExpression}" });

        return node;
    }

    private static string GetElementDetails(TemplateElement el) => TemplateElement.GetSummary(el);
}
