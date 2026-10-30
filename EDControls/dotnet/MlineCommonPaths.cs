using System;
using System.IO;

namespace EDControls;

/// <summary>
/// <c>%APPDATA%\m-line\common</c> - složka sdílená všemi m-line nástroji (shell, TemplateDesigner),
/// ne jedné aplikaci. Jediné místo, které cestu skládá.
/// </summary>
internal static class MlineCommonPaths
{
    private static bool _handedOut;

    internal static string Folder
    {
        get
        {
            _handedOut = true;
            return FolderFor(Redirected);
        }
    }

    internal static string File(string fileName) => Path.Combine(Folder, fileName);

    /// <summary>Složka místo výchozí (<see cref="EDAppearance.UseCommonFolder"/>); null = výchozí.</summary>
    internal static string? Redirected { get; private set; }

    /// <summary>Už si někdo cestu vzal (úložiště tématu, písma, log chyb)?</summary>
    internal static bool HandedOut => _handedOut;

    internal static string FolderFor(string? redirected) => redirected ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m-line", "common");

    internal static void Redirect(string folder)
    {
        // Úložiště tématu a písma si cestu berou jednou při inicializaci typu: pozdě by se přesunul jen log chyb.
        if (_handedOut)
            throw new InvalidOperationException("Sdílenou složku m-line už někdo použil, přesměrování musí přijít dřív.");
        Redirected = folder;
    }

    /// <summary>Jen pro testy: vrátí stav uložený před testem.</summary>
    internal static void Restore(string? redirected, bool handedOut)
    {
        Redirected = redirected;
        _handedOut = handedOut;
    }
}
