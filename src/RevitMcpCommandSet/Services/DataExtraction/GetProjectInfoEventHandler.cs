using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetProjectInfoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public GetProjectInfoResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters()
        {
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
                var pi = doc.ProjectInformation;

                var info = new ProjectInfoModel
                {
                    ProjectName = pi.Name ?? "",
                    ProjectNumber = pi.Number ?? "",
                    ClientName = pi.ClientName ?? "",
                    Address = pi.Address ?? "",
                    BuildingName = pi.BuildingName ?? "",
                    Author = pi.Author ?? "",
                    OrganizationName = pi.OrganizationName ?? "",
                    OrganizationDescription = pi.OrganizationDescription ?? "",
                    Status = pi.Status ?? "",
                    IssueDate = pi.IssueDate ?? "",
                    FilePath = doc.PathName ?? ""
                };

                ResultInfo = new GetProjectInfoResult
                {
                    ProjectInfo = info,
                    Success = true,
                    Message = "Successfully retrieved project info"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetProjectInfoResult
                {
                    Success = false,
                    Message = $"Error getting project info: {ex.Message}"
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName() => "Get Project Info";
    }
}
