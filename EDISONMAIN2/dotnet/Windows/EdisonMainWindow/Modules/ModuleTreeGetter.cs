using System.Collections.Generic;
using System.Collections.ObjectModel;
using ALWA.SYS;
using EDControls;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public class ModuleTreeGetter : IModuleTreeGetter
    {
        public IReadOnlyList<IModule> GetModuleTree(IMLRest mlRest)
        {
            CVar treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", new CVar());
            return ParseTree(treeData);
        }

        /// <summary>
        /// Wire format (z ALVA.EDISONMAIN2.API.Tree.GetTree):
        ///   $lb("FAMILY", displayName, isExpanded)
        ///   $lb("EXE", displayName, executablePath, formName, elementId, isLicensed)
        ///   $lb("FAMILY_END")
        /// </summary>
        internal static ObservableCollection<IModule> ParseTree(CVar treeData)
        {
            var result = new ObservableCollection<IModule>();
            var familyStack = new Stack<ObservableCollection<IModule>>();
            ObservableCollection<IModule> currentList = result;

            foreach (KeyValuePair<string, CVar> kvp in treeData)
            {
                if (kvp.Key == "ERR") continue;
                CList row = kvp.Value;
                switch (row.GetString(1))
                {
                    case "FAMILY":
                        var family = new ModuleFamily {
                            DisplayedName = row.GetString(2),
                            IsExpanded    = row.GetString(3) == "1"
                        };
                        currentList.Add(family);
                        familyStack.Push(currentList);
                        currentList = family.NestedModules;
                        break;
                    case "EXE":
                        currentList.Add(new ExecutableModule {
                            DisplayedName  = row.GetString(2),
                            ExecutablePath = row.GetString(3),
                            FormName       = row.GetString(4),
                            ElementId      = row.Count >= 5 ? row.GetString(5) : "",
                            IsLicensed     = row.Count >= 6 && row.GetString(6) == "1"
                        });
                        break;
                    case "FAMILY_END":
                        if (familyStack.Count > 0) currentList = familyStack.Pop();
                        break;
                }
            }

            return result;
        }
    }
}
