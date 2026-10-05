using System;
using System.IO;

namespace EDControls;

/// <summary>
/// <c>%APPDATA%\m-line\common</c> - složka sdílená všemi m-line nástroji (shell, TemplateDesigner),
/// ne jedné aplikaci. Jediné místo, které cestu skládá.
/// </summary>
internal static class MlineCommonPaths
{
    internal static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m-line", "common");

    internal static string File(string fileName) => Path.Combine(Folder, fileName);
}
