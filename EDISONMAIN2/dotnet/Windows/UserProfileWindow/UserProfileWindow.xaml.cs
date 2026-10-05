using System.Linq;
using System.Windows;
using EDControls.EDInputForm;

namespace EDISONMAIN2.Windows.UserProfileWindow;

public partial class UserProfileWindow : Window, IUserProfileForm
{
    private readonly UserProfileViewModel _vm;

    /// <param name="defaultWindowDisplay">Název výchozího okna k zobrazení (prázdné = nenastaveno).</param>
    public UserProfileWindow(string defaultWindowDisplay)
    {
        DataContext = _vm = new UserProfileViewModel(defaultWindowDisplay);
        InitializeComponent();

        _vm.Form = this;
        TheForm.SchemaLoaded += _vm.FillForm;
        TheForm.ButtonClicked += OnFormButton;
        // Auto-Init proběhne na Loaded (CacheClass je nastaven) → FillForm prefilne data.
        TheForm.CacheClass = "ALVA.EDISONMAIN2.EDInputForm.UserProfile";
    }

    private void OnFormButton(string key)
    {
        switch (key)
        {
            case "changePassword": _vm.ChangePasswordCommand.Execute(null); break;
            case "certificates":   _vm.ShowCertificatesCommand.Execute(null); break;
        }
    }

    bool IUserProfileForm.Save() => TheForm.Save();

    void IUserProfileForm.Validate() => TheForm.Validate();

    string? IUserProfileForm.Get(string key) => FieldOf(key)?.Value;

    void IUserProfileForm.Set(string key, string value)
    {
        if (FieldOf(key) is { } field) field.Value = value;
    }

    private Field? FieldOf(string key) => TheForm.Items?.OfType<Field>().FirstOrDefault(x => x.Key == key);
}
