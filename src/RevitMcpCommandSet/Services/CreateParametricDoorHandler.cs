using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using RevitMcpSdk;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using System;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class CreateParametricDoorHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private double _width;
        private double _height;
        private int _hostWallId;
        private double _locationParameter;

        public AIResult<int> Result { get; private set; }

        public void SetParameters(double width, double height, int hostWallId, double locationParameter)
        {
            _width = width;
            _height = height;
            _hostWallId = hostWallId;
            _locationParameter = locationParameter;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                int createdId = -1;
                string message = "Success";

                using (Transaction tx = new Transaction(doc, "Create Parametric Door"))
                {
                    tx.Start();

                    // Find default door family symbol
                    FamilySymbol defaultDoorSymbol = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_Doors)
                        .Cast<FamilySymbol>()
                        .FirstOrDefault();

                    if (defaultDoorSymbol == null)
                    {
                        throw new Exception("No door families loaded in the project.");
                    }

                    if (!defaultDoorSymbol.IsActive)
                    {
                        defaultDoorSymbol.Activate();
                    }

                    // Duplicate the symbol for specific dimensions
                    string newTypeName = $"Custom Door {_width}x{_height}";
                    FamilySymbol customDoorSymbol = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_Doors)
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(fs => fs.Name == newTypeName);

                    if (customDoorSymbol == null)
                    {
                        customDoorSymbol = defaultDoorSymbol.Duplicate(newTypeName) as FamilySymbol;
                        
                        Parameter widthParam = customDoorSymbol.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM) 
                                               ?? customDoorSymbol.LookupParameter("Width");
                        Parameter heightParam = customDoorSymbol.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM) 
                                                ?? customDoorSymbol.LookupParameter("Height");

                        if (widthParam != null && !widthParam.IsReadOnly) widthParam.Set(_width);
                        if (heightParam != null && !heightParam.IsReadOnly) heightParam.Set(_height);
                    }

                    // Find host wall
                    Wall hostWall = null;
                    if (_hostWallId > 0)
                    {
                        hostWall = doc.GetElement(RevitIdUtils.ToElementId(_hostWallId)) as Wall;
                    }
                    else
                    {
                        hostWall = new FilteredElementCollector(doc)
                            .OfClass(typeof(Wall))
                            .Cast<Wall>()
                            .FirstOrDefault();
                    }

                    if (hostWall == null)
                    {
                        throw new Exception("Host wall not found or invalid.");
                    }

                    Level level = doc.GetElement(hostWall.LevelId) as Level;
                    if (level == null)
                    {
                        throw new Exception("Valid level not found for the host wall.");
                    }

                    LocationCurve locCurve = hostWall.Location as LocationCurve;
                    if (locCurve == null)
                    {
                        throw new Exception("Wall does not have a valid location curve.");
                    }

                    // Constrain location parameter between 0 and 1
                    double param = Math.Max(0.0, Math.Min(1.0, _locationParameter));
                    XYZ locationPt = locCurve.Curve.Evaluate(param, true);

                    FamilyInstance door = doc.Create.NewFamilyInstance(locationPt, customDoorSymbol, hostWall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    
                    if (door != null)
                    {
                        createdId = RevitIdUtils.ToInt(door.Id);
                        message = $"Successfully created door '{newTypeName}' on wall {RevitIdUtils.ToLong(hostWall.Id)}.";
                    }
                    else
                    {
                        throw new Exception("Failed to instantiate door family.");
                    }

                    tx.Commit();
                }

                Result = new AIResult<int>
                {
                    Success = true,
                    Message = message,
                    Response = createdId
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<int>
                {
                    Success = false,
                    Message = ex.Message,
                    Response = -1
                };
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

        public string GetName() => "Create Parametric Door";
    }
}
