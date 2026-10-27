using System.Globalization;

namespace EDControls;

/// <summary>
/// Launch kontrakt EDISONMAIN2: značka + 21 polí v uvozovkách. Jediný kodek pro producenta (shell,
/// <see cref="Format"/>) i konzumenta (TemplateDesigner, <see cref="TryParse"/>); pozice drží <see cref="Field"/>.
/// Zrcadlí <c>EDISONMAIN2API.FormToRunGetter</c> v repu EDISON (vzorový argv: <c>launch-args.fixture.txt</c> vedle něj
/// i v <c>EDControls.Tests/Fixtures</c>) - pole se jen přidávají na konec, starší host je ignoruje.
/// FormName je v kontraktu kvůli legacy WinForms modulům, WPF aplikace (TemplateDesigner) ho ignoruje.
/// </summary>
public sealed record EdisonLaunchArgs(
    string Server,
    int RestPort,
    string Namespace,
    string Idfa,
    string LoginName,
    string FullName,
    string Mlp,
    string FormName,
    string ShellGuid,
    string InputParam)
{
    public const string LaunchMarker = "EDISONMAIN2";

    /// <summary>Pozice v argv (0 = značka). Musí sedět s <c>FormToRunGetter</c> v repu EDISON.</summary>
    internal static class Field
    {
        public const int Marker = 0;
        public const int Server = 1;
        public const int RestPort = 2;
        public const int Namespace = 3;
        public const int Idfa = 4;
        public const int LoginName = 5;
        public const int FullName = 6;
        public const int Mlp = 7;
        public const int FormName = 8;
        public const int ShellGuid = 9;
        public const int InputParam = 10;
        public const int AgendaPath = 11;
        public const int LicensedElements = 12;
        public const int ShortcutPayload = 13;
        public const int IsAdmin = 14;
        public const int MayUpdate = 15;
        public const int UserDirectory = 16;
        public const int UserEmail = 17;
        public const int LicenceVersion = 18;
        public const int LicenceState = 19;
        public const int LicenceValidTo = 20;
        public const int ShellMonitor = 21;
        public const int Count = 22;
    }

    /// <summary>Cesta k agendě, kterou host načte přes <c>Assembly.LoadFrom</c>; prázdná = přímý launch (EDISONMAIN, TD).</summary>
    public string AgendaPath { get; init; } = "";

    /// <summary>Licencované elementy aktivní licence shellu (čárkou) - host z nich skládá licenci a přístupová práva.</summary>
    public string LicensedElements { get; init; } = "";

    /// <summary>Zástupce na ploše: neprůhledný payload, host ho místo formy spustí jako proceduru; prázdný = běžný launch.</summary>
    public string ShortcutPayload { get; init; } = "";

    /// <summary>→ <c>MLProps.UserIsAdmin</c>; host bez pole defaultuje na false.</summary>
    public bool IsAdmin { get; init; }

    /// <summary>→ <c>MLProps.UserMayUpdate</c> (právo serverového update); admin ho neimplikuje.</summary>
    public bool MayUpdate { get; init; }

    /// <summary>Osobní adresář z loginu → <c>MLProps.UserDirectory</c> (bez něj agendy exportují do kořene disku).</summary>
    public string UserDirectory { get; init; } = "";

    public string UserEmail { get; init; } = "";

    /// <summary>Verze, stav (<c>(int)Lic.State</c>) a konec platnosti licence shellu - host pak nevolá <c>GetActiveLic</c>.</summary>
    public string LicenceVersion { get; init; } = "";

    public string LicenceState { get; init; } = "";

    public string LicenceValidTo { get; init; } = "";

    /// <summary>Monitor shellu (jméno zařízení, <c>\\.\DISPLAY2</c>): okno bez uložené pozice se otevře na něm.</summary>
    public string ShellMonitor { get; init; } = "";

    /// <summary>První argument je značka kontraktu (bez ohledu na validitu zbytku).</summary>
    public static bool IsEdisonLaunch(string[] args) => Arg(args, Field.Marker) == LaunchMarker;

    /// <summary>
    /// Zparsuje argumenty kontraktu. Vrátí null pokud args nejsou EDISONMAIN2 launch
    /// nebo jsou neúplné/nevalidní (chybějící server, nečíselný port/mlp). Pole za ShellGuid jsou volitelná.
    /// </summary>
    public static EdisonLaunchArgs? TryParse(string[] args)
    {
        if (!IsEdisonLaunch(args) || args.Length <= Field.ShellGuid) return null;
        if (string.IsNullOrWhiteSpace(Arg(args, Field.Server))) return null;
        if (!int.TryParse(Arg(args, Field.RestPort), out int restPort) || restPort <= 0) return null;
        if (!int.TryParse(Arg(args, Field.Mlp), out int mlp)) return null;

        return new EdisonLaunchArgs(
            Server:     Arg(args, Field.Server),
            RestPort:   restPort,
            Namespace:  Arg(args, Field.Namespace),
            Idfa:       Arg(args, Field.Idfa),
            LoginName:  Arg(args, Field.LoginName),
            FullName:   Arg(args, Field.FullName),
            Mlp:        mlp.ToString(CultureInfo.InvariantCulture),
            FormName:   Arg(args, Field.FormName),
            ShellGuid:  Arg(args, Field.ShellGuid),
            InputParam: Arg(args, Field.InputParam))
        {
            AgendaPath = Arg(args, Field.AgendaPath),
            LicensedElements = Arg(args, Field.LicensedElements),
            ShortcutPayload = Arg(args, Field.ShortcutPayload),
            IsAdmin = Arg(args, Field.IsAdmin) == "1",
            MayUpdate = Arg(args, Field.MayUpdate) == "1",
            UserDirectory = Arg(args, Field.UserDirectory),
            UserEmail = Arg(args, Field.UserEmail),
            LicenceVersion = Arg(args, Field.LicenceVersion),
            LicenceState = Arg(args, Field.LicenceState),
            LicenceValidTo = Arg(args, Field.LicenceValidTo),
            ShellMonitor = Arg(args, Field.ShellMonitor),
        };
    }

    /// <summary>
    /// Argumenty procesu (bez cesty k exe). Host argv neescapuje, hodnoty se jen obalí uvozovkami: uvozovka
    /// nebo koncové zpětné lomítko (<c>"C:\dir\"</c> čte parser jako escapovanou uvozovku) by slily pole a posunuly
    /// identitu session, proto je <c>Plain</c> z každého textového pole odstraní. Mezera uvnitř uvozovek nevadí.
    /// </summary>
    internal string Format()
    {
        var values = new string[Field.Count];
        values[Field.Marker] = LaunchMarker;
        values[Field.Server] = Plain(Server);
        values[Field.RestPort] = RestPort.ToString(CultureInfo.InvariantCulture);
        values[Field.Namespace] = Plain(Namespace);
        values[Field.Idfa] = Plain(Idfa);
        values[Field.LoginName] = Plain(LoginName);
        values[Field.FullName] = Plain(FullName);
        values[Field.Mlp] = Plain(Mlp);
        values[Field.FormName] = Plain(FormName);
        values[Field.ShellGuid] = Plain(ShellGuid);
        values[Field.InputParam] = Plain(InputParam);
        values[Field.AgendaPath] = Plain(AgendaPath);
        values[Field.LicensedElements] = Plain(LicensedElements);
        values[Field.ShortcutPayload] = Plain(ShortcutPayload);
        values[Field.IsAdmin] = IsAdmin ? "1" : "0";
        values[Field.MayUpdate] = MayUpdate ? "1" : "0";
        values[Field.UserDirectory] = Plain(UserDirectory);
        values[Field.UserEmail] = Plain(UserEmail);
        values[Field.LicenceVersion] = Version(LicenceVersion);
        values[Field.LicenceState] = Plain(LicenceState);
        values[Field.LicenceValidTo] = Plain(LicenceValidTo);
        values[Field.ShellMonitor] = Plain(ShellMonitor);
        return LaunchMarker + string.Concat(values.Skip(Field.Server).Select(v => $" \"{v}\""));
    }

    private static string Arg(string[] args, int field) => field < args.Length ? args[field] : "";

    private static string Plain(string? value) => (value ?? "").Replace("\"", "").TrimEnd('\\');

    // Verze jde do legacy Val(): stačí číslice a tečka, cokoli dalšího by jen hrozilo rozbitím argv.
    private static string Version(string? version) => new((version ?? "").Where(c => char.IsDigit(c) || c == '.').ToArray());
}
