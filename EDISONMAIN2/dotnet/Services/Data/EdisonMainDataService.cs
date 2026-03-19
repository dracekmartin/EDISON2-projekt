using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ALVA.EDISONAPI;
using ALWA.SYS;
using EDControls;
using EDISONMAIN2.Services.Modules;
using EDISONMAIN2.Services;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Services.Data
{
    public class EdisonMainDataService(ConnectionParams connParams) : IEdisonMainDataService
    {
        private readonly ConnectionParams _connParams = connParams;

        // MLRest instances are created once and reused. Two are needed because
        // GetCompanies uses an empty namespace context while GetModuleTree needs
        // the specific company namespace — MLRest binds these at construction time.
        private MLRest? _emptyMLRest;
        private MLRest? _customerSpecificMLRest;

        private MLRest CreateMLRest(string database, string idfa) =>
            new(_connParams.Server, _connParams.RestPort, database, idfa)
            {
                LoginName = _connParams.UserName,
                FullName = _connParams.FullName,
                Mlp = _connParams.Mlp.ToString()
            };

        private MLRest EmptyRest =>
            _emptyMLRest ??= CreateMLRest(database: "", idfa: "");

        private MLRest GetTreeRest(string database, string idfa)
        {
            if (_customerSpecificMLRest == null || _customerSpecificMLRest.DataNamespace != database || _customerSpecificMLRest.Idfa != idfa)
            {
                _customerSpecificMLRest = CreateMLRest(database, idfa);
            }
            return _customerSpecificMLRest;
        }

        public IReadOnlyList<CompanyStruct> GetCompanies()
        {
            string errState = "";
            var companies = GetCompanyList(ref errState, EmptyRest, false);
            if (!string.IsNullOrEmpty(errState))
                throw new InvalidOperationException(errState);
            return companies;
        }

        public IReadOnlyList<IModule> GetModuleTree(string database, string idfa)
        {
            var mlRest = GetTreeRest(database, idfa);
            CVar treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", new CVar());
            return ParseTree(treeData);
        }

        /// <summary>
        /// Parses the module tree from Caché output.
        /// Caché writes ^||ML("OUT","i") = $lb(type, ...) where type is:
        ///   "FAMILY"     → $lb("FAMILY", displayName, isExpanded)
        ///   "EXE"        → $lb("EXE", displayName, executablePath, formName)
        ///   "FAMILY_END" → $lb("FAMILY_END")
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
                            FormName       = row.GetString(4)
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
