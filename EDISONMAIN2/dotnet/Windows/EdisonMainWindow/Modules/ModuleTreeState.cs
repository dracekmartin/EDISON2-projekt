using System.Collections.Generic;
using System.Linq;
using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Algoritmy nad stromem modulů: licenční a přístupová filtrace, fulltext hledání a klíče
    /// rozbalených uzlů. Čisté funkce bez stavu shellu - proto testovatelné bez ViewModelu.
    /// </summary>
    internal static class ModuleTreeState
    {
        /// <summary>Per stanice konstantní (DNS doména / registry override) - stačí vyhodnotit jednou.</summary>
        internal static readonly bool IsMlineDomainStation = MlineDomainDetector.IsDomainMline();

        /// <summary>
        /// Filtrace stromu: licence (<see cref="ExecutableModule.IsLicensed"/> ∈ <see cref="Lic.Elements"/>),
        /// <see cref="ExecutableModule.Hidden"/>, mlineOnly a aplikační přístup (<c>AccessTable</c>);
        /// admin fallback řeší už <see cref="AccessLoader"/> při loadu.
        /// Gate platí i na FAMILY (package ID) - složka mimo licenci/AP/doménu skryje celou větev,
        /// parita s legacy <c>BuildTreeXML</c>; prázdná FAMILY se navíc auto-skryje.
        /// </summary>
        internal static IModule? TryFilter(IModule module, Lic? licence, AccessTable? access = null, bool isMlineDomain = false) => module switch
        {
            ExecutableModule exe => IsAllowed(exe, licence, access, isMlineDomain)
                ? (exe.MlineOnly ? exe with { DisplayedName = exe.DisplayedName + MlineDomainDetector.Suffix } : exe)
                : null,
            ModuleFamily family  => FilterFamily(family, licence, access, isMlineDomain),
            _                    => module,
        };

        internal static bool IsAllowed(ExecutableModule exe, Lic? licence, AccessTable? access = null, bool isMlineDomain = false)
        {
            if (exe.Hidden) return false;
            if (exe.MlineOnly && !isMlineDomain) return false;
            if (exe.IsLicensed && !(licence?.Elements?.Contains(exe.ElementId) ?? false))
                return false;
            // AccessTable null = load nebyl proveden (typicky testy) → pass-through (graceful degradation).
            // ElementId "" = legacy 4-field řádek bez ID → bez AP gate (parita s FAMILY větví).
            if (access != null && exe.ElementId.Length > 0 && !access.Contains(exe.ElementId))
                return false;
            return true;
        }

        internal static ModuleFamily? FilterFamily(ModuleFamily family, Lic? licence, AccessTable? access = null, bool isMlineDomain = false)
        {
            // Gate složky PŘED rekurzí - mimo licenci/AP/doménu je skrytá celá větev včetně
            // jinak povolených dětí (např. admin fallback přidá formTMList, ale bez AP
            // na package Kmenová evidence se nezobrazí - parita s legacy BuildTreeXML).
            if (family.MlineOnly && !isMlineDomain) return null;
            if (family.IsLicensed && !(licence?.Elements?.Contains(family.ElementId) ?? false))
                return null;
            // ElementId "" = syntetická složka bez gate; access null = pass-through (testy).
            if (access != null && family.ElementId.Length > 0 && !access.Contains(family.ElementId))
                return null;

            var visibleChildren = family.NestedModules
                .Select(child => TryFilter(child, licence, access, isMlineDomain))
                .Where(child => child != null)
                .ToList();

            if (visibleChildren.Count == 0) return null;

            var filtered = new ModuleFamily
            {
                DisplayedName = family.DisplayedName + (family.MlineOnly ? MlineDomainDetector.Suffix : ""),
                IsExpanded    = family.IsExpanded,
                ElementId     = family.ElementId,
                IsLicensed    = family.IsLicensed,
                MlineOnly     = family.MlineOnly,
            };
            foreach (var child in visibleChildren) filtered.NestedModules.Add(child!);
            return filtered;
        }

        /// <summary>
        /// Nastaví <see cref="IModule.IsVisibleBySearch"/> rekurzivně. Uzel je viditelný, když jeho
        /// název obsahuje <paramref name="text"/> (case + diakritika insensitive) NEBO má viditelného
        /// potomka (prázdný text = vše viditelné). Rodina nalezená podle vlastního názvu zpřístupní
        /// celý svůj obsah; rodina s nalezeným potomkem se při aktivním hledání rozbalí.
        /// Vrací, zda je modul viditelný.
        /// </summary>
        internal static bool ApplySearchToModule(IModule module, string text)
        {
            var needle = SearchNormalization.NormalizeForSearch(text);
            bool selfMatch = needle.Length == 0
                || SearchNormalization.NormalizeForSearch(module.DisplayedName).Contains(needle);

            if (module is ModuleFamily family)
            {
                // Shoda na názvu skupiny zpřístupní celý její obsah - potomci hledané slovo v názvu
                // nemají, takže by nález byl slepá ulička (skupina prázdná a spustit ji nejde).
                // Prázdný needle v rekurzi = "všechno viditelné", proto stačí předat "".
                bool wholeSubtree = selfMatch && needle.Length > 0;
                bool anyChildVisible = false;
                foreach (var child in family.NestedModules)
                    if (ApplySearchToModule(child, wholeSubtree ? "" : text)) anyChildVisible = true;
                selfMatch = selfMatch || anyChildVisible;
                if (text.Length > 0 && anyChildVisible)
                {
                    if (wholeSubtree) SetExpandedRecursive(family, true);
                    else family.IsExpanded = true;
                }
            }

            module.IsVisibleBySearch = selfMatch;
            return selfMatch;
        }

        /// <summary>
        /// První spustitelný modul v pořadí stromu, který přežil hledání (DFS). Do skryté rodiny
        /// se nesestupuje - její potomci mají <see cref="IModule.IsVisibleBySearch"/> false také.
        /// </summary>
        internal static ExecutableModule? FirstVisibleExecutable(IEnumerable<IModule> tree)
        {
            foreach (var module in tree)
            {
                if (!module.IsVisibleBySearch) continue;
                switch (module)
                {
                    case ExecutableModule exe:
                        return exe;
                    case ModuleFamily family when FirstVisibleExecutable(family.NestedModules) is { } hit:
                        return hit;
                }
            }
            return null;
        }

        // Klíč uzlu = "<tree>:<index-cesta>" (např. "main:5.2") - stabilní vůči přejmenování i suffixům
        // z ModuleLaunchabilityCheck, na rozdíl od cesty přes DisplayedName.

        internal static void SetExpandedRecursive(IModule module, bool expanded)
        {
            if (module is not ModuleFamily family) return;
            family.IsExpanded = expanded;
            foreach (var child in family.NestedModules)
                SetExpandedRecursive(child, expanded);
        }

        internal static void CollectExpanded(IList<IModule> tree, string treeName, List<string> keys)
        {
            for (int i = 0; i < tree.Count; i++)
                CollectExpandedNode(tree[i], treeName, [i], keys);
        }

        private static void CollectExpandedNode(IModule module, string treeName, List<int> path, List<string> keys)
        {
            if (module is not ModuleFamily family) return;
            if (family.IsExpanded) keys.Add(ExpansionPathKey(treeName, path));
            for (int i = 0; i < family.NestedModules.Count; i++)
                CollectExpandedNode(family.NestedModules[i], treeName, [.. path, i], keys);
        }

        internal static string ExpansionPathKey(string treeName, IReadOnlyList<int> path) =>
            treeName + ":" + string.Join(".", path);
    }
}
