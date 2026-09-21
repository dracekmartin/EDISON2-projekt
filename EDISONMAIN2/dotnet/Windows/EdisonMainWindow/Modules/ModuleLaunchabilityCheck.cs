using System.IO;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Kontrola launchovatelnosti modulů. Tree.cls neautoruje barvu - odvozuje se zde z exe
    /// a promítá do barvy (vždy) a textového suffixu v názvu (jen <see cref="InternalDiagnostics"/>:
    /// kolegové validují stav stromu na Release buildu, u zákazníka by se suffix přetažením
    /// na plochu uložil do Caché blobu sdíleného s legacy agendami).
    ///
    /// Stavy: prázdné exe = autorský stub (oranžová); exe vyplněné ale soubor chybí = červená
    /// (rozchod, který má být vidět); existující exe = launchovatelné (generický host
    /// EDISONMAIN2API.exe spustí libovolnou agendu bez retrofitu).
    /// </summary>
    internal static class ModuleLaunchabilityCheck
    {
        // Diagnostické barvy z theme (EDTheme.Module.*); fallback na literál, když slovník není
        // mergnutý. ⚠️ Jestli slovník mergnutý je, **nejde zvenčí říct** - v jednom procesu může
        // aplikaci s theme založit kterýkoli jiný test a xUnit kolekce pouští souběžně. Test proto
        // musí porovnávat proti těmhle vlastnostem, ne proti literálu (padalo to obden).
        internal static string StubColor => ResolveThemeColor("EDTheme.Module.Stub.Color", "#FFA500");    // oranžová - záměrný stub bez exe
        internal static string BrokenColor => ResolveThemeColor("EDTheme.Module.Broken.Color", "#FF0000"); // červená - tvrdí launchovatelný, ale exe chybí

        private static string ResolveThemeColor(string key, string fallback)
            => System.Windows.Application.Current?.TryFindResource(key) is System.Windows.Media.Color c
                ? $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}"
                : fallback;

        private enum State { Launchable, Stub, MissingExe }

        /// <summary>Agregovaná kategorie uzlu (pro obarvení sbalené rodiny dle obsahu).</summary>
        private enum Category { Launchable, Stub, Broken }

        /// <summary>Rekurzivně projde uzel a obarví / olabeluje moduly dle launchovatelnosti.</summary>
        public static IModule Mark(IModule module) => MarkNode(module).node;

        private static (IModule node, Category cat) MarkNode(IModule module) => module switch
        {
            ModuleFamily family => MarkFamily(family),
            ExecutableModule exe => MarkExe(exe),
            _ => (module, Category.Launchable),
        };

        /// <summary>
        /// Rodina dědí stav z dětí, aby sbalená signalizovala obsah: jakýkoliv rozbitý potomek →
        /// červená „rozbité"; všichni potomci stuby → oranžová „stuby"; jinak (je tam něco
        /// launchovatelného) → default. Stejná priorita jako u listů (broken &gt; launchable &gt; stub).
        /// </summary>
        private static (IModule, Category) MarkFamily(ModuleFamily family)
        {
            var marked = new ModuleFamily
            {
                DisplayedName = family.DisplayedName,
                IsExpanded    = family.IsExpanded,
                ElementId     = family.ElementId,
                IsLicensed    = family.IsLicensed,
                MlineOnly     = family.MlineOnly,
            };
            bool anyBroken = false, anyLaunchable = false;
            foreach (var child in family.NestedModules)
            {
                var (markedChild, cat) = MarkNode(child);
                marked.NestedModules.Add(markedChild);
                if (cat == Category.Broken) anyBroken = true;
                else if (cat == Category.Launchable) anyLaunchable = true;
            }

            if (anyBroken)
            {
                marked.Color = BrokenColor;
                marked.DisplayedName += Suffix(" - rozbité");
                return (marked, Category.Broken);
            }
            if (anyLaunchable) return (marked, Category.Launchable);

            marked.Color = StubColor;
            marked.DisplayedName += Suffix(" - stuby");
            return (marked, Category.Stub);
        }

        private static (IModule, Category) MarkExe(ExecutableModule exe) => Evaluate(exe) switch
        {
            State.Launchable => (exe, Category.Launchable),
            State.Stub => (exe with { Color = StubColor, DisplayedName = exe.DisplayedName + Suffix(" - stub") }, Category.Stub),
            _ => (exe with { Color = BrokenColor, DisplayedName = exe.DisplayedName + Suffix(" - chybí exe") }, Category.Broken),
        };

        private static string Suffix(string text) => InternalDiagnostics.Enabled ? text : "";

        private static State Evaluate(ExecutableModule exe)
        {
            if (exe.ExecutablePath.Length == 0) return State.Stub;

            var resolved = WindowNavigator.ResolveExecutablePath(exe.ExecutablePath);
            return File.Exists(resolved) ? State.Launchable : State.MissingExe;
        }
    }
}
