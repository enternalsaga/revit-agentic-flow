using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Workspace
{
    public class WorkspaceSnapshotRequest
    {
        public bool IncludeImage { get; set; } = true;
        public bool IncludeVisibleElements { get; set; } = true;
        public bool IncludeSelection { get; set; } = true;
        public int ElementLimit { get; set; } = 100;
        public int PixelSize { get; set; } = 1600;
        public string OutputDirectory { get; set; }
    }

    public class WorkspaceSnapshotResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TimestampUtc { get; set; }
        public WorkspaceViewInfo ActiveView { get; set; }
        public WorkspaceUiViewInfo UiView { get; set; }
        public WorkspaceImageInfo Image { get; set; }
        public int TotalElementsInView { get; set; }
        public int ReturnedElementCount { get; set; }
        public List<WorkspaceElementInfo> VisibleElements { get; set; } = new List<WorkspaceElementInfo>();
        public List<WorkspaceElementInfo> Selection { get; set; } = new List<WorkspaceElementInfo>();
    }

    public class WorkspaceViewInfo
    {
        public long Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public string ViewType { get; set; }
        public bool IsTemplate { get; set; }
        public int Scale { get; set; }
        public string DetailLevel { get; set; }
        public string DisplayStyle { get; set; }
        public bool CanBePrinted { get; set; }
    }

    public class WorkspaceUiViewInfo
    {
        public bool Found { get; set; }
        public WorkspaceRect WindowRectangle { get; set; }
        public WorkspacePoint ZoomCornerMin { get; set; }
        public WorkspacePoint ZoomCornerMax { get; set; }
    }

    public class WorkspaceImageInfo
    {
        public bool Exported { get; set; }
        public string FilePath { get; set; }
        public int PixelSize { get; set; }
        public string Error { get; set; }
    }

    public class WorkspaceElementInfo
    {
        public long Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }
        public WorkspaceBoundingBox BoundingBox { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    public class WorkspaceBoundingBox
    {
        public WorkspacePoint Min { get; set; }
        public WorkspacePoint Max { get; set; }
    }

    public class WorkspacePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class WorkspaceRect
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
