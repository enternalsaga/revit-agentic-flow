using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils
{
    public static class RevitIdUtils
    {
        public static long ToLong(ElementId id)
        {
            if (id == null) return -1;

#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }
    }
}
