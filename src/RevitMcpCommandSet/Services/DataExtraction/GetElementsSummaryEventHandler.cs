using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetElementsSummaryEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private string _category;
        private string _parameterName;
        private string _aggregation;

        public GetElementsSummaryResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(string category, string parameterName = null, string aggregation = "count")
        {
            _category = category;
            _parameterName = parameterName;
            _aggregation = aggregation;
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

                var elements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Where(e => e.Category != null &&
                           e.Category.Name.Equals(_category, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var groups = new Dictionary<string, List<double?>>();

                foreach (Element elem in elements)
                {
                    string groupKey;
                    double? numericValue = null;

                    if (!string.IsNullOrEmpty(_parameterName))
                    {
                        Parameter param = elem.LookupParameter(_parameterName);
                        if (param != null)
                        {
                            // Get group key from type name
                            groupKey = elem.Name ?? "Unknown";

                            // Get numeric value for aggregation
                            if (param.StorageType == StorageType.Double)
                                numericValue = param.AsDouble();
                            else if (param.StorageType == StorageType.Integer)
                                numericValue = param.AsInteger();
                            else
                                groupKey = param.AsValueString() ?? param.AsString() ?? "Unknown";
                        }
                        else
                        {
                            groupKey = elem.Name ?? "Unknown";
                        }
                    }
                    else
                    {
                        groupKey = elem.Name ?? "Unknown";
                    }

                    if (!groups.ContainsKey(groupKey))
                        groups[groupKey] = new List<double?>();

                    groups[groupKey].Add(numericValue);
                }

                var summaryGroups = new List<SummaryGroupModel>();
                double? grandTotal = null;

                foreach (var kvp in groups.OrderByDescending(g => g.Value.Count))
                {
                    var group = new SummaryGroupModel
                    {
                        GroupName = kvp.Key,
                        Count = kvp.Value.Count
                    };

                    var numericValues = kvp.Value.Where(v => v.HasValue).Select(v => v.Value).ToList();
                    if (numericValues.Any())
                    {
                        group.Sum = numericValues.Sum();
                        group.Average = numericValues.Average();
                        group.Min = numericValues.Min();
                        group.Max = numericValues.Max();

                        grandTotal = (grandTotal ?? 0) + group.Sum.Value;
                    }

                    summaryGroups.Add(group);
                }

                ResultInfo = new GetElementsSummaryResult
                {
                    Category = _category,
                    TotalElements = elements.Count,
                    Groups = summaryGroups,
                    GrandTotal = grandTotal,
                    Success = true,
                    Message = $"Summarized {elements.Count} {_category} elements into {summaryGroups.Count} groups"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetElementsSummaryResult
                {
                    Category = _category,
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

        public string GetName() => "Get Elements Summary";
    }
}
