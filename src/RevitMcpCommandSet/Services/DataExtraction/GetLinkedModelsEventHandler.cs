using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetLinkedModelsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public GetLinkedModelsResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

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
                var linkModels = new List<LinkedModelInfo>();

                // Get Revit links
                var revitLinks = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkType))
                    .Cast<RevitLinkType>();

                foreach (RevitLinkType linkType in revitLinks)
                {
                    var exFileRef = linkType.GetExternalFileReference();
                    string path = exFileRef != null
                        ? ModelPathUtils.ConvertModelPathToUserVisiblePath(exFileRef.GetAbsolutePath())
                        : "";
                    bool isLoaded = exFileRef != null &&
                        exFileRef.GetLinkedFileStatus() == LinkedFileStatus.Loaded;

                    linkModels.Add(new LinkedModelInfo
                    {
#if REVIT2024_OR_GREATER
                        Id = linkType.Id.Value,
#else
                        Id = linkType.Id.IntegerValue,
#endif
                        Name = linkType.Name,
                        FilePath = path,
                        IsLoaded = isLoaded,
                        LinkType = "RevitLink"
                    });
                }

                // Get CAD links
                var cadLinks = new FilteredElementCollector(doc)
                    .OfClass(typeof(CADLinkType))
                    .Cast<CADLinkType>();

                foreach (CADLinkType cadLink in cadLinks)
                {
                    var exFileRef = cadLink.GetExternalFileReference();
                    string path = exFileRef != null
                        ? ModelPathUtils.ConvertModelPathToUserVisiblePath(exFileRef.GetAbsolutePath())
                        : "";

                    linkModels.Add(new LinkedModelInfo
                    {
#if REVIT2024_OR_GREATER
                        Id = cadLink.Id.Value,
#else
                        Id = cadLink.Id.IntegerValue,
#endif
                        Name = cadLink.Name,
                        FilePath = path,
                        IsLoaded = true,
                        LinkType = "CADLink"
                    });
                }

                ResultInfo = new GetLinkedModelsResult
                {
                    TotalLinks = linkModels.Count,
                    Links = linkModels,
                    Success = true,
                    Message = $"Found {linkModels.Count} linked models"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetLinkedModelsResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Get Linked Models";
    }
}
