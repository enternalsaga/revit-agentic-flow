using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class AssignParameterBetweenLevelsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _lowerLevelId;
        private long _upperLevelId;
        private string _parameterName;
        private string _valueStr;
        private string _category;
        private bool _dryRun;

        public AssignBetweenLevelsResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(long lowerLevelId, long upperLevelId, string parameterName, string valueStr, string category, bool dryRun)
        {
            _lowerLevelId = lowerLevelId;
            _upperLevelId = upperLevelId;
            _parameterName = parameterName;
            _valueStr = valueStr;
            _category = category;
            _dryRun = dryRun;
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
                var llId = new ElementId(_lowerLevelId);
                var ulId = new ElementId(_upperLevelId);
#else
                var llId = new ElementId((int)_lowerLevelId);
                var ulId = new ElementId((int)_upperLevelId);
#endif

                var lowerLevel = doc.GetElement(llId) as Level;
                var upperLevel = doc.GetElement(ulId) as Level;

                if (lowerLevel == null || upperLevel == null)
                {
                    ResultInfo = new AssignBetweenLevelsResult { Success = false, Message = "Levels not found" };
                    return;
                }

                double minZ = Math.Min(lowerLevel.Elevation, upperLevel.Elevation);
                double maxZ = Math.Max(lowerLevel.Elevation, upperLevel.Elevation);

                // Create bounding box for entire model XY, limited to Z
                BoundingBoxXYZ bbox = new BoundingBoxXYZ
                {
                    Min = new XYZ(-100000, -100000, minZ),
                    Max = new XYZ(100000, 100000, maxZ)
                };

                Outline outline = new Outline(bbox.Min, bbox.Max);
                BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);

                var collector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(filter);

                if (!string.IsNullOrEmpty(_category))
                {
                    if (Enum.TryParse(_category, true, out BuiltInCategory cat))
                        collector.OfCategory(cat);
                }

                var elements = collector.ToElements();

                if (_dryRun)
                {
                    ResultInfo = new AssignBetweenLevelsResult
                    {
                        Success = true, DryRun = true, LowerLevelId = _lowerLevelId, UpperLevelId = _upperLevelId,
                        UpdatedCount = elements.Count, Message = $"Dry run: found {elements.Count} elements between levels."
                    };
                    return;
                }

                int successCount = 0;
                int failCount = 0;

                using (Transaction tx = new Transaction(doc, "Assign Params Between Levels"))
                {
                    tx.Start();
                    foreach (var e in elements)
                    {
                        Parameter p = e.LookupParameter(_parameterName);
                        if (p == null || p.IsReadOnly) { failCount++; continue; }

                        if (SetParamValue(p, _valueStr)) successCount++;
                        else failCount++;
                    }
                    tx.Commit();
                }

                ResultInfo = new AssignBetweenLevelsResult
                {
                    Success = true, DryRun = false,
                    LowerLevelId = _lowerLevelId, UpperLevelId = _upperLevelId,
                    UpdatedCount = successCount, FailedCount = failCount,
                    Message = $"Updated {successCount} elements between levels. Failed: {failCount}."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new AssignBetweenLevelsResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        private bool SetParamValue(Parameter param, string valueStr)
        {
            try
            {
                if (param.StorageType == StorageType.String) return param.Set(valueStr);
                if (param.StorageType == StorageType.Integer)
                {
                    if (int.TryParse(valueStr, out int iVal)) return param.Set(iVal);
                    if (bool.TryParse(valueStr, out bool bVal)) return param.Set(bVal ? 1 : 0);
                }
                if (param.StorageType == StorageType.Double && double.TryParse(valueStr, out double dVal)) return param.Set(dVal);
            }
            catch { }
            return false;
        }

        public string GetName() => "Assign Parameter Between Levels";
    }
}
