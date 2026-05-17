using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Workspace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitMCPCommandSet.Utils
{
    public static class WorkspaceSnapshotUtils
    {
        public static WorkspaceViewInfo CreateViewInfo(View view)
        {
            return new WorkspaceViewInfo
            {
                Id = RevitIdUtils.ToLong(view.Id),
                UniqueId = view.UniqueId,
                Name = view.Name,
                ViewType = view.ViewType.ToString(),
                IsTemplate = view.IsTemplate,
                Scale = view.Scale,
                DetailLevel = view.DetailLevel.ToString(),
                DisplayStyle = view.DisplayStyle.ToString(),
                CanBePrinted = view.CanBePrinted
            };
        }

        public static WorkspaceUiViewInfo CreateUiViewInfo(UIApplication app, View activeView)
        {
            var uiView = app.ActiveUIDocument
                .GetOpenUIViews()
                .FirstOrDefault(v => RevitIdUtils.ToLong(v.ViewId) == RevitIdUtils.ToLong(activeView.Id));

            if (uiView == null)
            {
                return new WorkspaceUiViewInfo { Found = false };
            }

            var rect = uiView.GetWindowRectangle();
            var corners = uiView.GetZoomCorners();

            return new WorkspaceUiViewInfo
            {
                Found = true,
                WindowRectangle = new WorkspaceRect
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Right = rect.Right,
                    Bottom = rect.Bottom,
                    Width = rect.Right - rect.Left,
                    Height = rect.Bottom - rect.Top
                },
                ZoomCornerMin = ToPoint(corners[0]),
                ZoomCornerMax = ToPoint(corners[1])
            };
        }

        public static WorkspaceElementInfo CreateElementInfo(Document doc, View view, Element element)
        {
            return new WorkspaceElementInfo
            {
                Id = RevitIdUtils.ToLong(element.Id),
                UniqueId = element.UniqueId,
                Name = element.Name,
                Category = element.Category?.Name ?? "Unknown",
                TypeName = GetTypeName(doc, element),
                BoundingBox = ToBoundingBox(element.get_BoundingBox(view) ?? element.get_BoundingBox(null)),
                Parameters = GetCommonParameters(element)
            };
        }

        public static List<WorkspaceElementInfo> GetVisibleElements(Document doc, View view, int limit, out int totalElementsInView)
        {
            var elements = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(e => e.Category != null && !e.IsHidden(view))
                .ToList();

            totalElementsInView = elements.Count;

            if (limit > 0)
            {
                elements = elements.Take(limit).ToList();
            }

            return elements.Select(e => CreateElementInfo(doc, view, e)).ToList();
        }

        public static List<WorkspaceElementInfo> GetSelection(Document doc, View view, UIDocument uiDoc)
        {
            return uiDoc.Selection
                .GetElementIds()
                .Select(doc.GetElement)
                .Where(e => e != null)
                .Select(e => CreateElementInfo(doc, view, e))
                .ToList();
        }

        public static string BuildImageBasePath(string outputDirectory, string viewName)
        {
            var root = string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(Path.GetTempPath(), "RevitMCP", "workspace_snapshots")
                : outputDirectory;

            Directory.CreateDirectory(root);

            var safeViewName = string.Join("_", viewName.Split(Path.GetInvalidFileNameChars()));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            return Path.Combine(root, $"workspace_{safeViewName}_{timestamp}");
        }

        public static WorkspaceImageInfo ExportActiveViewImage(Document doc, View view, string outputDirectory, int pixelSize)
        {
            var imageInfo = new WorkspaceImageInfo
            {
                Exported = false,
                PixelSize = pixelSize
            };

            try
            {
                var basePath = BuildImageBasePath(outputDirectory, view.Name);

                using (var options = new ImageExportOptions())
                {
                    options.ExportRange = ExportRange.SetOfViews;
                    options.SetViewsAndSheets(new List<ElementId> { view.Id });
                    options.FilePath = basePath;
                    options.HLRandWFViewsFileType = ImageFileType.PNG;
                    options.ShadowViewsFileType = ImageFileType.PNG;
                    options.ZoomType = ZoomFitType.FitToPage;
                    options.PixelSize = Math.Max(256, pixelSize);
                    options.FitDirection = FitDirectionType.Horizontal;

                    doc.ExportImage(options);
                }

                var directory = Path.GetDirectoryName(basePath);
                var prefix = Path.GetFileName(basePath);
                var exportedFile = Directory.GetFiles(directory, $"{prefix}*.png")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                imageInfo.Exported = !string.IsNullOrEmpty(exportedFile);
                imageInfo.FilePath = exportedFile;
            }
            catch (Exception ex)
            {
                imageInfo.Error = ex.Message;
            }

            return imageInfo;
        }

        private static WorkspacePoint ToPoint(XYZ point)
        {
            if (point == null) return null;
            return new WorkspacePoint { X = point.X, Y = point.Y, Z = point.Z };
        }

        private static WorkspaceBoundingBox ToBoundingBox(BoundingBoxXYZ bbox)
        {
            if (bbox == null) return null;
            return new WorkspaceBoundingBox
            {
                Min = ToPoint(bbox.Min),
                Max = ToPoint(bbox.Max)
            };
        }

        private static string GetTypeName(Document doc, Element element)
        {
            var typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId) return string.Empty;

            var type = doc.GetElement(typeId);
            return type?.Name ?? string.Empty;
        }

        private static Dictionary<string, string> GetCommonParameters(Element element)
        {
            var result = new Dictionary<string, string>();
            foreach (var parameterName in new[] { "Mark", "Comments", "Level", "Family", "Type" })
            {
                var parameter = element.LookupParameter(parameterName);
                if (parameter == null || !parameter.HasValue) continue;

                var value = GetParameterValue(parameter);
                if (!string.IsNullOrEmpty(value))
                {
                    result[parameterName] = value;
                }
            }

            return result;
        }

        private static string GetParameterValue(Parameter parameter)
        {
            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString();
                case StorageType.Double:
                    return parameter.AsValueString() ?? parameter.AsDouble().ToString("F3");
                case StorageType.Integer:
                    return parameter.AsInteger().ToString();
                case StorageType.ElementId:
                    return RevitIdUtils.ToLong(parameter.AsElementId()).ToString();
                default:
                    return parameter.AsValueString();
            }
        }
    }
}
