using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class FilterAndSetParameterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private string _category;
        private string _filterParam;
        private string _filterValue;
        private string _setParam;
        private string _setValue;
        private bool _dryRun;
        public FilterAndSetResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(string category, string filterParam, string filterValue, string setParam, string setValue, bool dryRun)
        {
            _category = category;
            _filterParam = filterParam;
            _filterValue = filterValue;
            _setParam = setParam;
            _setValue = setValue;
            _dryRun = dryRun;
            TaskCompleted = false;
            _resetEvent.Reset();
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

                if (!string.IsNullOrEmpty(_category))
                {
                    var builtInCategory = GetCategoryByName(_category);
                    if (builtInCategory != BuiltInCategory.INVALID)
                        collector.OfCategory(builtInCategory);
                }

                var matchedElements = new List<Element>();
                foreach (Element e in collector)
                {
                    Parameter p = e.LookupParameter(_filterParam);
                    if (p != null && p.AsValueString() != null &&
                        p.AsValueString().IndexOf(_filterValue, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedElements.Add(e);
                    }
                }

                if (_dryRun)
                {
                    ResultInfo = new FilterAndSetResult
                    {
                        Success = true,
                        DryRun = true,
                        MatchedCount = matchedElements.Count,
                        MatchedIds = matchedElements.Select(e =>
#if REVIT2024_OR_GREATER
                            e.Id.Value
#else
                            (long)e.Id.IntegerValue
#endif
                        ).ToList(),
                        Message = $"Dry run: Found {matchedElements.Count} matching elements. No changes made."
                    };
                    return;
                }

                int successCount = 0;
                int failCount = 0;
                var details = new List<string>();

                using (Transaction tx = new Transaction(doc, "Filter and Set Parameter"))
                {
                    tx.Start();
                    foreach (var element in matchedElements)
                    {
                        Parameter p = element.LookupParameter(_setParam);
                        if (p == null || p.IsReadOnly)
                        {
                            failCount++;
                            details.Add($"ID {element.Id}: Parameter {_setParam} not found or read-only");
                            continue;
                        }

                        if (SetParamValue(p, _setValue)) successCount++;
                        else failCount++;
                    }
                    tx.Commit();
                }

                ResultInfo = new FilterAndSetResult
                {
                    Success = true,
                    DryRun = false,
                    MatchedCount = matchedElements.Count,
                    UpdatedCount = successCount,
                    FailedCount = failCount,
                    Details = details,
                    Message = $"Updated {successCount} elements. Failed: {failCount}."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new FilterAndSetResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        private BuiltInCategory GetCategoryByName(string name)
        {
            if (Enum.TryParse(name, true, out BuiltInCategory cat)) return cat;
            return BuiltInCategory.INVALID;
        }

        private bool SetParamValue(Parameter param, string valueStr)
        {
            try
            {
                if (param.StorageType == StorageType.String) return param.Set(valueStr);
                if (param.StorageType == StorageType.Integer)
                {
                    if (int.TryParse(valueStr, out int iVal)) return param.Set(iVal);
                    if (bool.TryParse(valueStr, out bool bVal)) return param.Set(bVal ? 1 : 0);
                }
                if (param.StorageType == StorageType.Double && double.TryParse(valueStr, out double dVal)) return param.Set(dVal);
            }
            catch { }
            return false;
        }

        public string GetName() => "Filter And Set Parameter";
    }
}
