using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace EDControls;

/// <summary>
/// Statická introspekce referencí .NET assembly — čte AssemblyRef metadata tabulku přes
/// <see cref="PEReader"/> bez načtení/spuštění assembly (na rozdíl od <c>Assembly.Load</c>;
/// <c>ReflectionOnlyLoad</c> na .NET Core+ není). Použito pro runtime detekci, jestli je
/// legacy modul retrofitnutý pro EDISONMAIN2 (referencuje EDISONMAIN2API → launchovatelný).
/// </summary>
public static class AssemblyReferences
{
    /// <summary>
    /// True pokud assembly na <paramref name="assemblyPath"/> referencuje assembly jména
    /// <paramref name="referencedAssemblyName"/> (case-insensitive, bez přípony). Chybějící /
    /// nečitelný / ne-PE soubor → false (nikdy nevyhazuje).
    /// </summary>
    public static bool References(string assemblyPath, string referencedAssemblyName)
    {
        try
        {
            if (!File.Exists(assemblyPath)) return false;

            using var stream = File.OpenRead(assemblyPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return false;

            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                if (string.Equals(reader.GetString(reference.Name), referencedAssemblyName,
                                   StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch (BadImageFormatException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
