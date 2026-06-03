using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class TraceMepConnectionsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _elementId;
        private int _maxDepth;

        public MepTraceResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(long elementId, int maxDepth)
        {
            _elementId = elementId;
            _maxDepth = maxDepth;
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
                var elemId = new ElementId(_elementId);
#else
                var elemId = new ElementId((int)_elementId);
#endif
                var startElement = doc.GetElement(elemId);

                if (startElement == null)
                {
                    ResultInfo = new MepTraceResult { Success = false, Message = "Start element not found" };
                    return;
                }

                var visited = new HashSet<ElementId>();
                var queue = new Queue<(Element Element, int Depth)>();
                
                queue.Enqueue((startElement, 0));
                visited.Add(startElement.Id);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.Depth >= _maxDepth) continue;

                    var connected = GetConnectedElements(current.Element);
                    foreach (var conn in connected)
                    {
                        if (!visited.Contains(conn.Id))
                        {
                            visited.Add(conn.Id);
                            queue.Enqueue((conn, current.Depth + 1));
                        }
                    }
                }

                // Remove start element from result list
                visited.Remove(startElement.Id);

                ResultInfo = new MepTraceResult
                {
                    Success = true,
                    TotalConnected = visited.Count,
                    ConnectedElements = visited.Select(id => 
#if REVIT2024_OR_GREATER
                        id.Value
#else
                        (long)id.IntegerValue
#endif
                    ).ToList(),
                    Message = $"Found {visited.Count} connected elements within depth {_maxDepth}."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new MepTraceResult { Success = false, Message = $"Error: {ex.Message}" };
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
                    // Filter out connector types like logical, physical only
                    if (refConnector.Owner.Id != e.Id && refConnector.Owner.Category != null)
                        yield return refConnector.Owner;
                }
            }
        }

        public string GetName() => "Trace MEP Connections";
    }
}
