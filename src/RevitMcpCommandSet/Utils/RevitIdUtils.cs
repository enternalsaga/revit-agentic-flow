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

        public static int ToInt(ElementId id)
        {
            return (int)ToLong(id);
        }

        public static ElementId ToElementId(long id)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(id);
#else
            return new ElementId((int)id);
#endif
        }

        public static ElementId ToElementId(int id)
        {
#if REVIT2024_OR_GREATER
            return new ElementId((long)id);
#else
            return new ElementId(id);
#endif
        }
    }
}
