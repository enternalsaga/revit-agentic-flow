using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class FindAndSelectEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private string _category;
        private string _parameterName;
        private string _parameterValue;
        private bool _isolate;
        public FindAndSelectResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(string category, string parameterName, string parameterValue, bool isolate)
        {
            _category = category;
            _parameterName = parameterName;
            _parameterValue = parameterValue;
            _isolate = isolate;
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
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc.Document;
                var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

                if (!string.IsNullOrEmpty(_category))
                {
                    if (Enum.TryParse(_category, true, out BuiltInCategory cat))
                        collector.OfCategory(cat);
                }

                var matchedIds = new List<ElementId>();
                foreach (Element e in collector)
                {
                    if (string.IsNullOrEmpty(_parameterName) || string.IsNullOrEmpty(_parameterValue))
                    {
                        matchedIds.Add(e.Id);
                    }
                    else
                    {
                        Parameter p = e.LookupParameter(_parameterName);
                        if (p != null && p.AsValueString() != null &&
                            p.AsValueString().IndexOf(_parameterValue, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedIds.Add(e.Id);
                        }
                    }
                }

                uiDoc.Selection.SetElementIds(matchedIds);

                if (_isolate && matchedIds.Count > 0)
                {
                    using (Transaction tx = new Transaction(doc, "Isolate Elements"))
                    {
                        tx.Start();
                        doc.ActiveView.IsolateElementsTemporary(matchedIds);
                        tx.Commit();
                    }
                }

                ResultInfo = new FindAndSelectResult
                {
                    Success = true,
                    Count = matchedIds.Count,
                    Isolated = _isolate,
                    Message = $"Found and selected {matchedIds.Count} elements."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new FindAndSelectResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Find And Select";
    }
}
