using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class SwitchViewEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public string ViewName { get; set; }
        public int? ViewId { get; set; }
        public object Result { get; private set; }

        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

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

                View targetView = FindTargetView(doc);
                if (targetView == null)
                {
                    Result = new
                    {
                        Success = false,
                        Message = $"View not found. Name='{ViewName}', Id={ViewId}"
                    };
                    return;
                }

                uiDoc.ActiveView = targetView;

                Result = new
                {
                    Success = true,
                    Message = $"Switched to view: {targetView.Name}",
                    View = new
                    {
#if REVIT2024_OR_GREATER
                        Id = (int)targetView.Id.Value,
#else
                        Id = targetView.Id.IntegerValue,
#endif
                        Name = targetView.Name,
                        ViewType = targetView.ViewType.ToString()
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new
                {
                    Success = false,
                    Message = ex.Message
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private View FindTargetView(Document doc)
        {
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .ToList();

            if (ViewId.HasValue)
            {
#if REVIT2024_OR_GREATER
                var elem = doc.GetElement(new ElementId((long)ViewId.Value));
#else
                var elem = doc.GetElement(new ElementId(ViewId.Value));
#endif
                return elem as View;
            }

            if (!string.IsNullOrEmpty(ViewName))
            {
                var exact = collector.FirstOrDefault(v =>
                    v.Name.Equals(ViewName, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;

                var partial = collector.FirstOrDefault(v =>
                    v.Name.IndexOf(ViewName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (partial != null) return partial;
            }

            // Default: find {3D} view
            var default3d = collector
                .OfType<View3D>()
                .FirstOrDefault(v => v.Name.Contains("{3D}"));
            return default3d ?? collector.OfType<View3D>().FirstOrDefault();
        }

        public string GetName()
        {
            return "Switch Active View";
        }
    }
}
