using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class PurgeAnalysisEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private bool _analyzeOnly;

        public PurgeAnalysisResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(bool analyzeOnly)
        {
            _analyzeOnly = analyzeOnly;
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
                
                // Get all views
                var allViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .ToList();

                // Check View Templates
                var allTemplates = allViews.Where(v => v.IsTemplate).ToList();
                var usedTemplateIds = new HashSet<ElementId>(
                    allViews.Where(v => !v.IsTemplate && v.ViewTemplateId != ElementId.InvalidElementId)
                            .Select(v => v.ViewTemplateId)
                );

                var unusedTemplates = allTemplates.Where(t => !usedTemplateIds.Contains(t.Id)).ToList();

                // Check Filters
                var allFilters = new FilteredElementCollector(doc)
                    .OfClass(typeof(ParameterFilterElement))
                    .Cast<ParameterFilterElement>()
                    .ToList();

                var usedFilterIds = new HashSet<ElementId>();
                foreach (var view in allViews)
                {
                    if (view.AreGraphicsOverridesAllowed())
                    {
                        var filters = view.GetFilters();
                        foreach (var f in filters) usedFilterIds.Add(f);
                    }
                }

                var unusedFilters = allFilters.Where(f => !usedFilterIds.Contains(f.Id)).ToList();

                if (!_analyzeOnly)
                {
                    using (Transaction tx = new Transaction(doc, "Purge Analysis"))
                    {
                        tx.Start();
                        foreach (var t in unusedTemplates) doc.Delete(t.Id);
                        foreach (var f in unusedFilters) doc.Delete(f.Id);
                        tx.Commit();
                    }
                }

                ResultInfo = new PurgeAnalysisResult
                {
                    Success = true,
                    Deleted = !_analyzeOnly,
                    UnusedViewTemplates = unusedTemplates.Select(t => t.Name).ToList(),
                    UnusedFilters = unusedFilters.Select(f => f.Name).ToList(),
                    Message = $"Found {unusedTemplates.Count} unused View Templates and {unusedFilters.Count} unused Filters."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new PurgeAnalysisResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Purge Analysis";
    }
}
