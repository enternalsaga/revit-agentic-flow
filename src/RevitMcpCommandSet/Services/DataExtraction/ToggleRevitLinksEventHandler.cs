using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;
using System.Collections.Generic;
using System;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class ToggleRevitLinksEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private bool _visible;

        public ToggleRevitLinksResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(bool visible)
        {
            _visible = visible;
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
                var view = app.ActiveUIDocument.ActiveView;

                var links = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_RvtLinks)
                    .WhereElementIsNotElementType()
                    .ToElements();

                int toggled = 0;

                using (Transaction tx = new Transaction(doc, "Toggle Revit Links"))
                {
                    tx.Start();

                    foreach (var link in links)
                    {
                        if (link.IsHidden(view) == _visible)
                        {
                            if (_visible)
                            {
                                view.UnhideElements(new List<ElementId> { link.Id });
                            }
                            else
                            {
                                view.HideElements(new List<ElementId> { link.Id });
                            }
                            toggled++;
                        }
                    }

                    tx.Commit();
                }

                ResultInfo = new ToggleRevitLinksResult
                {
                    Success = true,
                    LinksToggled = toggled,
                    Message = $"Successfully {(_visible ? "showed" : "hid")} {toggled} Revit links in the current view."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new ToggleRevitLinksResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Toggle Revit Links";
    }
}
