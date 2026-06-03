using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetCategoriesCommand : ExternalEventCommandBase
    {
        private GetCategoriesEventHandler _handler => (GetCategoriesEventHandler)Handler;

        public override string CommandName => "get_categories";

        public GetCategoriesCommand(UIApplication uiApp)
            : base(new GetCategoriesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                bool includeEmpty = parameters?["includeEmpty"]?.Value<bool>() ?? false;
                _handler.SetParameters(includeEmpty);

                if (RaiseAndWaitForCompletion(60000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get categories operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get categories: {ex.Message}");
            }
        }
    }
}
