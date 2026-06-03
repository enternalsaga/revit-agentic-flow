using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services
{
    public class InspectStackedWallTypeEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public StackedWallTypeInspectionInfo InspectionInfo { get; private set; }
        public AIResult<StackedWallTypeInspectionResult> Result { get; private set; }

        public void SetParameters(StackedWallTypeInspectionInfo data)
        {
            InspectionInfo = data;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;
            Transaction trans = null;

            try
            {
                WallType stackedType = ResolveStackedWallType();
                if (stackedType == null)
                    throw new InvalidOperationException("No stacked wall type found. Provide typeId/typeName or load a stacked wall type into the project.");

                Level baseLevel = doc.FindNearestLevel(InspectionInfo.BaseLevel / 304.8);
                if (baseLevel == null)
                    throw new InvalidOperationException("No level found for temporary stacked wall inspection.");

                double sampleLength = Math.Max(InspectionInfo.SampleLength, 1000) / 304.8;
                double sampleHeight = Math.Max(InspectionInfo.SampleHeight, 1000) / 304.8;
                double baseOffset = InspectionInfo.BaseLevel / 304.8 - baseLevel.Elevation;

                var result = new StackedWallTypeInspectionResult
                {
                    TypeId = stackedType.Id.GetValue(),
                    TypeName = stackedType.Name,
                    SampleHeight = sampleHeight * 304.8
                };

                trans = new Transaction(doc, "Inspect Stacked Wall Type");
                trans.Start();

                Line baseline = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(sampleLength, 0, 0));
                Wall tempWall = Wall.Create(doc, baseline, stackedType.Id, baseLevel.Id, sampleHeight, baseOffset, false, false);
                doc.Regenerate();

                if (tempWall == null || !tempWall.IsStackedWall)
                    throw new InvalidOperationException($"Type '{stackedType.Name}' did not create a stacked wall instance.");

                foreach (ElementId memberId in tempWall.GetStackedWallMemberIds())
                {
                    Wall member = doc.GetElement(memberId) as Wall;
                    if (member == null)
                        continue;

                    BoundingBoxXYZ bbox = member.get_BoundingBox(null);
                    double minZ = bbox == null ? 0 : bbox.Min.Z * 304.8;
                    double maxZ = bbox == null ? 0 : bbox.Max.Z * 304.8;

                    result.Members.Add(new StackedWallMemberResult
                    {
                        MemberId = member.Id.GetValue(),
                        WallTypeId = member.WallType.Id.GetValue(),
                        WallTypeName = member.WallType.Name,
                        Width = member.Width * 304.8,
                        Height = bbox == null ? 0 : (bbox.Max.Z - bbox.Min.Z) * 304.8,
                        MinZ = minZ,
                        MaxZ = maxZ
                    });
                }

                trans.RollBack();
                trans = null;

                Result = new AIResult<StackedWallTypeInspectionResult>
                {
                    Success = true,
                    Message = $"Inspected stacked wall type '{stackedType.Name}' using a rolled-back temporary wall. No model elements were kept.",
                    Response = result
                };
            }
            catch (Exception ex)
            {
                if (trans != null && trans.HasStarted())
                    trans.RollBack();

                Result = new AIResult<StackedWallTypeInspectionResult>
                {
                    Success = false,
                    Message = $"Failed to inspect stacked wall type: {ex.Message}",
                    Response = null
                };
                TaskDialog.Show("Error", $"Failed to inspect stacked wall type: {ex.Message}");
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

        public string GetName()
        {
            return "Inspect Stacked Wall Type";
        }

        private WallType ResolveStackedWallType()
        {
            if (InspectionInfo.TypeId > 0)
            {
                Element element = doc.GetElement(RevitIdUtils.ToElementId(InspectionInfo.TypeId));
                if (element is WallType wallType && wallType.Kind == WallKind.Stacked)
                    return wallType;
            }

            var stackedTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .Where(wt => wt.Kind == WallKind.Stacked)
                .ToList();

            if (!string.IsNullOrWhiteSpace(InspectionInfo.TypeName))
            {
                WallType exact = stackedTypes.FirstOrDefault(wt =>
                    string.Equals(wt.Name, InspectionInfo.TypeName, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return exact;

                WallType contains = stackedTypes.FirstOrDefault(wt =>
                    wt.Name.IndexOf(InspectionInfo.TypeName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (contains != null)
                    return contains;
            }

            return stackedTypes.FirstOrDefault();
        }
    }
}
