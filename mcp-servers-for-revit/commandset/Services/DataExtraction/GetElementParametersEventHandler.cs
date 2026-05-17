using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetElementParametersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _elementId;
        private bool _includeReadOnly;

        public GetElementParametersResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(long elementId, bool includeReadOnly = true)
        {
            _elementId = elementId;
            _includeReadOnly = includeReadOnly;
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

#if REVIT2024_OR_GREATER
                var elemId = new ElementId((long)_elementId);
#else
                var elemId = new ElementId((int)_elementId);
#endif
                var element = doc.GetElement(elemId);

                if (element == null)
                {
                    ResultInfo = new GetElementParametersResult
                    {
                        ElementId = _elementId,
                        Success = false,
                        Message = $"Element with ID {_elementId} not found"
                    };
                    return;
                }

                var paramList = new List<ParameterModel>();

                foreach (Parameter param in element.Parameters)
                {
                    if (!_includeReadOnly && param.IsReadOnly) continue;

                    var paramModel = new ParameterModel
                    {
                        Name = param.Definition?.Name ?? "Unknown",
                        StorageType = param.StorageType.ToString(),
                        IsReadOnly = param.IsReadOnly,
                        Group = param.Definition?.GetGroupTypeId()?.ToString() ?? "",
                        IsShared = param.IsShared
                    };

                    // Extract value based on storage type
                    switch (param.StorageType)
                    {
                        case StorageType.String:
                            paramModel.Value = param.AsString() ?? "";
                            break;
                        case StorageType.Integer:
                            paramModel.Value = param.AsInteger();
                            break;
                        case StorageType.Double:
                            paramModel.Value = param.AsDouble();
                            break;
                        case StorageType.ElementId:
                            var refId = param.AsElementId();
                            var refElem = doc.GetElement(refId);
                            paramModel.Value = refElem?.Name ?? refId.ToString();
                            break;
                        default:
                            paramModel.Value = param.AsValueString() ?? "";
                            break;
                    }

                    paramList.Add(paramModel);
                }

                ResultInfo = new GetElementParametersResult
                {
                    ElementId = _elementId,
                    ElementCategory = element.Category?.Name ?? "Unknown",
                    ElementTypeName = element.Name ?? "",
                    Parameters = paramList.OrderBy(p => p.Name).ToList(),
                    Success = true,
                    Message = $"Found {paramList.Count} parameters"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetElementParametersResult
                {
                    ElementId = _elementId,
                    Success = false,
                    Message = $"Error getting element parameters: {ex.Message}"
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName() => "Get Element Parameters";
    }
}
