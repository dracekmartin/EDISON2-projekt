using EDControls;
using EDControls.EDViewModel;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Windows.CompanyPickerWindow
{
    /// <summary>Výběr jiné firmy poté, co licence v <c>refused</c> nestačila (start i přihlášení jiného uživatele).</summary>
    public class CompanyPickerViewModel : EDViewModel
    {
        private readonly IReadOnlyList<CompanyStruct> _offered;

        public CompanyPickerViewModel(CompanyStruct refused, IReadOnlyList<CompanyStruct> offered)
            : this(EDServices.Current, refused, offered) { }

        /// <summary>DI/test overload - vlastní <c>EDServices</c> místo <c>EDServices.Current</c>.</summary>
        public CompanyPickerViewModel(EDServices services, CompanyStruct refused, IReadOnlyList<CompanyStruct> offered)
            : base(services)
        {
            _offered = offered;
            Message = $"Ve firmě {Display(refused)} byl překročen počet licencí. Vyberte jinou firmu.";
            Suggestions = [.. offered.Select(c => new ListItem(c.Id, c.Name))];
            SelectedCompanyId = offered.FirstOrDefault().Id;
        }

        public string Message { get; }

        /// <summary>Nabídka pro filtrovatelný výběr (Key = IDFa, Label = název), jako přepínač firem shellu.</summary>
        public IReadOnlyList<ListItem> Suggestions { get; }

        public string? SelectedCompanyId { get; set; }

        /// <summary>Vybraná firma; volající ji čte po <c>ShowDialog() == true</c>.</summary>
        public CompanyStruct? Picked =>
            _offered.Where(c => c.Id == SelectedCompanyId).Select(c => (CompanyStruct?)c).FirstOrDefault();

        // Formát přepínače firem (EDFilterComboBox „Key - Label").
        private static string Display(CompanyStruct company) =>
            string.IsNullOrEmpty(company.Name) ? company.Id : $"{company.Id} - {company.Name}";

        // Bez výběru se dialog nesmí zavřít jako potvrzený - volající by zakládal session v null firmě.
        protected override void OnOk()
        {
            if (Picked is null) return;
            base.OnOk();
        }

        // Okno se centruje na ownera, geometrie se nepersistuje (vzor MiniAppPickerViewModel).
        protected override string? PersistedFormId => null;
    }
}
