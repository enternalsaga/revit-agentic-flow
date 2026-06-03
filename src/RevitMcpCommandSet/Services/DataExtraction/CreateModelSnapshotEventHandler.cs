using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class CreateModelSnapshotEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private string _category;

        public ModelSnapshotResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(string category)
        {
            _category = category;
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
                
                var collector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WhereElementIsViewIndependent();

                if (!string.IsNullOrEmpty(_category) && Enum.TryParse(_category, true, out BuiltInCategory cat))
                {
                    collector.OfCategory(cat);
                }
                
                var elementsDict = new Dictionary<string, SnapshotElementInfo>();

                using (var md5 = MD5.Create())
                {
                    foreach (Element e in collector)
                    {
                        if (e.Category == null) continue; // Skip elements without category

                        string name = e.Name;
                        string categoryName = e.Category.Name;
                        string locationData = GetLocationData(e);

                        // Build a payload string to hash
                        string payload = $"{name}|{categoryName}|{locationData}";
                        
                        byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(payload));
                        string hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

#if REVIT2024_OR_GREATER
                        string idStr = e.Id.Value.ToString();
#else
                        string idStr = e.Id.IntegerValue.ToString();
#endif

                        elementsDict[idStr] = new SnapshotElementInfo
                        {
                            Hash = hashStr,
                            Category = categoryName,
                            Name = name
                        };
                    }
                }

                ResultInfo = new ModelSnapshotResult
                {
                    Success = true,
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    Elements = elementsDict,
                    Message = $"Created snapshot of {elementsDict.Count} elements."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new ModelSnapshotResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        private string GetLocationData(Element e)
        {
            if (e.Location is LocationPoint lp) return $"{lp.Point.X:F3},{lp.Point.Y:F3},{lp.Point.Z:F3}";
            if (e.Location is LocationCurve lc) 
                return $"{lc.Curve.GetEndPoint(0).X:F3},{lc.Curve.GetEndPoint(0).Y:F3},{lc.Curve.GetEndPoint(0).Z:F3}_" +
                       $"{lc.Curve.GetEndPoint(1).X:F3},{lc.Curve.GetEndPoint(1).Y:F3},{lc.Curve.GetEndPoint(1).Z:F3}";
            var bbox = e.get_BoundingBox(null);
            if (bbox != null) 
                return $"BBox_{bbox.Min.X:F3},{bbox.Min.Y:F3},{bbox.Min.Z:F3}_{bbox.Max.X:F3},{bbox.Max.Y:F3},{bbox.Max.Z:F3}";
            return "NoLocation";
        }

        public string GetName() => "Create Model Snapshot";
    }
}
