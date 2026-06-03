using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class BatchChangeMaterialsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private string _category;
        private string _searchMaterialName;
        private string _replaceMaterialName;
        private bool _dryRun;

        public BatchChangeMaterialsResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(string category, string searchMaterialName, string replaceMaterialName, bool dryRun)
        {
            _category = category;
            _searchMaterialName = searchMaterialName;
            _replaceMaterialName = replaceMaterialName;
            _dryRun = dryRun;
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

                Material searchMat = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .FirstOrDefault(m => m.Name.Equals(_searchMaterialName, StringComparison.OrdinalIgnoreCase));

                Material replaceMat = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .FirstOrDefault(m => m.Name.Equals(_replaceMaterialName, StringComparison.OrdinalIgnoreCase));

                if (searchMat == null)
                {
                    ResultInfo = new BatchChangeMaterialsResult { Success = false, Message = $"Search material '{_searchMaterialName}' not found." };
                    return;
                }
                if (replaceMat == null)
                {
                    ResultInfo = new BatchChangeMaterialsResult { Success = false, Message = $"Replace material '{_replaceMaterialName}' not found." };
                    return;
                }

                var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
                if (!string.IsNullOrEmpty(_category) && Enum.TryParse(_category, true, out BuiltInCategory cat))
                {
                    collector.OfCategory(cat);
                }

                var processedTypeIds = new HashSet<ElementId>();
                int instancesUpdated = 0;
                int typesUpdated = 0;
                int compoundStructuresUpdated = 0;

                using (Transaction tx = new Transaction(doc, "Batch Change Materials"))
                {
                    if (!_dryRun) tx.Start();

                    foreach (Element e in collector)
                    {
                        // Check Instance Parameters
                        bool instanceModified = false;
                        foreach (Parameter p in e.Parameters)
                        {
                            if (p.StorageType == StorageType.ElementId && !p.IsReadOnly)
                            {
#if REVIT2024_OR_GREATER
                                if (p.AsElementId().Value == searchMat.Id.Value)
                                {
                                    if (!_dryRun) p.Set(replaceMat.Id);
                                    instanceModified = true;
                                }
#else
                                if (p.AsElementId().IntegerValue == searchMat.Id.IntegerValue)
                                {
                                    if (!_dryRun) p.Set(replaceMat.Id);
                                    instanceModified = true;
                                }
#endif
                            }
                        }
                        if (instanceModified) instancesUpdated++;

                        // Process Element Type
                        ElementId typeId = e.GetTypeId();
                        if (typeId != ElementId.InvalidElementId && !processedTypeIds.Contains(typeId))
                        {
                            processedTypeIds.Add(typeId);
                            ElementType type = doc.GetElement(typeId) as ElementType;
                            if (type != null)
                            {
                                // Check Type Parameters
                                bool typeModified = false;
                                foreach (Parameter p in type.Parameters)
                                {
                                    if (p.StorageType == StorageType.ElementId && !p.IsReadOnly)
                                    {
#if REVIT2024_OR_GREATER
                                        if (p.AsElementId().Value == searchMat.Id.Value)
                                        {
                                            if (!_dryRun) p.Set(replaceMat.Id);
                                            typeModified = true;
                                        }
#else
                                        if (p.AsElementId().IntegerValue == searchMat.Id.IntegerValue)
                                        {
                                            if (!_dryRun) p.Set(replaceMat.Id);
                                            typeModified = true;
                                        }
#endif
                                    }
                                }
                                if (typeModified) typesUpdated++;

                                // Check Compound Structure
                                if (type is HostObjAttributes hostObj)
                                {
                                    CompoundStructure cs = hostObj.GetCompoundStructure();
                                    if (cs != null)
                                    {
                                        bool csModified = false;
                                        IList<CompoundStructureLayer> layers = cs.GetLayers();
                                        for (int i = 0; i < layers.Count; i++)
                                        {
                                            var layer = layers[i];
#if REVIT2024_OR_GREATER
                                            if (layer.MaterialId.Value == searchMat.Id.Value)
                                            {
                                                if (!_dryRun) cs.SetMaterialId(i, replaceMat.Id);
                                                csModified = true;
                                            }
#else
                                            if (layer.MaterialId.IntegerValue == searchMat.Id.IntegerValue)
                                            {
                                                if (!_dryRun) cs.SetMaterialId(i, replaceMat.Id);
                                                csModified = true;
                                            }
#endif
                                        }

                                        if (csModified)
                                        {
                                            if (!_dryRun) hostObj.SetCompoundStructure(cs);
                                            compoundStructuresUpdated++;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (!_dryRun) tx.Commit();
                }

                ResultInfo = new BatchChangeMaterialsResult
                {
                    Success = true,
                    DryRun = _dryRun,
                    InstancesUpdated = instancesUpdated,
                    TypesUpdated = typesUpdated,
                    CompoundStructuresUpdated = compoundStructuresUpdated,
                    Message = $"Successfully processed materials. Instances: {instancesUpdated}, Types: {typesUpdated}, CompoundStructures: {compoundStructuresUpdated}."
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new BatchChangeMaterialsResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Batch Change Materials";
    }
}
