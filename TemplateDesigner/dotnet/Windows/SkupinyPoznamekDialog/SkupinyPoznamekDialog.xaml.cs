using System.Collections.Generic;
using System.Windows;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

public partial class SkupinyPoznamekDialog : Window
{
    private readonly SkupinyPoznamekDialogViewModel _vm;

    /// <summary>Výsledné nastavení po OK - pass-through z VM.</summary>
    public List<NastaveniSkupinyPoznamek> Vysledek => _vm.Vysledek;

    public SkupinyPoznamekDialog(IReadOnlyList<NastaveniSkupinyPoznamek> skupiny, int sloupcuPrvku)
    {
        DataContext = _vm = new SkupinyPoznamekDialogViewModel(skupiny, sloupcuPrvku);
        InitializeComponent();
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }
}
