using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class MeasureDistanceEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long? _id1;
        private long? _id2;
        public MeasureDistanceResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(long? id1, long? id2)
        {
            _id1 = id1;
            _id2 = id2;
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

                Element e1 = null, e2 = null;

                if (_id1.HasValue && _id2.HasValue)
                {
#if REVIT2024_OR_GREATER
                    e1 = doc.GetElement(new ElementId(_id1.Value));
                    e2 = doc.GetElement(new ElementId(_id2.Value));
#else
                    e1 = doc.GetElement(new ElementId((int)_id1.Value));
                    e2 = doc.GetElement(new ElementId((int)_id2.Value));
#endif
                }
                else
                {
                    var selectedIds = uiDoc.Selection.GetElementIds().ToList();
                    if (selectedIds.Count >= 2)
                    {
                        e1 = doc.GetElement(selectedIds[0]);
                        e2 = doc.GetElement(selectedIds[1]);
                    }
                }

                if (e1 == null || e2 == null)
                {
                    ResultInfo = new MeasureDistanceResult { Success = false, Message = "Two elements must be provided or selected." };
                    return;
                }

                XYZ p1 = GetElementCenter(e1);
                XYZ p2 = GetElementCenter(e2);

                if (p1 == null || p2 == null)
                {
                    ResultInfo = new MeasureDistanceResult { Success = false, Message = "Could not determine location for one or both elements." };
                    return;
                }

                double dist3D = p1.DistanceTo(p2) * 304.8;
                XYZ p2Proj = new XYZ(p2.X, p2.Y, p1.Z);
                double dist2D = p1.DistanceTo(p2Proj) * 304.8;

                ResultInfo = new MeasureDistanceResult
                {
                    Success = true,
                    Distance3D = Math.Round(dist3D, 2),
                    Distance2D = Math.Round(dist2D, 2),
#if REVIT2024_OR_GREATER
                    ElementId1 = e1.Id.Value,
                    ElementId2 = e2.Id.Value,
#else
                    ElementId1 = (long)e1.Id.IntegerValue,
                    ElementId2 = (long)e2.Id.IntegerValue,
#endif
                    Message = $"Measured distance between element {e1.Id} and {e2.Id}."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new MeasureDistanceResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        private XYZ GetElementCenter(Element e)
        {
            if (e.Location is LocationPoint lp) return lp.Point;
            if (e.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
            var bbox = e.get_BoundingBox(null);
            if (bbox != null) return (bbox.Min + bbox.Max) / 2.0;
            return null;
        }

        public string GetName() => "Measure Distance";
    }
}
