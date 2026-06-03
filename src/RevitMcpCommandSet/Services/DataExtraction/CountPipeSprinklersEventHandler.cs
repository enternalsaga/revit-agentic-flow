using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class CountPipeSprinklersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _pipeId;

        public PipeSprinklerCountResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(long pipeId)
        {
            _pipeId = pipeId;
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
                var elemId = new ElementId(_pipeId);
#else
                var elemId = new ElementId((int)_pipeId);
#endif
                var pipeElement = doc.GetElement(elemId) as Autodesk.Revit.DB.Plumbing.Pipe;

                if (pipeElement == null)
                {
                    ResultInfo = new PipeSprinklerCountResult { Success = false, Message = "Element is not a Pipe or not found" };
                    return;
                }

                // Get pipe diameter
                double diameterInches = 0;
                double diameterMm = 0;
                Parameter diameterParam = pipeElement.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                if (diameterParam != null)
                {
                    diameterInches = diameterParam.AsDouble() * 12.0; // from feet to inches
                    diameterMm = diameterParam.AsDouble() * 304.8;
                }

                // Traverse network to find sprinklers
                var visited = new HashSet<ElementId>();
                var queue = new Queue<Element>();
                
                queue.Enqueue(pipeElement);
                visited.Add(pipeElement.Id);

                int sprinklerCount = 0;

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();

                    var connected = GetConnectedElements(current);
                    foreach (var conn in connected)
                    {
                        if (!visited.Contains(conn.Id))
                        {
                            visited.Add(conn.Id);
                            queue.Enqueue(conn);

                            if (conn.Category != null && conn.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Sprinklers)
                            {
                                sprinklerCount++;
                            }
                        }
                    }
                }

                double requiredDiameterMm = GetRequiredDiameterMm(sprinklerCount);
                bool satisfies = diameterMm >= requiredDiameterMm * 0.95; // 5% tolerance

                ResultInfo = new PipeSprinklerCountResult
                {
                    Success = true,
                    SprinklerCount = sprinklerCount,
                    PipeDiameter = Math.Round(diameterMm, 1),
                    RequiredDiameter = requiredDiameterMm,
                    SatisfiesNFPA = satisfies,
                    Message = satisfies ? "Pipe diameter satisfies NFPA light hazard schedule." : "Pipe diameter is undersized for the number of downstream sprinklers."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new PipeSprinklerCountResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        private IEnumerable<Element> GetConnectedElements(Element e)
        {
            ConnectorManager cm = null;
            if (e is MEPCurve curve) cm = curve.ConnectorManager;
            else if (e is FamilyInstance fi && fi.MEPModel != null) cm = fi.MEPModel.ConnectorManager;

            if (cm == null) yield break;

            foreach (Connector c in cm.Connectors)
            {
                if (!c.IsConnected) continue;
                foreach (Connector refConnector in c.AllRefs)
                {
                    if (refConnector.Owner.Id != e.Id && refConnector.Owner.Category != null)
                        yield return refConnector.Owner;
                }
            }
        }

        private double GetRequiredDiameterMm(int sprinklerCount)
        {
            if (sprinklerCount <= 2) return 25.0; // 1"
            if (sprinklerCount <= 3) return 32.0; // 1-1/4"
            if (sprinklerCount <= 5) return 40.0; // 1-1/2"
            if (sprinklerCount <= 10) return 50.0; // 2"
            if (sprinklerCount <= 20) return 65.0; // 2-1/2"
            if (sprinklerCount <= 40) return 80.0; // 3"
            if (sprinklerCount <= 100) return 100.0; // 4"
            return 150.0; // 6"
        }

        public string GetName() => "Count Pipe Sprinklers";
    }
}
