using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Workspace;
using RevitMCPCommandSet.Utils;
using RevitMcpSdk;
using System;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class WorkspaceSnapshotEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private WorkspaceSnapshotRequest _request = new WorkspaceSnapshotRequest();
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public WorkspaceSnapshotResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }

        public void SetParameters(WorkspaceSnapshotRequest request)
        {
            _request = request ?? new WorkspaceSnapshotRequest();
            if (_request.ElementLimit < 0) _request.ElementLimit = 0;
            if (_request.PixelSize <= 0) _request.PixelSize = 1600;
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
                if (uiDoc == null)
                {
                    ResultInfo = new WorkspaceSnapshotResult
                    {
                        Success = false,
                        Message = "No active Revit document.",
                        TimestampUtc = DateTime.UtcNow.ToString("O")
                    };
                    return;
                }

                var doc = uiDoc.Document;
                var activeView = doc.ActiveView;

                var result = new WorkspaceSnapshotResult
                {
                    Success = true,
                    Message = "Workspace snapshot created.",
                    TimestampUtc = DateTime.UtcNow.ToString("O"),
                    ActiveView = WorkspaceSnapshotUtils.CreateViewInfo(activeView),
                    UiView = WorkspaceSnapshotUtils.CreateUiViewInfo(app, activeView)
                };

                if (_request.IncludeImage)
                {
                    result.Image = WorkspaceSnapshotUtils.ExportActiveViewImage(
                        doc,
                        activeView,
                        _request.OutputDirectory,
                        _request.PixelSize);
                }

                if (_request.IncludeVisibleElements)
                {
                    result.VisibleElements = WorkspaceSnapshotUtils.GetVisibleElements(
                        doc,
                        activeView,
                        _request.ElementLimit,
                        out var totalElementsInView);
                    result.TotalElementsInView = totalElementsInView;
                    result.ReturnedElementCount = result.VisibleElements.Count;
                }

                if (_request.IncludeSelection)
                {
                    result.Selection = WorkspaceSnapshotUtils.GetSelection(doc, activeView, uiDoc);
                }

                ResultInfo = result;
            }
            catch (Exception ex)
            {
                ResultInfo = new WorkspaceSnapshotResult
                {
                    Success = false,
                    Message = $"Workspace snapshot failed: {ex.Message}",
                    TimestampUtc = DateTime.UtcNow.ToString("O")
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "Snapshot Revit Workspace";
        }
    }
}
