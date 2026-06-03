using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using RevitMcpSdk;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace RevitMCPCommandSet.Services
{
    public class EditWallProfileHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private int _wallId;
        private JArray _profilePoints;

        public AIResult<int> Result { get; private set; }

        public void SetParameters(int wallId, JArray profilePoints)
        {
            _wallId = wallId;
            _profilePoints = profilePoints;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                
                if (_wallId <= 0)
                    throw new Exception("Missing or invalid wallId");

                Wall wall = doc.GetElement(RevitIdUtils.ToElementId(_wallId)) as Wall;
                if (wall == null)
                    throw new Exception("Wall not found");

                if (!wall.CanHaveProfileSketch())
                    throw new Exception("This wall cannot have a profile sketch");

                if (_profilePoints == null)
                    throw new Exception("Missing profilePoints");

                List<XYZ> points = new List<XYZ>();
                double ft = 304.8;
                foreach (JObject ptObj in _profilePoints)
                {
                    double x = ptObj["x"]?.Value<double>() ?? 0;
                    double y = ptObj["y"]?.Value<double>() ?? 0;
                    double z = ptObj["z"]?.Value<double>() ?? 0;
                    points.Add(new XYZ(x / ft, y / ft, z / ft));
                }

                if (points.Count < 3)
                    throw new Exception("Profile must have at least 3 points");

                List<Curve> newCurves = new List<Curve>();
                for (int i = 0; i < points.Count; i++)
                {
                    XYZ p1 = points[i];
                    XYZ p2 = points[(i + 1) % points.Count];
                    if (p1.DistanceTo(p2) > 0.01)
                        newCurves.Add(Line.CreateBound(p1, p2));
                }

                using (TransactionGroup tg = new TransactionGroup(doc, "Edit Wall Profile"))
                {
                    tg.Start();

                    Sketch sketch = null;
                    if (wall.SketchId == ElementId.InvalidElementId)
                    {
                        using (Transaction t1 = new Transaction(doc, "Create Sketch"))
                        {
                            t1.Start();
                            sketch = wall.CreateProfileSketch();
                            t1.Commit();
                        }
                    }
                    else
                    {
                        sketch = doc.GetElement(wall.SketchId) as Sketch;
                    }

                    using (SketchEditScope editScope = new SketchEditScope(doc, "Edit Wall Profile"))
                    {
                        editScope.Start(sketch.Id);
                        using (Transaction t2 = new Transaction(doc, "Modify Sketch Curves"))
                        {
                            t2.Start();
                            
                            ICollection<ElementId> oldCurves = sketch.GetAllElements();
                            foreach (var id in oldCurves) doc.Delete(id);
                            
                            foreach (Curve c in newCurves)
                            {
                                doc.Create.NewModelCurve(c, sketch.SketchPlane);
                            }
                            
                            t2.Commit();
                        }
                        editScope.Commit(new EditProfileIgnoreWarnings());
                    }

                    tg.Commit();
                }

                Result = new AIResult<int>
                {
                    Success = true,
                    Message = "Successfully edited wall profile.",
                    Response = _wallId
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<int>
                {
                    Success = false,
                    Message = ex.Message,
                    Response = -1
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Edit Wall Profile";
    }

    public class EditProfileIgnoreWarnings : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            failuresAccessor.DeleteAllWarnings();
            return FailureProcessingResult.Continue;
        }
    }
}
