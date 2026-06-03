using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class CheckSprinklerCoverageEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _levelId;
        private double _coverageRadius; // mm

        public SprinklerCoverageResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(long levelId, double coverageRadius)
        {
            _levelId = levelId;
            _coverageRadius = coverageRadius;
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
                var lvlId = new ElementId(_levelId);
#else
                var lvlId = new ElementId((int)_levelId);
#endif

                var filter = new ElementLevelFilter(lvlId);
                var sprinklers = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Sprinklers)
                    .WhereElementIsNotElementType()
                    .WherePasses(filter)
                    .ToElements();

                if (sprinklers.Count == 0)
                {
                    ResultInfo = new SprinklerCoverageResult { Success = true, TotalSprinklers = 0, CoveredArea = 0, Message = "No sprinklers found on this level." };
                    return;
                }

                double radiusFeet = _coverageRadius / 304.8;
                
                var points = new List<XYZ>();
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                foreach (var s in sprinklers)
                {
                    XYZ pt = GetElementCenter(s);
                    if (pt != null)
                    {
                        points.Add(pt);
                        if (pt.X < minX) minX = pt.X;
                        if (pt.Y < minY) minY = pt.Y;
                        if (pt.X > maxX) maxX = pt.X;
                        if (pt.Y > maxY) maxY = pt.Y;
                    }
                }

                if (points.Count == 0)
                {
                    ResultInfo = new SprinklerCoverageResult { Success = false, Message = "Could not determine location of sprinklers." };
                    return;
                }

                // Grid-based sampling (0.5m x 0.5m = 1.64ft x 1.64ft grid)
                double gridStepFeet = 500.0 / 304.8; // 500mm
                double areaPerPointSqMeters = 0.5 * 0.5;

                // Expand bounding box by coverage radius
                minX -= radiusFeet;
                minY -= radiusFeet;
                maxX += radiusFeet;
                maxY += radiusFeet;

                int coveredPoints = 0;

                for (double x = minX; x <= maxX; x += gridStepFeet)
                {
                    for (double y = minY; y <= maxY; y += gridStepFeet)
                    {
                        XYZ testPt = new XYZ(x, y, 0);
                        foreach (var p in points)
                        {
                            // 2D distance
                            double dist = Math.Sqrt(Math.Pow(testPt.X - p.X, 2) + Math.Pow(testPt.Y - p.Y, 2));
                            if (dist <= radiusFeet)
                            {
                                coveredPoints++;
                                break;
                            }
                        }
                    }
                }

                double totalArea = coveredPoints * areaPerPointSqMeters;

                ResultInfo = new SprinklerCoverageResult
                {
                    Success = true,
                    TotalSprinklers = sprinklers.Count,
                    CoverageRadius = _coverageRadius,
                    CoveredArea = Math.Round(totalArea, 2),
                    Message = $"Analyzed coverage for {sprinklers.Count} sprinklers. Total covered area: {Math.Round(totalArea, 2)} sqm."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new SprinklerCoverageResult { Success = false, Message = $"Error: {ex.Message}" };
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

        public string GetName() => "Check Sprinkler Coverage";
    }
}
