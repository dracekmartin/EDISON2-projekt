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

    private static string? DevIdfa(ILocalPrefs prefs) => prefs.LastIdfaFor("dev-host", 57772);

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        WithTempPrefs(null, _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Null(prefs.LastIdfaByServer);
            Assert.Null(prefs.LoginNames);
            Assert.Null(prefs.RememberedPasswords);
            Assert.Null(prefs.MiniAppLayouts);
            Assert.Null(prefs.TreeWidths);
            Assert.Null(prefs.SupportStripHeights);
            Assert.Null(prefs.Connections);
            Assert.Null(prefs.ShellWindowBoundsByServer);
        });
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        WithTempPrefs("{ tohle není JSON", _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Null(prefs.LastIdfaByServer);
        });
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        WithTempPrefs("", _ => Assert.Null(LocalPrefs.Load().LastIdfaByServer));
    }

    [Fact]
    public void Load_PartialFile_LeavesUnknownKeysNull()
    {
        // Kompatibilní upgrade: starší soubor nezná nové klíče a nesmí je dostat "prázdné"
        // hodnoty - null je signál "uživatel nic nenastavil".
        WithTempPrefs("""{"lastIdfaByServer":{"dev-host:57772":"CB_DP"}}""", _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Equal("CB_DP", DevIdfa(prefs));
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
            {"lastIdfaByServer":{"dev-host:57772":"CB_DP"},"loginNames":{"dev-host:57772":"tester"},
             "rememberedPasswords":{"dev-host:57772":"blob"},
             "shellWindowBoundsByServer":{"dev-host:57772":{"left":10,"top":20,"width":800,"height":600,"maximized":true}},
             "miniAppLayouts":{"CB_DP":"<xml/>"},
             "treeWidths":{"CB_DP":321.5},
             "supportStripHeights":{"CB_DP":512.5}}
            """, _ =>
        {
            var prefs = LocalPrefs.Load();

            Assert.Equal("CB_DP", prefs.LastIdfaFor("DEV-host", 57772));
            Assert.Equal("tester", DevName(prefs));
            Assert.Equal("blob", prefs.RememberedPasswordFor("DEV-host", 57772));
            Assert.Equal(800, prefs.ShellWindowBoundsFor("dev-host", 57772)!.Width);
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
            new LocalPrefs { TreeWidths = new() { ["CB_DP"] = 280 } }.Save();
            new LocalPrefs { TreeWidths = new() { ["CB_MLINEAS"] = 300 } }.Save();

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
                LastIdfaByServer = new() { [ServerPrefs.Key("dev-host", 57772)] = "CB_DP" },
                LoginNames = new() { [ServerPrefs.Key("dev-host", 57772)] = "tester" },
                MiniAppLayouts = new() { ["CB_DP"] = "<xml/>", ["CB_MLINEAS"] = "<xml2/>" },
                TreeWidths = new() { ["CB_DP"] = 280 },
                SupportStripHeights = new() { ["CB_DP"] = 520 },
            }.Save();

            var loaded = LocalPrefs.Load();

            Assert.Equal(("CB_DP", "tester"), (DevIdfa(loaded), DevName(loaded)));
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
        // Geometrie hlavního okna: záznam serveru sdílí shell i přihlašovací okno, takže se čte hned
        // při startu, ještě před loginem.
        WithTempPrefs(null, _ =>
        {
            var prefs = new LocalPrefs();
            // Monitor vlevo od primárního má zápornou pozici - nesmí se cestou ztratit.
            prefs.SetShellWindowBounds("dev-host", 57772, new WindowBoundsPrefs
            {
                Left = -1800, Top = -120, Width = 1600, Height = 900, Maximized = true,
            });
            prefs.Save();

            var bounds = LocalPrefs.Load().ShellWindowBoundsFor("dev-host", 57772)!;

            Assert.Equal((-1800, -120, 1600, 900), (bounds.Left, bounds.Top, bounds.Width, bounds.Height));
            Assert.True(bounds.Maximized);
        });
    }

    [Fact]
    public void Save_WritesCamelCaseKeysAndSkipsNulls()
    {
        WithTempPrefs(null, file =>
        {
            var prefs = new LocalPrefs();
            prefs.SetLastIdfa("dev-host", 57772, "CB_DP");
            prefs.Save();
            var json = File.ReadAllText(file);

            Assert.Contains("\"lastIdfaByServer\"", json);
            // Nulls se nezapisují - jinak by je příští Load nešel odlišit od "nenastaveno".
            Assert.DoesNotContain("loginNames", json);
            Assert.DoesNotContain("rememberedPasswords", json);
            // Globální hodnoty starší verze nový shell nezapisuje.
            Assert.DoesNotContain("\"lastIdfa\"", json);
            Assert.DoesNotContain("\"shellWindowBounds\"", json);
        });
    }

    [Fact]
    public void Save_TwoInstances_KeepEachOthersChanges()
    {
        // Instance B uloží připojení, A se pak zavře a uloží layout ze snímku, ve kterém připojení chybí.
        WithTempPrefs("""{"lastIdfaByServer":{"dev-host:57772":"CB_DP"}}""", _ =>
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
            Assert.Equal("CB_DP", DevIdfa(loaded));
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
    public void Save_TwoInstancesOnDifferentServers_KeepBothCompaniesAndWindows()
    {
        // Instalace Dev a Local mají stejná IDFa: firma ani okno jedné nesmí přepsat druhou.
        WithTempPrefs(null, _ =>
        {
            var dev = LocalPrefs.Load();
            var local = LocalPrefs.Load();

            dev.SetLastIdfa("dev-host", 57772, "CB_DP");
            dev.SetShellWindowBounds("dev-host", 57772, new WindowBoundsPrefs { Left = 0, Width = 800 });
            dev.Save();
            local.SetLastIdfa("localhost", 57772, "JI_DP");
            local.SetShellWindowBounds("localhost", 57772, new WindowBoundsPrefs { Left = 1920, Width = 1024 });
            local.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal(("CB_DP", "JI_DP"), (DevIdfa(loaded), loaded.LastIdfaFor("localhost", 57772)));
            Assert.Equal(0, loaded.ShellWindowBoundsFor("dev-host", 57772)!.Left);
            Assert.Equal(1920, loaded.ShellWindowBoundsFor("localhost", 57772)!.Left);
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

#pragma warning disable CS0618 // přihlášení, firma a geometrie okna starší verze
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

    [Fact]
    public void Save_WithServerValues_KeepsLegacyKeys()
    {
        // Starší verze shellu na téže stanici z nich dál čte firmu a geometrii okna.
        WithTempPrefs("""{"lastIdfa":"CB_DP","shellWindowBounds":{"left":10,"width":800}}""", _ =>
        {
            var prefs = LocalPrefs.Load();
            prefs.SetLastIdfa("dev-host", 57772, "JI_DP");
            prefs.SetShellWindowBounds("dev-host", 57772, new WindowBoundsPrefs { Left = 50, Width = 900 });
            prefs.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal(("CB_DP", "JI_DP"), (loaded.LastIdfa, DevIdfa(loaded)));
            Assert.Equal((10, 800), (loaded.ShellWindowBounds!.Left, loaded.ShellWindowBounds.Width));
        });
    }

    [Fact]
    public void LastIdfaOrLegacyFor_NoServerHasOwnCompany_ReturnsLegacyWithoutStoringIt()
    {
        // Firma serveru se uloží až po přijetí session, ne už při čtení před loginem.
        var prefs = new TransientLocalPrefs { LastIdfa = "CB_DP" };

        Assert.Equal("CB_DP", prefs.LastIdfaOrLegacyFor("dev-host", 57772));
        Assert.Null(prefs.LastIdfaByServer);
    }

    [Fact]
    public void LastIdfaOrLegacyFor_OtherServerHasOwnCompany_IgnoresLegacy()
    {
        // Firmu starší verze dostane jen první server, druhá instalace by jinak startovala ve firmě té první.
        var prefs = new TransientLocalPrefs { LastIdfa = "CB_DP" };
        prefs.SetLastIdfa("localhost", 57772, "JI_DP");

        Assert.Null(prefs.LastIdfaOrLegacyFor("dev-host", 57772));
        Assert.Equal("JI_DP", prefs.LastIdfaOrLegacyFor("localhost", 57772));
    }

    [Fact]
    public void LastIdfaOrLegacyFor_EmptyLegacyCompany_ReturnsNull() =>
        Assert.Null(new TransientLocalPrefs { LastIdfa = "" }.LastIdfaOrLegacyFor("dev-host", 57772));
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
        WithTempPrefs("""{"treeWidths":{"CB_DP":280}}""", _ =>
        {
            var a = LocalPrefs.Load();
            var b = LocalPrefs.Load();

            a.Connections = [new SavedConnection { Name = "A" }];
            a.TreeWidths!["CB_DP"] = 300;
            a.Save();
            b.Connections = [new SavedConnection { Name = "B" }];
            b.TreeWidths!["CB_DP"] = 320;
            b.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal("B", Assert.Single(loaded.Connections!).Name);
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

            b.SetLastIdfa("dev-host", 57772, "CB_B");
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
            prefs.SetLastIdfa("dev-host", 57772, "CB_DP");
            prefs.Save();

            Assert.Equal("CB_DP", DevIdfa(LocalPrefs.Load()));
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
            prefs.SetLastIdfa("dev-host", 57772, "CB_DP");
            prefs.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal(("CB_DP", "tester"), (DevIdfa(loaded), DevName(loaded)));
            Assert.Equal(280, loaded.TreeWidths!["CB_DP"]);
        });
    }

    [Fact]
    public void Save_FileUnreadable_KeepsDiskAndWritesOnNextSave()
    {
        // Nečitelný soubor (síť, zámek jiného procesu) není poškozený: zápis vlastního stavu by smazal cizí změny.
        WithTempPrefs("""{"lastIdfaByServer":{"dev-host:57772":"CB_DP"},"treeWidths":{"CB_DP":280}}""", file =>
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
            Assert.Equal(("tester", "CB_DP"), (DevName(loaded), DevIdfa(loaded)));
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

            prefs.SetLastIdfa("dev-host", 57772, "CB_DP");
            prefs.Save();

            var loaded = LocalPrefs.Load();
            Assert.Equal(("CB_DP", "tester"), (DevIdfa(loaded), DevName(loaded)));
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
