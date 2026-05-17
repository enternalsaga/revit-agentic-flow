using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;
using System.Collections.Generic;
using System;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class VerifyElementsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private List<long> _elementIds;

        public VerifyElementsResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(List<long> elementIds)
        {
            _elementIds = elementIds;
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
                
                var existing = new List<long>();
                var missing = new List<long>();

                foreach (var id in _elementIds)
                {
#if REVIT2024_OR_GREATER
                    var eId = new ElementId(id);
#else
                    var eId = new ElementId((int)id);
#endif
                    var e = doc.GetElement(eId);
                    if (e != null && e.IsValidObject)
                    {
                        existing.Add(id);
                    }
                    else
                    {
                        missing.Add(id);
                    }
                }

                ResultInfo = new VerifyElementsResult
                {
                    Success = true,
                    ExistingIds = existing,
                    MissingIds = missing,
                    Message = $"Verified {_elementIds.Count} elements. {existing.Count} exist, {missing.Count} missing."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new VerifyElementsResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Verify Elements";
    }
}
