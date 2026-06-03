using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class CopyParametersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _sourceId;
        private List<long> _targetIds;
        private List<string> _paramNames;
        private bool _overwrite;
        public CopyParametersResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(long sourceId, List<long> targetIds, List<string> paramNames, bool overwrite)
        {
            _sourceId = sourceId;
            _targetIds = targetIds;
            _paramNames = paramNames;
            _overwrite = overwrite;
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
                var srcElemId = new ElementId(_sourceId);
#else
                var srcElemId = new ElementId((int)_sourceId);
#endif
                var sourceElement = doc.GetElement(srcElemId);

                if (sourceElement == null)
                {
                    ResultInfo = new CopyParametersResult { Success = false, Message = "Source element not found" };
                    return;
                }

                int successCount = 0;
                int failCount = 0;
                var copiedParams = new HashSet<string>();

                using (Transaction tx = new Transaction(doc, "Copy Parameters"))
                {
                    tx.Start();

                    foreach (var targetId in _targetIds)
                    {
#if REVIT2024_OR_GREATER
                        var tgtElemId = new ElementId(targetId);
#else
                        var tgtElemId = new ElementId((int)targetId);
#endif
                        var targetElement = doc.GetElement(tgtElemId);
                        if (targetElement == null) { failCount++; continue; }

                        bool targetUpdated = false;

                        foreach (Parameter srcParam in sourceElement.Parameters)
                        {
                            if (_paramNames != null && _paramNames.Count > 0 && !_paramNames.Contains(srcParam.Definition.Name))
                                continue; // Skip if not in explicit list

                            Parameter tgtParam = targetElement.LookupParameter(srcParam.Definition.Name);
                            if (tgtParam != null && !tgtParam.IsReadOnly)
                            {
                                if (!_overwrite && tgtParam.HasValue) continue;

                                bool copied = false;
                                switch (srcParam.StorageType)
                                {
                                    case StorageType.String: copied = tgtParam.Set(srcParam.AsString()); break;
                                    case StorageType.Integer: copied = tgtParam.Set(srcParam.AsInteger()); break;
                                    case StorageType.Double: copied = tgtParam.Set(srcParam.AsDouble()); break;
                                    case StorageType.ElementId: copied = tgtParam.Set(srcParam.AsElementId()); break;
                                }

                                if (copied)
                                {
                                    copiedParams.Add(srcParam.Definition.Name);
                                    targetUpdated = true;
                                }
                            }
                        }

                        if (targetUpdated) successCount++;
                        else failCount++;
                    }

                    tx.Commit();
                }

                ResultInfo = new CopyParametersResult
                {
                    Success = true,
                    SourceId = _sourceId,
                    TargetIds = _targetIds,
                    CopiedParameters = copiedParams.ToList(),
                    UpdatedCount = successCount,
                    FailedCount = failCount,
                    Message = $"Copied parameters to {successCount} elements"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new CopyParametersResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Copy Parameters";
    }
}
