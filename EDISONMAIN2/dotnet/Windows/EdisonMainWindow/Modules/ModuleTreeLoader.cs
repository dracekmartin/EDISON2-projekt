using System.Collections.Generic;
using System.Collections.ObjectModel;
using ALWA.SYS;
using EDControls;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public class ModuleTreeLoader : IModuleTreeLoader
    {
        public IReadOnlyList<IModule> GetModuleTree(IMLRest mlRest, string treeName = "main")
        {
            var input = new CVar();
            input["tree"] = new CVar(treeName);
            CVar treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", input);
            return ParseTree(treeData);
        }

        /// <summary>
        /// Wire format (z ALVA.EDISONMAIN2.API.Tree.GetTree):
        ///   $lb("FAMILY", displayName, isExpanded, elementId, isLicensed, mlineOnly)
        ///   $lb("EXE", displayName, executablePath, formName, elementId, isLicensed, inputParam, hidden, color, mlineOnly, longName)
        ///   $lb("FAMILY_END")
        /// </summary>
        internal static ObservableCollection<IModule> ParseTree(CVar treeData)
        {
            var result = new ObservableCollection<IModule>();
            var familyStack = new Stack<ObservableCollection<IModule>>();
            var familyNames = new List<string>();
            ObservableCollection<IModule> currentList = result;

            foreach (KeyValuePair<string, CVar> kvp in treeData)
            {
                if (kvp.Key == "ERR") continue;
                CList row = kvp.Value;
                switch (row.GetString(1))
                {
                    case "FAMILY":
                        var family = new ModuleFamily {
                            Name          = row.GetString(2),
                            IsExpanded    = row.GetString(3) == "1",
                            ElementId     = row.Count >= 4 ? row.GetString(4) : "",
                            IsLicensed    = row.Count >= 5 && row.GetString(5) == "1",
                            MlineOnly     = row.Count >= 6 && row.GetString(6) == "1"
                        };
                        currentList.Add(family);
                        familyStack.Push(currentList);
                        familyNames.Add(family.Name);
                        currentList = family.NestedModules;
                        break;
                    case "EXE":
                        currentList.Add(new ExecutableModule {
                            Name           = row.GetString(2),
                            ExecutablePath = row.GetString(3),
                            FormName       = row.GetString(4),
                            ElementId      = row.Count >= 5 ? row.GetString(5) : "",
                            IsLicensed     = row.Count >= 6 && row.GetString(6) == "1",
                            InputParam     = row.Count >= 7 ? row.GetString(7) : "",
                            Hidden         = row.Count >= 8 && row.GetString(8) == "1",
                            Color          = row.Count >= 9 ? row.GetString(9) : "",
                            MlineOnly      = row.Count >= 10 && row.GetString(10) == "1",
                            LongName       = row.Count >= 11 ? row.GetString(11) : "",
                            FamilyPath     = string.Join(ExecutableModule.PathSeparator, familyNames)
                        });
                        break;
                    case "FAMILY_END":
                        if (familyStack.Count > 0)
                        {
                            currentList = familyStack.Pop();
                            familyNames.RemoveAt(familyNames.Count - 1);
                        }
                        break;
                }
            }

            return result;
        }
    }
}
