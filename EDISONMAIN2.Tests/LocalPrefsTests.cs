using System;
using System.IO;

namespace EDISONMAIN2.Tests;

/// <summary>
/// local.json: kompatibilní upgrade (chybějící klíč zůstane null, nepřepíše se defaultem),
/// odolnost proti poškozenému souboru - bootstrapper na Load spoléhá ještě před loginem,
/// takže výjimka by shodila start - a sloučení zápisů víc instancí shellu nad jedním souborem.
/// Cesta se přesměrovává přes internal seam <see cref="LocalPrefs.FilePath"/>, ne přes reálné %APPDATA%.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
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

    private static string? DevName(ILocalPrefs prefs) => prefs.LoginNameFor("dev-host", 57772);

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        WithTempPrefs(null, _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Null(prefs.LastIdfa);
            Assert.Null(prefs.LoginNames);
            Assert.Null(prefs.RememberedPasswords);
            Assert.Null(prefs.MiniAppLayouts);
            Assert.Null(prefs.TreeWidths);
            Assert.Null(prefs.SupportStripHeights);
            Assert.Null(prefs.Connections);
            Assert.Null(prefs.ShellWindowBounds);
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
            Assert.Null(prefs.LoginNames);
            Assert.Null(prefs.MiniAppLayouts);
            Assert.Null(prefs.TreeWidths);
            Assert.Null(prefs.SupportStripHeights);
        });
    }

    [Fact]
    public void Load_DeserializesOnDiskCamelCaseShape()
    {
        WithTempPrefs(
            """
            {"lastIdfa":"CB_DP","loginNames":{"dev-host:57772":"tester"},"rememberedPasswords":{"dev-host:57772":"blob"},
             "miniAppLayouts":{"CB_DP":"<xml/>"},
             "treeWidths":{"CB_DP":321.5},
             "supportStripHeights":{"CB_DP":512.5}}
            """, _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Equal("CB_DP", prefs.LastIdfa);
            Assert.Equal("tester", DevName(prefs));
            Assert.Equal("blob", prefs.RememberedPasswordFor("DEV-host", 57772));
            Assert.Equal("<xml/>", prefs.MiniAppLayouts!["CB_DP"]);
            Assert.Equal(321.5, prefs.TreeWidths!["CB_DP"]);
            Assert.Equal(512.5, prefs.SupportStripHeights!["CB_DP"]);
        });
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        WithTempPrefs(null, file =>
        {
            new LocalPrefs { LastIdfa = "CB_DP" }.Save();
            new LocalPrefs { LastIdfa = "CB_MLINEAS" }.Save();

            // Jen cílový soubor - dočasný soubor atomického zápisu nesmí zůstat ležet.
            Assert.Equal([file], Directory.GetFiles(Path.GetDirectoryName(file)!));
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
                LoginNames = new() { [ServerLogins.Key("dev-host", 57772)] = "tester" },
                MiniAppLayouts = new() { ["CB_DP"] = "<xml/>", ["CB_MLINEAS"] = "<xml2/>" },
                TreeWidths = new() { ["CB_DP"] = 280 },
                SupportStripHeights = new() { ["CB_DP"] = 520 },
            }.Save();

            var loaded = LocalPrefs.Load();

            Assert.Equal("tester", DevName(loaded));
            Assert.Equal(2, loaded.MiniAppLayouts!.Count);
            Assert.Equal("<xml2/>", loaded.MiniAppLayouts["CB_MLINEAS"]);
            Assert.Equal(280, loaded.TreeWidths!["CB_DP"]);
            Assert.Equal(520, loaded.SupportStripHeights!["CB_DP"]);
        });
    }

    [Fact]
    public void SaveLoad_RoundTripsConnections()
    {
        WithTempPrefs(null, _ =>
        {
            new LocalPrefs
            {
                Connections = [new SavedConnection { Name = "Test", Server = "10.0.0.5", RestPort = 57770 }],
            }.Save();

            var loaded = LocalPrefs.Load();

            var connection = Assert.Single(loaded.Connections!);
            Assert.Equal(("Test", "10.0.0.5", 57770), (connection.Name, connection.Server, connection.RestPort));
        });
    }

    [Fact]
    public void SaveLoad_RoundTripsShellWindowBounds()
    {
        // Geometrie hlavního okna: sdílí ji shell i přihlašovací okno, takže se čte hned
        // při startu, ještě před loginem.
        WithTempPrefs(null, _ =>
        {
            new LocalPrefs
            {
                // Monitor vlevo od primárního má zápornou pozici - nesmí se cestou ztratit.
                ShellWindowBounds = new WindowBoundsPrefs
                {
                    Left = -1800, Top = -120, Width = 1600, Height = 900, Maximized = true,
                },
            }.Save();

            var bounds = LocalPrefs.Load().ShellWindowBounds!;

            Assert.Equal((-1800, -120, 1600, 900), (bounds.Left, bounds.Top, bounds.Width, bounds.Height));
            Assert.True(bounds.Maximized);
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
            Assert.DoesNotContain("loginNames", json);
            Assert.DoesNotContain("rememberedPasswords", json);
        });
    }

    [Fact]
    public void Save_TwoInstances_KeepEachOthersChanges()
    {
        // Instance B uloží připojení, A se pak zavře a uloží layout ze snímku, ve kterém připojení chybí.
        WithTempPrefs("""{"lastIdfa":"CB_DP"}""", _ =>
        {
            var a = LocalPrefs.Load();
            var b = LocalPrefs.Load();

            b.Connections = [new SavedConnection { Name = "Test", Server = "10.0.0.5", RestPort = 57770 }];
            b.Save();
            (a.MiniAppLayouts ??= new())["CB_DP"] = "<xml/>";
            a.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal("Test", Assert.Single(loaded.Connections!).Name);
            Assert.Equal("<xml/>", loaded.MiniAppLayouts!["CB_DP"]);
            Assert.Equal("CB_DP", loaded.LastIdfa);
        });
    }

    [Fact]
    public void Save_TwoInstances_MergeDictionaryPerKey()
    {
        WithTempPrefs("""{"treeWidths":{"CB_DP":280}}""", _ =>
        {
            var a = LocalPrefs.Load();
            var b = LocalPrefs.Load();

            a.TreeWidths!["CB_DP"] = 300;
            a.Save();
            b.TreeWidths!["CB_MLINEAS"] = 250;
            b.Save();

            var widths = LocalPrefs.Load().TreeWidths!;
            Assert.Equal(300, widths["CB_DP"]);
            Assert.Equal(250, widths["CB_MLINEAS"]);
        });
    }

    [Fact]
    public void Save_TwoInstancesOnDifferentServers_KeepBothLogins()
    {
        // Dvě instalace na stanici (DEV a CLOUD1) běží zároveň a sdílí local.json.
        WithTempPrefs(null, _ =>
        {
            var dev = LocalPrefs.Load();
            var cloud = LocalPrefs.Load();

            dev.SetLoginName("dev-host", 57772, "jan.novak");
            dev.Save();
            cloud.SetLoginName("cloud1-host", 57773, "petr");
            cloud.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal("jan.novak", DevName(loaded));
            Assert.Equal("petr", loaded.LoginNameFor("cloud1-host", 57773));
        });
    }

    [Fact]
    public void Save_PasswordForgottenInOneInstance_KeepsNameSavedByOther()
    {
        // Jméno a heslo se slučují zvlášť: „Změnit heslo" ve starší instanci nesmí vrátit jméno,
        // pod kterým se na tomtéž serveru mezitím přihlásila jiná.
        WithTempPrefs("""{"loginNames":{"dev-host:57772":"jan"},"rememberedPasswords":{"dev-host:57772":"blob-jan"}}""", _ =>
        {
            var a = LocalPrefs.Load();
            var b = LocalPrefs.Load();

            b.SetLoginName("dev-host", 57772, "petr");
            b.Save();
            a.SetRememberedPassword("dev-host", 57772, null);
            a.Save();

            Assert.Equal("petr", DevName(LocalPrefs.Load()));
        });
    }

    [Fact]
    public void SetRememberedPassword_Null_RemovesOnlyThatServer()
    {
        WithTempPrefs("""{"rememberedPasswords":{"dev-host:57772":"blob","cloud1-host:57773":"blob2"}}""", file =>
        {
            var prefs = LocalPrefs.Load();
            prefs.SetRememberedPassword("dev-host", 57772, null);
            prefs.Save();

            // Null hodnota ve slovníku by se zapsala jako "klíč": null.
            Assert.DoesNotContain("null", File.ReadAllText(file));
            var loaded = LocalPrefs.Load();
            Assert.Null(loaded.RememberedPasswordFor("dev-host", 57772));
            Assert.Equal("blob2", loaded.RememberedPasswordFor("cloud1-host", 57773));
        });
    }

