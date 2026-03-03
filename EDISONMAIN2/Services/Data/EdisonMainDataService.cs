using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ALVA.EDISONAPI;
using ALWA.SYS;
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
            object[] treeData = mlRest.Query("ALVA.EDISONMAIN2.API.Tree", "GetTree", Array.Empty<object>());
            mlRest.RestStatus.ThrowIfError();
            return ParseTree(treeData, mlRest.Delimiter);
        }

        private static ObservableCollection<IModule> ParseTree(object[] treeData, char delimiter)
        {
            var result = new ObservableCollection<IModule>();
            var familyStack = new Stack<ObservableCollection<IModule>>();
            ObservableCollection<IModule> currentList = result;

            for (int i = 1; i < treeData.Length; i++)
            {
                string[] parts = treeData[i]?.ToString()?.Split(delimiter) ?? [];
                switch (parts[0])
                {
                    case "FAMILY":
                        var family = new ModuleFamily { DisplayedName = parts[1], IsExpanded = parts[2] == "1" };
                        currentList.Add(family);
                        familyStack.Push(currentList);
                        currentList = family.NestedModules;
                        break;
                    case "EXE":
                        currentList.Add(new ExecutableModule { DisplayedName = parts[1], ExecutablePath = parts[2], FormName = parts[3] });
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
