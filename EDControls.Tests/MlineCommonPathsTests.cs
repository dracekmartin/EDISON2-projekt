using System.IO;

namespace EDControls.Tests;

/// <summary>Přesměrování sdílené složky m-line pro E2E běh shellu.</summary>
[Collection(SharedStaticStateCollection.Name)]
public class MlineCommonPathsTests
{
    private const string E2EFolder = @"C:\temp\EDISON2.E2E\common";

    [Fact]
    public void FolderFor_WithoutRedirect_IsSharedMlineCommon()
    {
        Assert.EndsWith(Path.Combine("m-line", "common"), MlineCommonPaths.FolderFor(null));
        Assert.Equal(E2EFolder, MlineCommonPaths.FolderFor(E2EFolder));
    }

    [Fact]
    public void UseCommonFolder_BeforeFirstUse_RedirectsFiles() => WithUnusedPaths(() =>
    {
        EDAppearance.UseCommonFolder(E2EFolder);

        Assert.Equal(Path.Combine(E2EFolder, "theme.json"), MlineCommonPaths.File("theme.json"));
    });

    [Fact]
    public void UseCommonFolder_AfterFirstUse_Throws() => WithUnusedPaths(() =>
    {
        _ = MlineCommonPaths.File("theme.json");

        // Úložiště tématu a písma by zůstala u vývojáře, přesunul by se jen log chyb.
        Assert.Throws<InvalidOperationException>(() => EDAppearance.UseCommonFolder(E2EFolder));
        Assert.Null(MlineCommonPaths.Redirected);
    });

    // Statické úložiště tématu a písma mohly cestu v procesu testů vzít už dřív.
    private static void WithUnusedPaths(Action test)
    {
        var (folder, handedOut) = (MlineCommonPaths.Redirected, MlineCommonPaths.HandedOut);
        MlineCommonPaths.Restore(redirected: null, handedOut: false);
        try { test(); }
        finally { MlineCommonPaths.Restore(folder, handedOut); }
    }
}
