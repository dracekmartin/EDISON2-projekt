using EDControls.EDViewModel;

namespace TemplateDesigner;

public class SaveSectionTemplateDialogViewModel : EDViewModel
{
    private string _name;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public SaveSectionTemplateDialogViewModel(string initialName)
    {
        _name = initialName;
    }

    protected override void OnOk()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            PopupDisplayer.Info("Zadejte název šablony.");
            return;
        }
        Name = _name.Trim();
        CloseDialog(true);
    }
}
