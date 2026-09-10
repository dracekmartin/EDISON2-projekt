using System.IO;
using System.Linq;
using System.Windows;
using EDControls.EDInputForm;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.UserProfileWindow;

public partial class UserProfileWindow : Window
{
    private readonly UserProfileViewModel _vm;

    // Zobrazený název výchozího okna (server ho nezná - dodá shell z modulového stromu).
    private readonly string _defaultWindowDisplay;

    /// <param name="defaultWindowDisplay">Název výchozího okna k zobrazení (prázdné = nenastaveno).</param>
    public UserProfileWindow(string defaultWindowDisplay)
    {
        _defaultWindowDisplay = defaultWindowDisplay;
        DataContext = _vm = new UserProfileViewModel();
        InitializeComponent();

        _vm.SaveRequested = SaveForm;
        TheForm.SchemaLoaded += OnFormInitialized;
        TheForm.ButtonClicked += OnFormButton;
        // Auto-Init proběhne na Loaded (CacheClass je nastaven) → OnFormInitialized prefilne data.
        TheForm.CacheClass = "ALVA.EDISONMAIN2.EDInputForm.UserProfile";
    }

    // Po Init naplní hodnoty klientsky (přehled z MLRest, export adresář z UserProfileService),
    // pak re-validace nastaví editovatelnost cesty dle přepínače „Použít osobní adresář".
    private void OnFormInitialized()
    {
        var ml = EDServices.Current.MLRest;
        if (ml is null) return;

        var p = UserProfileService.Load(ml);
        SetField("fullName", ml.FullName);
        SetField("loginName", ml.LoginName);
        SetField("mlp", ml.Mlp);
        SetField("usePersonDir", p.UsePersonDir ? "1" : "0");
        SetField("personDir", p.PersonDir);
        SetField("centralDir", p.CentralExportDir);
        SetField("defaultWindow", _defaultWindowDisplay);
        TheForm.Validate();
    }

    private void OnFormButton(string key)
    {
        switch (key)
        {
            case "changePassword": _vm.ChangePasswordCommand.Execute(null); break;
            case "certificates":   _vm.ShowCertificatesCommand.Execute(null); break;
        }
    }

    // OK: server BeforeSave validace (neprázdnost) → klientská kontrola existence složky → persist.
    private bool SaveForm()
    {
        if (!TheForm.Save()) return false;

        bool use = GetField("usePersonDir") == "1";
        string dir = GetField("personDir") ?? "";
        if (ValidateExportDir(use, dir, Directory.Exists) is { } error)
        {
            EDServices.Current.PopupDisplayer.Info(error);
            return false;
        }

        UserProfileService.SaveExportDir(EDServices.Current.RequireMLRest, use, dir);
        return true;
    }

    /// <summary>Klientský guard před persistem (server existenci složky nekontroluje); null = uložit.</summary>
    internal static string? ValidateExportDir(bool usePersonDir, string personDir, Func<string, bool> directoryExists) =>
        usePersonDir && !directoryExists(personDir) ? "Zadaný osobní adresář neexistuje." : null;

    private void SetField(string key, string value)
    {
        if (TheForm.Items?.OfType<Field>().FirstOrDefault(x => x.Key == key) is { } f)
            f.Value = value;
    }

    private string? GetField(string key) =>
        TheForm.Items?.OfType<Field>().FirstOrDefault(x => x.Key == key)?.Value;
}
