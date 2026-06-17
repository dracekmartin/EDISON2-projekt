namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public record ExecutableModule : IModule
    {
        public string DisplayedName { get; init; } = "";

        public string ExecutablePath { get; init; } = "";

        public string FormName { get; init; } = "";

        /// <summary>Numerické ID z EDISFormEnum (string). Klíč pro licenční gate; jinak unikátní identifikátor uzlu.</summary>
        public string ElementId { get; init; } = "";

        /// <summary>Když true, <see cref="ElementId"/> se kontroluje proti <c>Lic.Elements</c>; jinak modul vidí každý.</summary>
        public bool IsLicensed { get; init; } = false;

        /// <summary>
        /// Volitelný launch parametr předaný formě jako <c>EDParamIn</c> (string).
        /// Legacy navbar <c>input="AdminMode=1"</c> ekvivalent. Default <c>""</c> = forma použije svůj default.
        /// </summary>
        public string InputParam { get; init; } = "";

        /// <summary>Když true, modul se ve stromu nezobrazí. Legacy `hidden="1"` ekvivalent.</summary>
        public bool Hidden { get; init; } = false;

        /// <summary>
        /// Hex barva (<c>"#RRGGBB"</c>) pro Foreground položky ve stromu. Prázdný / null = výchozí
        /// barva tématu. Konvence: <c>"#FFA500"</c> (oranžová) = autorský stub bez vyplněné
        /// <c>.exe</c>; <c>"#FF0000"</c> (červená) = exe vyplněné, ale soubor chybí.
        /// </summary>
        public string Color { get; init; } = "";
    }
}
