using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TemplateDesigner.Models;
using Brushes = System.Windows.Media.Brushes;

namespace TemplateDesigner.Controls;

public partial class TemplateTreePanel : UserControl
{
    private TemplateDesignerViewModel?   _vm;
    private Action<TemplateElement>?     _openElementProperties;
    private Action<Section>?             _openSectionProperties;
    private Action?                      _afterSelectionChanged;
    private bool                         _suppressSelection;

    public TemplateTreePanel() => InitializeComponent();

    public void SetContext(
        TemplateDesignerViewModel vm,
        Action<TemplateElement> openElementProperties,
        Action<Section> openSectionProperties,
        Action afterSelectionChanged)
    {
        _vm                    = vm;
        _openElementProperties = openElementProperties;
        _openSectionProperties = openSectionProperties;
        _afterSelectionChanged = afterSelectionChanged;
    }

    public void Rebuild()
    {
        if (_vm is null) return;
        _suppressSelection = true;
        try
        {
            Tree.Items.Clear();
            var template = _vm.State.CurrentTemplate;

            var root = new TreeViewItem
            {
                Header     = TemplateTreeNames.Sablona(template),
                FontWeight = FontWeights.Bold,
                IsExpanded = true
            };

            root.Items.Add(new TreeViewItem
            {
                Header     = TemplateTreeNames.Stranka(template.PageSize),
                Foreground = Brushes.Gray
            });

            foreach (var section in template.Sections)
            {
                var sNode = new TreeViewItem
                {
                    Header     = TemplateTreeNames.Sekce(section),
                    Tag        = section,
                    IsExpanded = true,
                    FontWeight = section == _vm.State.ActiveSection ? FontWeights.SemiBold : FontWeights.Normal
                };

                var orderedElements  = section.Elements.OrderBy(e => e.ZIndex).ToList();
                var processedGroups  = new HashSet<string>();

                foreach (var el in orderedElements)
                {
                    if (el.GroupId is not null && !processedGroups.Contains(el.GroupId))
                    {
                        processedGroups.Add(el.GroupId);
                        var groupMembers = orderedElements.Where(e2 => e2.GroupId == el.GroupId).ToList();
                        var groupNode = new TreeViewItem
                        {
                            Header     = TemplateTreeNames.Skupina(groupMembers.Count),
                            Tag        = el.GroupId,
                            IsExpanded = true,
                            FontStyle  = FontStyles.Italic
                        };

                        foreach (var member in groupMembers)
                            groupNode.Items.Add(CreateElementNode(member));
                        sNode.Items.Add(groupNode);
                    }
                    else if (el.GroupId is null)
                    {
                        sNode.Items.Add(CreateElementNode(el));
                    }
                }

                root.Items.Add(sNode);
            }

            Tree.Items.Add(root);

            if (_vm.State.SelectedElements.Count > 0)
            {
                var selectedEl   = _vm.State.SelectedElements[0];
                var itemToSelect = FindItemByTag(root, selectedEl);
                if (itemToSelect is not null)
                {
                    itemToSelect.IsSelected = true;
                    itemToSelect.Focus();
                }
            }
        }
        finally { _suppressSelection = false; }
    }

    private TreeViewItem CreateElementNode(TemplateElement el)
    {
        bool isSelected = _vm!.State.SelectedElements.Contains(el);
        return new TreeViewItem
        {
            Header     = TemplateTreeNames.Prvek(el),
            Tag        = el,
            FontWeight = isSelected ? FontWeights.Bold  : FontWeights.Normal,
            Foreground = isSelected ? Brushes.Blue      : Brushes.Black
        };
    }

    private static TreeViewItem? FindItemByTag(TreeViewItem parent, object tag)
    {
        foreach (TreeViewItem child in parent.Items)
        {
            if (child.Tag == tag) return child;
            var found = FindItemByTag(child, tag);
            if (found is not null) return found;
        }
        return null;
    }

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressSelection || _vm is null) return;
        if (e.NewValue is not TreeViewItem item) return;

        _vm.SelectFromTree(item.Tag);
        _afterSelectionChanged?.Invoke();
    }

    private void Tree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null and not TreeViewItem)
            source = VisualTreeHelper.GetParent(source);

        if (source is not TreeViewItem item) return;

        if (item.Tag is TemplateElement element)
            _openElementProperties?.Invoke(element);
        else if (item.Tag is Section section)
            _openSectionProperties?.Invoke(section);

        e.Handled = true;
    }

    private void Tree_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Tree.SelectedItem is TreeViewItem item)
        {
            if (item.Tag is TemplateElement element)
                _openElementProperties?.Invoke(element);
            else if (item.Tag is Section section)
                _openSectionProperties?.Invoke(section);
            e.Handled = true;
        }
    }
}
