namespace EDControls;

/// <summary>
/// Argumentový kontrakt EDISONMAIN2 pro spouštění samostatných aplikací se session shellu.
/// Zrcadlí <c>EDISONMAIN2API.FormToRunGetter</c> (c:\MLineRepos\EDISON\EDISONMAIN2API):
/// [0]="EDISONMAIN2", [1]=server, [2]=restPort, [3]=namespace, [4]=idfa,
/// [5]=loginName, [6]=fullName, [7]=mlp, [8]=formName, [9]=shellGuid, [10]=inputParam (volitelný).
/// FormName a ShellGuid jsou součástí kontraktu kvůli legacy WinForms modulům -
/// WPF aplikace (TemplateDesigner) je ignorují, stejně jako pole [11]-[15] hosta
/// (cesta agendy, licencované elementy, zástupce, admin flag, právo update) - viz WindowNavigator.BuildLauncherArguments.
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

    /// <summary>První argument je značka kontraktu (bez ohledu na validitu zbytku).</summary>
    public static bool IsEdisonLaunch(string[] args) => args.Length > 0 && args[0] == LaunchMarker;

    /// <summary>
    /// Zparsuje argumenty kontraktu. Vrátí null pokud args nejsou EDISONMAIN2 launch
    /// nebo jsou neúplné/nevalidní (chybějící server, nečíselný port/mlp).
    /// </summary>
    public static EdisonLaunchArgs? TryParse(string[] args)
    {
        if (!IsEdisonLaunch(args) || args.Length < 10) return null;
        if (string.IsNullOrWhiteSpace(args[1])) return null;
        if (!int.TryParse(args[2], out int restPort) || restPort <= 0) return null;
        if (!int.TryParse(args[7], out int mlp)) return null;

        return new EdisonLaunchArgs(
            Server:     args[1],
            RestPort:   restPort,
            Namespace:  args[3],
            Idfa:       args[4],
            LoginName:  args[5],
            FullName:   args[6],
            Mlp:        mlp.ToString(),
            FormName:   args[8],
            ShellGuid:  args[9],
            InputParam: args.Length > 10 ? args[10] : "");
    }
}
