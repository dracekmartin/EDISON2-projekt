using System;
using System.IO;

namespace EDISONMAIN2.Tests;

/// <summary>
/// local.json: kompatibilní upgrade (chybějící klíč zůstane null, nepřepíše se defaultem)
/// a odolnost proti poškozenému souboru - bootstrapper na Load spoléhá ještě před loginem,
/// takže výjimka by shodila start. Cesta se přesměrovává přes internal seam
/// <see cref="LocalPrefs.FilePath"/>, ne přes reálné %APPDATA%.
/// </summary>
public class LocalPrefsTests
{
    /// <summary>Spustí <paramref name="body"/> s persistencí přesměrovanou do temp adresáře.</summary>
    private static void WithTempPrefs(string? initialContent, Action<string> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"edprefs_{Guid.NewGuid():N}");
        var file = Path.Combine(dir, "local.json");
        var saved = LocalPrefs.FilePath;
        try
        {
            if (initialContent != null)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(file, initialContent);
            }
            LocalPrefs.FilePath = file;
            body(file);
        }
        finally
        {
            LocalPrefs.FilePath = saved;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        WithTempPrefs(null, _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Null(prefs.LastIdfa);
            Assert.Null(prefs.LastLoginName);
            Assert.Null(prefs.RememberedPassword);
            Assert.False(prefs.ShowWindowsAsDialogs);
            Assert.Null(prefs.MiniAppLayouts);
            Assert.Null(prefs.TreeWidths);
        });
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        WithTempPrefs("{ tohle není JSON", _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Null(prefs.LastIdfa);
        });
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        WithTempPrefs("", _ => Assert.Null(LocalPrefs.Load().LastIdfa));
    }

    [Fact]
    public void Load_PartialFile_LeavesUnknownKeysNull()
    {
        // Kompatibilní upgrade: starší soubor nezná nové klíče a nesmí je dostat "prázdné"
        // hodnoty - null je signál "uživatel nic nenastavil".
        WithTempPrefs("""{"lastIdfa":"CB_DP"}""", _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Equal("CB_DP", prefs.LastIdfa);
            Assert.Null(prefs.LastLoginName);
            Assert.Null(prefs.MiniAppLayouts);
            Assert.Null(prefs.TreeWidths);
        });
    }

    [Fact]
    public void Load_DeserializesOnDiskCamelCaseShape()
    {
        WithTempPrefs(
            """
            {"lastIdfa":"CB_DP","lastLoginName":"tester","rememberedPassword":"blob",
             "showWindowsAsDialogs":true,"miniAppLayouts":{"CB_DP":"<xml/>"},
             "treeWidths":{"CB_DP":321.5}}
            """, _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Equal("CB_DP", prefs.LastIdfa);
            Assert.Equal("tester", prefs.LastLoginName);
            Assert.Equal("blob", prefs.RememberedPassword);
            Assert.True(prefs.ShowWindowsAsDialogs);
            Assert.Equal("<xml/>", prefs.MiniAppLayouts!["CB_DP"]);
            Assert.Equal(321.5, prefs.TreeWidths!["CB_DP"]);
        });
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        WithTempPrefs(null, file =>
        {
            new LocalPrefs { LastIdfa = "CB_DP" }.Save();

            Assert.True(File.Exists(file));
        });
    }

    [Fact]
    public void SaveLoad_RoundTripsDictionaries()
    {
        WithTempPrefs(null, _ =>
        {
            new LocalPrefs
            {
                LastIdfa = "CB_DP",
                LastLoginName = "tester",
                ShowWindowsAsDialogs = true,
                MiniAppLayouts = new() { ["CB_DP"] = "<xml/>", ["CB_MLINEAS"] = "<xml2/>" },
                TreeWidths = new() { ["CB_DP"] = 280 },
            }.Save();

            var loaded = LocalPrefs.Load();

            Assert.Equal("tester", loaded.LastLoginName);
            Assert.True(loaded.ShowWindowsAsDialogs);
            Assert.Equal(2, loaded.MiniAppLayouts!.Count);
            Assert.Equal("<xml2/>", loaded.MiniAppLayouts["CB_MLINEAS"]);
            Assert.Equal(280, loaded.TreeWidths!["CB_DP"]);
        });
    }

    [Fact]
    public void Save_WritesCamelCaseKeysAndSkipsNulls()
    {
        WithTempPrefs(null, file =>
        {
            new LocalPrefs { LastIdfa = "CB_DP" }.Save();
            var json = File.ReadAllText(file);

            Assert.Contains("\"lastIdfa\"", json);
            // Nulls se nezapisují - jinak by je příští Load nešel odlišit od "nenastaveno".
            Assert.DoesNotContain("lastLoginName", json);
            Assert.DoesNotContain("rememberedPassword", json);
        });
    }
}