#pragma warning disable CS0618 // přihlášení starší verze
    [Fact]
    public void Save_AfterAdoptingLegacyLogin_KeepsLegacyKeys()
    {
        // Starší verze shellu na téže stanici z nich dál předvyplňuje.
        WithTempPrefs("""{"lastLoginName":"jan","rememberedPassword":"blob"}""", _ =>
        {
            var prefs = LocalPrefs.Load();
            prefs.AdoptLegacyLogin("dev-host", 57772);
            prefs.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal(("jan", "blob"), (loaded.LastLoginName, loaded.RememberedPassword));
            Assert.Equal(("jan", "blob"), (DevName(loaded), loaded.RememberedPasswordFor("dev-host", 57772)));
        });
    }
#pragma warning restore CS0618

    [Fact]
    public void Save_KeyRemovedByOneInstance_StaysRemoved()
    {
        WithTempPrefs("""{"miniAppLayouts":{"CB_DP":"<a/>","CB_MLINEAS":"<b/>"}}""", _ =>
        {
            var a = LocalPrefs.Load();
            var b = LocalPrefs.Load();

            a.MiniAppLayouts!.Remove("CB_DP");
            a.Save();
            b.MiniAppLayouts!["CB_MLINEAS"] = "<b2/>";
            b.Save();

            var layouts = LocalPrefs.Load().MiniAppLayouts!;
            Assert.False(layouts.ContainsKey("CB_DP"));
            Assert.Equal("<b2/>", layouts["CB_MLINEAS"]);
        });
    }

    [Fact]
    public void Save_SameKeyInBothInstances_LastWriterWins()
    {
        WithTempPrefs("""{"lastIdfa":"CB_DP","treeWidths":{"CB_DP":280}}""", _ =>
        {
            var a = LocalPrefs.Load();
            var b = LocalPrefs.Load();

            a.LastIdfa = "CB_A";
            a.TreeWidths!["CB_DP"] = 300;
            a.Save();
            b.LastIdfa = "CB_B";
            b.TreeWidths!["CB_DP"] = 320;
            b.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal("CB_B", loaded.LastIdfa);
            Assert.Equal(320, loaded.TreeWidths!["CB_DP"]);
        });
    }

    [Fact]
    public void Save_AdoptsMergedResultAsMemoryAndBase()
    {
        // Bez převzetí výsledku by další Save instance B vzal cizí klíč za "smazaný námi"
        // (zmizel by), nebo za "přidaný námi" (vzkřísil by ho po smazání v A).
        WithTempPrefs(null, _ =>
        {
            var a = LocalPrefs.Load();
            var b = LocalPrefs.Load();

            (a.TreeWidths ??= new())["CB_A"] = 280;
            a.Save();
            (b.TreeWidths ??= new())["CB_B"] = 300;
            b.Save();
            Assert.Equal(280, b.TreeWidths!["CB_A"]);

            b.LastIdfa = "CB_B";
            b.Save();
            Assert.Equal(280, LocalPrefs.Load().TreeWidths!["CB_A"]);

            a.TreeWidths!.Remove("CB_A");
            a.Save();
            b.TreeWidths!["CB_B"] = 320;
            b.Save();

            var widths = LocalPrefs.Load().TreeWidths!;
            Assert.False(widths.ContainsKey("CB_A"));
            Assert.Equal(320, widths["CB_B"]);
        });
    }

    [Theory]
    [InlineData("{ tohle není JSON")]
    [InlineData("""{"treeWidths":"široký"}""")]
    public void Save_OverCorruptFile_WritesOwnState(string content)
    {
        // Platný JSON se špatným typem hodnoty je taky poškozený: do sloučení by se dostal a výsledek by nešel načíst.
        WithTempPrefs(content, _ =>
        {
            var prefs = LocalPrefs.Load();
            prefs.LastIdfa = "CB_DP";
            prefs.Save();

            Assert.Equal("CB_DP", LocalPrefs.Load().LastIdfa);
        });
    }

    [Fact]
    public void Save_FileCorruptedSinceLoad_WritesWholeOwnState()
    {
        // Nečitelný disk zastoupí báze instance: zapíše se celý její stav, ne jen poslední změna.
        WithTempPrefs("""{"loginNames":{"dev-host:57772":"tester"},"treeWidths":{"CB_DP":280}}""", file =>
        {
            var prefs = LocalPrefs.Load();
            File.WriteAllText(file, "{ tohle není JSON");
            prefs.LastIdfa = "CB_DP";
            prefs.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal(("CB_DP", "tester"), (loaded.LastIdfa, DevName(loaded)));
            Assert.Equal(280, loaded.TreeWidths!["CB_DP"]);
        });
    }

    [Fact]
    public void Save_FileUnreadable_KeepsDiskAndWritesOnNextSave()
    {
        // Nečitelný soubor (síť, zámek jiného procesu) není poškozený: zápis vlastního stavu by smazal cizí změny.
        WithTempPrefs("""{"lastIdfa":"CB_DP","treeWidths":{"CB_DP":280}}""", file =>
        {
            var prefs = LocalPrefs.Load();
            prefs.SetLoginName("dev-host", 57772, "tester");
            using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                prefs.Save();
            }

            var untouched = File.ReadAllText(file);
            Assert.DoesNotContain("tester", untouched);

            prefs.Save();
            var loaded = LocalPrefs.Load();
            Assert.Equal(("tester", "CB_DP"), (DevName(loaded), loaded.LastIdfa));
            Assert.Equal(280, loaded.TreeWidths!["CB_DP"]);
        });
    }

    [Fact]
    public void Load_FileUnreadable_ReturnsDefaultsAndLaterSaveKeepsDisk()
    {
        WithTempPrefs("""{"loginNames":{"dev-host:57772":"tester"},"treeWidths":{"CB_DP":280}}""", file =>
        {
            LocalPrefs prefs;
            using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                prefs = LocalPrefs.Load();
            }
            Assert.Null(prefs.LoginNames);

            prefs.LastIdfa = "CB_DP";
            prefs.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal(("CB_DP", "tester"), (loaded.LastIdfa, DevName(loaded)));
            Assert.Equal(280, loaded.TreeWidths!["CB_DP"]);
        });
    }

    [Fact]
    public void Save_KeepsKeysOfNewerVersion()
    {
        // Starší instance (souběh přes update shellu) nesmí smazat preferenci, kterou nezná.
        WithTempPrefs("""{"lastIdfa":"CB_DP","futurePref":{"x":1}}""", file =>
        {
            var prefs = LocalPrefs.Load();
            prefs.SetLoginName("dev-host", 57772, "tester");
            prefs.Save();

            Assert.Contains("\"futurePref\"", File.ReadAllText(file));
            Assert.Equal("tester", DevName(LocalPrefs.Load()));
        });
    }

    [Fact]
    public void Save_ConcurrentInstances_LoseNoKey()
    {
        // Zámek kryje okno mezi čtením a zápisem; bez něj souběžné zápisy klíče ztrácí.
        WithTempPrefs(null, _ =>
        {
            const int savesPerInstance = 20;
            var instances = new[] { LocalPrefs.Load(), LocalPrefs.Load() };
            var errors = new Exception?[instances.Length];
            var threads = instances.Select((prefs, i) => new Thread(() =>
            {
                try
                {
                    for (var n = 0; n < savesPerInstance; n++)
                    {
                        (prefs.TreeWidths ??= new())[$"{i}_{n}"] = n;
                        prefs.Save();
                    }
                }
                catch (Exception e)
                {
                    errors[i] = e;
                }
            })).ToArray();

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            Assert.All(errors, e => Assert.Null(e));
            Assert.Equal(instances.Length * savesPerInstance, LocalPrefs.Load().TreeWidths!.Count);
        });
    }
}
