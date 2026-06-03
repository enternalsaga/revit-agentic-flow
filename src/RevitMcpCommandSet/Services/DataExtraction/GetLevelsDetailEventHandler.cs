using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetLevelsDetailEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public GetLevelsDetailResult ResultInfo { get; private set; }
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
                var levelModels = new List<LevelDetailModel>();

                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation);

                foreach (Level level in levels)
                {
                    int elementCount = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .Where(e => e.LevelId == level.Id)
                        .Count();

                    var isBuildingStory = level.get_Parameter(BuiltInParameter.LEVEL_IS_BUILDING_STORY);

                    levelModels.Add(new LevelDetailModel
                    {
#if REVIT2024_OR_GREATER
                        Id = level.Id.Value,
#else
                        Id = level.Id.IntegerValue,
#endif
                        Name = level.Name,
                        Elevation = level.Elevation * 304.8, // feet to mm
                        IsBuildingStory = isBuildingStory?.AsInteger() == 1,
                        ElementCount = elementCount
                    });
                }

                ResultInfo = new GetLevelsDetailResult
                {
                    TotalLevels = levelModels.Count,
                    Levels = levelModels,
                    Success = true,
                    Message = $"Found {levelModels.Count} levels"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetLevelsDetailResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Get Levels Detail";
    }
}
