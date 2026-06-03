using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class SetElementParameterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _elementId;
        private string _parameterName;
        private JToken _value;
        public AIResult<string> ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(long elementId, string parameterName, JToken value)
        {
            _elementId = elementId;
            _parameterName = parameterName;
            _value = value;
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
                var elemId = new ElementId((long)_elementId);
#else
                var elemId = new ElementId((int)_elementId);
#endif
                var element = doc.GetElement(elemId);
                if (element == null)
                {
                    ResultInfo = new AIResult<string> { Success = false, Message = $"Element {_elementId} not found" };
                    return;
                }

                Parameter param = element.LookupParameter(_parameterName);
                if (param == null)
                {
                    ResultInfo = new AIResult<string> { Success = false, Message = $"Parameter '{_parameterName}' not found" };
                    return;
                }
                if (param.IsReadOnly)
                {
                    ResultInfo = new AIResult<string> { Success = false, Message = $"Parameter '{_parameterName}' is read-only" };
                    return;
                }

                using (Transaction tx = new Transaction(doc, $"Set {_parameterName}"))
                {
                    tx.Start();
                    bool setOk = false;
                    switch (param.StorageType)
                    {
                        case StorageType.String:
                            setOk = param.Set(_value.ToString());
                            break;
                        case StorageType.Integer:
                            setOk = param.Set(_value.Value<int>());
                            break;
                        case StorageType.Double:
                            setOk = param.Set(_value.Value<double>());
                            break;
                        case StorageType.ElementId:
#if REVIT2024_OR_GREATER
                            setOk = param.Set(new ElementId(_value.Value<long>()));
#else
                            setOk = param.Set(new ElementId(_value.Value<int>()));
#endif
                            break;
                    }
                    tx.Commit();
                    ResultInfo = new AIResult<string>
                    {
                        Success = setOk,
                        Message = setOk ? $"Set '{_parameterName}' = '{_value}' on element {_elementId}" : "Failed to set parameter value",
                        Response = setOk ? "OK" : "FAILED"
                    };
                }
            }
            catch (Exception ex)
            {
                ResultInfo = new AIResult<string> { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName() => "Set Element Parameter";
    }
}
