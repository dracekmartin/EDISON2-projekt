using System;
using System.Collections.Generic;

namespace EDISONMAIN2;

/// <summary>
/// Připojení z příkazové řádky (<c>server=&lt;host&gt; restport=&lt;port&gt;</c>): „Připojit k databázi"
/// spouští novou instanci shellu proti jinému serveru, protože session v procesu je na jeden server.
/// Má přednost před <c>EDISONini.xml</c> a přeskakuje wizard; soubor zůstává centrálně řízený
/// (parita s legacy <c>forceChange</c>: výchozí připojení nemění, další jsou navíc).
/// </summary>
public sealed record ConnectionOverride(string Server, int RestPort)
{
    private const string ServerKey = "server=";
    private const string RestPortKey = "restport=";

    /// <summary>Null = argumenty připojení nenese (běžný start). Cizí tokeny (např. <c>reason=2</c>) ignoruje.</summary>
    public static ConnectionOverride? Parse(IReadOnlyList<string> args)
    {
        string server = "";
        int restPort = 0;
        foreach (var arg in args)
        {
            if (arg.StartsWith(ServerKey, StringComparison.OrdinalIgnoreCase))
                server = arg[ServerKey.Length..].Trim();
            else if (arg.StartsWith(RestPortKey, StringComparison.OrdinalIgnoreCase))
                int.TryParse(arg[RestPortKey.Length..].Trim(), out restPort);
        }
        return server.Length > 0 && restPort > 0 ? new ConnectionOverride(server, restPort) : null;
    }

    /// <summary>Argumenty pro spuštění nové instance; párové k <see cref="Parse"/>.</summary>
    public IReadOnlyList<string> ToArguments() => [$"{ServerKey}{Server}", $"{RestPortKey}{RestPort}"];
}
