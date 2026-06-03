using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetElementsParameterValuesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private string _category;
        private string _parameterName;
        private int _limit;

        public GetElementsParameterValuesResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(string category, string parameterName, int limit = 500)
        {
            _category = category;
            _parameterName = parameterName;
            _limit = limit;
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
                var elementValues = new List<ElementParameterValueModel>();

                // Find the built-in category matching the name
                var elements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Where(e => e.Category != null &&
                           e.Category.Name.Equals(_category, StringComparison.OrdinalIgnoreCase))
                    .Take(_limit);

                foreach (Element elem in elements)
                {
                    Parameter param = elem.LookupParameter(_parameterName);
                    if (param == null) continue;

                    object value = null;
                    switch (param.StorageType)
                    {
                        case StorageType.String:
                            value = param.AsString() ?? "";
                            break;
                        case StorageType.Integer:
                            value = param.AsInteger();
                            break;
                        case StorageType.Double:
                            value = param.AsDouble();
                            break;
                        case StorageType.ElementId:
                            var refElem = doc.GetElement(param.AsElementId());
                            value = refElem?.Name ?? param.AsElementId().ToString();
                            break;
                        default:
                            value = param.AsValueString() ?? "";
                            break;
                    }

                    elementValues.Add(new ElementParameterValueModel
                    {
#if REVIT2024_OR_GREATER
                        ElementId = elem.Id.Value,
#else
                        ElementId = elem.Id.IntegerValue,
#endif
                        TypeName = elem.Name ?? "",
                        Value = value
                    });
                }

                ResultInfo = new GetElementsParameterValuesResult
                {
                    Category = _category,
                    ParameterName = _parameterName,
                    TotalElements = elementValues.Count,
                    Elements = elementValues,
                    Success = true,
                    Message = $"Extracted '{_parameterName}' from {elementValues.Count} {_category} elements"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetElementsParameterValuesResult
                {
                    Category = _category,
                    ParameterName = _parameterName,
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName() => "Get Elements Parameter Values";
    }
}
