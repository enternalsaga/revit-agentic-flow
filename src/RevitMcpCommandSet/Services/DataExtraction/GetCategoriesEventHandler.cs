using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetCategoriesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private bool _includeEmpty;

        public GetCategoriesResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(bool includeEmpty = false)
        {
            _includeEmpty = includeEmpty;
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
                var categoryCounts = new Dictionary<string, CategoryCountModel>();
                int totalElements = 0;

                // Collect all elements and group by category
                var elements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElements();

                foreach (Element elem in elements)
                {
                    if (elem.Category == null) continue;

                    string catName = elem.Category.Name;
                    string builtIn = elem.Category.BuiltInCategory.ToString();

                    if (!categoryCounts.ContainsKey(catName))
                    {
                        categoryCounts[catName] = new CategoryCountModel
                        {
                            CategoryName = catName,
                            BuiltInCategory = builtIn,
                            ElementCount = 0
                        };
                    }

                    categoryCounts[catName].ElementCount++;
                    totalElements++;
                }

                var categories = categoryCounts.Values
                    .Where(c => _includeEmpty || c.ElementCount > 0)
                    .OrderByDescending(c => c.ElementCount)
                    .ToList();

                ResultInfo = new GetCategoriesResult
                {
                    TotalCategories = categories.Count,
                    TotalElements = totalElements,
                    Categories = categories,
                    Success = true,
                    Message = $"Found {categories.Count} categories with {totalElements} total elements"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetCategoriesResult
                {
                    Success = false,
                    Message = $"Error getting categories: {ex.Message}"
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName() => "Get Categories";
    }
}
