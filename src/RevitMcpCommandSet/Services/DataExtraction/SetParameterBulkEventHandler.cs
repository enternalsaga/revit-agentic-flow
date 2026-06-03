using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class SetParameterBulkEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private List<long> _elementIds;
        private string _parameterName;
        private string _valueStr;
        public ParameterOperationResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(List<long> elementIds, string parameterName, string valueStr)
        {
            _elementIds = elementIds;
            _parameterName = parameterName;
            _valueStr = valueStr;
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
                int successCount = 0;
                int failCount = 0;
                var details = new List<string>();

                using (Transaction tx = new Transaction(doc, "Bulk Set Parameter"))
                {
                    tx.Start();

                    foreach (var id in _elementIds)
                    {
#if REVIT2024_OR_GREATER
                        var elemId = new ElementId(id);
#else
                        var elemId = new ElementId((int)id);
#endif
                        var element = doc.GetElement(elemId);
                        if (element == null)
                        {
                            failCount++;
                            details.Add($"ID {id}: Not found");
                            continue;
                        }

                        Parameter param = element.LookupParameter(_parameterName);
                        if (param == null || param.IsReadOnly)
                        {
                            failCount++;
                            details.Add($"ID {id}: Parameter not found or read-only");
                            continue;
                        }

                        bool ok = SetParamValue(param, _valueStr);
                        if (ok) successCount++;
                        else
                        {
                            failCount++;
                            details.Add($"ID {id}: Failed to parse/set value");
                        }
                    }

                    tx.Commit();
                }

                ResultInfo = new ParameterOperationResult
                {
                    Success = true,
                    UpdatedCount = successCount,
                    FailedCount = failCount,
                    Details = details,
                    Message = $"Successfully updated {successCount} elements. Failed: {failCount}."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new ParameterOperationResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
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
                if (param.StorageType == StorageType.Double && double.TryParse(valueStr, out double dVal))
                    return param.Set(dVal);
                if (param.StorageType == StorageType.ElementId && long.TryParse(valueStr, out long idVal))
                {
#if REVIT2024_OR_GREATER
                    return param.Set(new ElementId(idVal));
#else
                    return param.Set(new ElementId((int)idVal));
#endif
                }
            }
            catch { }
            return false;
        }

        public string GetName() => "Set Parameter Bulk";
    }
}
