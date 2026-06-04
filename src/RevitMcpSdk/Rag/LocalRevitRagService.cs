using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RevitMcpSdk.Rag;

/// <summary>
/// Local BM25-based RAG service for Revit API documentation.
///
/// Data source: RevitAPI.xml shipped with every Revit installation.
/// Same content as the official Autodesk API docs (the web docs render from this XML).
///
/// Index strategy: Lazy-load on first call. Revit startup is unaffected.
/// The 1-3s build time is absorbed into the first Claude API round-trip (~3-10s).
///
/// Chunk unit: one chunk per class. Includes class summary + all member signatures + descriptions.
/// Giant classes (BuiltInParameter, FilteredElementCollector) are split into domain sub-groups.
///
/// Search: BM25 (keyword). Handles exact API name lookups perfectly.
/// </summary>
public static class LocalRevitRagService
{
    // Lazy-loaded index -- built on first call, cached for the process lifetime.
    // Volatile so the fast path (read outside _buildLock) sees a published
    // engine without acquiring a lock on every search invocation.
    private static volatile BM25Engine? _engine;
    private static volatile string? _indexedXmlPath;
    private static readonly object _buildLock = new object();

    // XML files to index (relative to RevitAPI.dll directory)
    private static readonly string[] XmlFileNames =
    {
        "RevitAPI.xml",
        "RevitAPIUI.xml",
        "RevitAPIIFC.xml"
    };

    // Top-k chunks to return per query.
    private const int TopK = 3;

    // Max characters per chunk sent to the LLM (~300 tokens each).
    private const int MaxChunkDisplayChars = 1200;

    // Cap members per chunk.
    private const int MaxMembersPerChunk = 30;

    /// <summary>
    /// Fetch relevant Revit API documentation for the given query.
    /// </summary>
    public static Task<RagFetchResult> FetchAsync(
        string query,
        string revitVersion,
        CancellationToken ct = default)
    {
        return Task.Run(() => FetchInternal(query, revitVersion), ct);
    }

    private static RagFetchResult FetchInternal(string query, string revitVersion)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var engine = GetOrBuildEngine(revitVersion);

            if (engine == null)
            {
                Debug.WriteLine($"[LocalRAG] [MISS] engine=null (RevitAPI.xml not found)");
                return new RagFetchResult
                {
                    Status = "no_index",
                    ElapsedMs = sw.ElapsedMilliseconds,
                    ErrorSummary = "RevitAPI.xml not found in Revit installation directory."
                };
            }

            var hits = engine.Search(query, TopK);
            sw.Stop();

            if (hits.Count == 0)
            {
                Debug.WriteLine($"[LocalRAG] [MISS] query=\"{Clip(query)}\" ms={sw.ElapsedMilliseconds}");
                return new RagFetchResult
                {
                    Status = "no_match",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }

            // Build context text from top hits
            var sb = new StringBuilder();
            for (int i = 0; i < hits.Count; i++)
            {
                var chunk = hits[i];
                sb.AppendLine($"--- [{i + 1}] {chunk.Namespace}.{chunk.ClassName} ---");
                sb.AppendLine(chunk.DisplayText);
                sb.AppendLine();
            }

            string contextText = sb.ToString();

            Debug.WriteLine($"[LocalRAG] [HIT] query=\"{Clip(query)}\" hits={hits.Count} " +
                $"top=\"{hits[0].ClassName}\" ms={sw.ElapsedMilliseconds} " +
                $"preview=\"{Clip(hits[0].DisplayText, 120)}\"");

            return new RagFetchResult
            {
                Status = "hit",
                ContextText = contextText,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Debug.WriteLine($"[LocalRAG] [ERROR] {ex}");
            return new RagFetchResult
            {
                Status = "error",
                ElapsedMs = sw.ElapsedMilliseconds,
                ErrorSummary = ex.Message
            };
        }
    }

    // -- Index build (lazy, cached per process) --

    private static BM25Engine? GetOrBuildEngine(string revitVersion)
    {
        string? xmlDir = ResolveRevitXmlDirectory();
        if (string.IsNullOrEmpty(xmlDir))
        {
            Debug.WriteLine("[LocalRAG] [INDEX] RevitAPI.xml directory not resolved");
            return null;
        }

        // Fast path: engine already built for this XML location -- no lock needed.
        var cached = _engine;
        if (cached != null && string.Equals(_indexedXmlPath, xmlDir, StringComparison.OrdinalIgnoreCase))
            return cached;

        // Slow path: serialize the actual build. Double-check inside the lock.
        lock (_buildLock)
        {
            if (_engine != null && string.Equals(_indexedXmlPath, xmlDir, StringComparison.OrdinalIgnoreCase))
                return _engine;

            Debug.WriteLine($"[LocalRAG] [INDEX_BUILD_START] dir=\"{xmlDir}\" revit={revitVersion}");
            var buildSw = Stopwatch.StartNew();

            var chunks = BuildChunks(xmlDir!);
            buildSw.Stop();

            if (chunks.Count == 0)
            {
                Debug.WriteLine($"[LocalRAG] [INDEX_BUILD_FAILED] no chunks produced ms={buildSw.ElapsedMilliseconds}");
                return null;
            }

            var built = new BM25Engine(chunks);
            // Publish path field first so a fast-path reader can't see a
            // matching engine paired with a stale path.
            _indexedXmlPath = xmlDir;
            _engine = built;

            Debug.WriteLine($"[LocalRAG] [INDEX_BUILD_DONE] chunks={chunks.Count} bm25_tokens={built.ChunkCount} " +
                $"ms={buildSw.ElapsedMilliseconds} dir=\"{xmlDir}\"");

            return built;
        }
    }

    // -- XML -> RagChunk conversion --

    private static List<RagChunk> BuildChunks(string xmlDir)
    {
        // Collect all members across all XML files
        var allMembers = new List<XElement>();
        foreach (string fileName in XmlFileNames)
        {
            string path = Path.Combine(xmlDir, fileName);
            if (!File.Exists(path)) continue;

            try
            {
                var doc = XDocument.Load(path);
                allMembers.AddRange(doc.Descendants("member"));
                Debug.WriteLine($"[LocalRAG] [XML_LOADED] file={fileName} members={allMembers.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalRAG] [XML_LOAD_ERROR] file={fileName} err={ex.Message}");
            }
        }

        if (allMembers.Count == 0) return new List<RagChunk>();

        // Group by class name
        var byClass = new Dictionary<string, ClassAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in allMembers)
        {
            string? nameAttr = member.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(nameAttr)) continue;

            if (!TryParseRevitMember(nameAttr!, out string ns, out string className, out string memberName))
                continue;

            string key = ns + "." + className;
            if (!byClass.TryGetValue(key, out var acc))
            {
                acc = new ClassAccumulator { Namespace = ns, ClassName = className };
                byClass[key] = acc;
            }

            string summary = NormalizeSummary(member.Element("summary")?.Value);
            string remarks = NormalizeSummary(member.Element("remarks")?.Value);

            if (string.IsNullOrEmpty(memberName))
            {
                // Type-level summary
                acc.ClassSummary = summary;
                if (!string.IsNullOrEmpty(remarks))
                    acc.ClassRemarks = remarks;
            }
            else
            {
                // Member: collect parameters
                var paramDescs = new List<string>();
                foreach (var p in member.Elements("param"))
                {
                    string? pname = p.Attribute("name")?.Value;
                    string pdesc = NormalizeSummary(p.Value);
                    if (!string.IsNullOrEmpty(pname) && !string.IsNullOrEmpty(pdesc))
                        paramDescs.Add($"  {pname}: {pdesc}");
                }

                string returnDesc = NormalizeSummary(member.Element("returns")?.Value);

                acc.Members.Add(new MemberEntry
                {
                    MemberName = memberName,
                    Summary = summary,
                    Remarks = remarks,
                    ParamDescriptions = paramDescs,
                    Returns = returnDesc,
                    MemberTypePrefix = nameAttr![0]  // T/M/P/F/E
                });
            }
        }

        // Convert accumulators to chunks
        var chunks = new List<RagChunk>(byClass.Count);
        foreach (var kv in byClass.Values)
        {
            // Split giant BuiltInParameter into domain sub-groups
            if (kv.ClassName == "BuiltInParameter" && kv.Members.Count > 200)
            {
                chunks.AddRange(SplitBuiltInParameterChunks(kv));
                continue;
            }

            var chunk = AccumulatorToChunk(kv);
            if (chunk != null) chunks.Add(chunk);
        }

        return chunks;
    }

    private static RagChunk? AccumulatorToChunk(ClassAccumulator acc)
    {
        if (acc.Members.Count == 0 && string.IsNullOrEmpty(acc.ClassSummary))
            return null;

        var sb = new StringBuilder();
        sb.AppendLine($"[Class: {acc.ClassName}]");
        sb.AppendLine($"Namespace: {acc.Namespace}");

        if (!string.IsNullOrEmpty(acc.ClassSummary))
            sb.AppendLine($"Summary: {acc.ClassSummary}");

        if (acc.Members.Count > 0)
        {
            sb.AppendLine();
            foreach (var m in acc.Members.Take(MaxMembersPerChunk))
            {
                string prefix = m.MemberTypePrefix == 'M' ? "" :
                                m.MemberTypePrefix == 'P' ? "[Property] " :
                                m.MemberTypePrefix == 'F' ? "[Field] " :
                                m.MemberTypePrefix == 'E' ? "[Event] " : "";

                sb.Append($"{prefix}{m.MemberName}");
                if (!string.IsNullOrEmpty(m.Returns)) sb.Append($" -> {m.Returns}");
                sb.AppendLine();
                if (!string.IsNullOrEmpty(m.Summary)) sb.AppendLine($"  {m.Summary}");
            }

            if (acc.Members.Count > MaxMembersPerChunk)
                sb.AppendLine($"  ... ({acc.Members.Count - MaxMembersPerChunk} more members omitted)");
        }

        string displayText = sb.ToString();
        if (displayText.Length > MaxChunkDisplayChars)
            displayText = displayText.Substring(0, MaxChunkDisplayChars) + "\n[...truncated]";

        // IndexText = display + extra tags for BM25 coverage
        string indexText = displayText + " " + acc.ClassName + " " + acc.Namespace;

        return new RagChunk
        {
            ClassName = acc.ClassName,
            Namespace = acc.Namespace,
            DisplayText = displayText,
            IndexText = indexText
        };
    }

    private static IEnumerable<RagChunk> SplitBuiltInParameterChunks(ClassAccumulator acc)
    {
        // Group BuiltInParameter fields by domain prefix (WALL_, LEVEL_, MEP_, etc.)
        var domainGroups = new Dictionary<string, List<MemberEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in acc.Members)
        {
            string domain = ExtractBuiltInParamDomain(m.MemberName);
            if (!domainGroups.TryGetValue(domain, out var list))
            {
                list = new List<MemberEntry>();
                domainGroups[domain] = list;
            }
            list.Add(m);
        }

        foreach (var kv in domainGroups)
        {
            var sub = new ClassAccumulator
            {
                Namespace = acc.Namespace,
                ClassName = $"BuiltInParameter.{kv.Key}",
                ClassSummary = $"BuiltInParameter enum values for domain: {kv.Key}",
                Members = kv.Value
            };
            var chunk = AccumulatorToChunk(sub);
            if (chunk != null) yield return chunk;
        }
    }

    private static string ExtractBuiltInParamDomain(string name)
    {
        if (string.IsNullOrEmpty(name)) return "OTHER";
        // Take the first underscore-delimited segment
        int idx = name.IndexOf('_');
        return idx > 0 ? name.Substring(0, idx) : name;
    }

    // -- XML parsing helpers --

    private static bool TryParseRevitMember(
        string nameAttr,
        out string ns,
        out string className,
        out string memberName)
    {
        ns = "";
        className = "";
        memberName = "";

        if (nameAttr.Length < 3) return false;

        // Strip "T:", "M:", "P:", "F:", "E:" prefix
        string fullName = nameAttr.Length > 2 && nameAttr[1] == ':' ? nameAttr.Substring(2) : nameAttr;

        // Remove method parameter signature
        int parenIdx = fullName.IndexOf('(');
        if (parenIdx >= 0) fullName = fullName.Substring(0, parenIdx);

        // Must be in Autodesk.Revit namespace
        const string prefix = "Autodesk.Revit.";
        int prefixIdx = fullName.IndexOf(prefix, StringComparison.Ordinal);
        if (prefixIdx < 0) return false;

        string after = fullName.Substring(prefixIdx);

        // Split into segments
        string[] parts = after.Split('.');
        // parts[0]="Autodesk", parts[1]="Revit", parts[2]="DB" or sub-ns, ...

        if (parts.Length < 4) return false;  // need at least Autodesk.Revit.DB.ClassName

        // Find the class segment: the first segment after the known sub-namespaces
        var knownSubNs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DB", "UI", "Creation", "Structure", "Mechanical", "Plumbing",
            "Electrical", "Architecture", "Analysis", "IFC", "Macros",
            "Visual", "ApplicationServices", "Parameters", "Exceptions"
        };

        int classIdx = -1;
        for (int i = 2; i < parts.Length; i++)
        {
            if (!knownSubNs.Contains(parts[i]))
            {
                classIdx = i;
                break;
            }
        }

        if (classIdx < 0) return false;

        ns = string.Join(".", parts, 0, classIdx);
        className = parts[classIdx];
        memberName = classIdx + 1 < parts.Length ? parts[classIdx + 1] : "";

        // Strip property accessor prefixes
        if (memberName.StartsWith("get_", StringComparison.Ordinal))
            memberName = memberName.Substring(4);
        else if (memberName.StartsWith("set_", StringComparison.Ordinal))
            memberName = memberName.Substring(4);

        return !string.IsNullOrWhiteSpace(className);
    }

    private static string? ResolveRevitXmlDirectory()
    {
        try
        {
            // Find RevitAPI.dll via loaded assemblies (we're running inside Revit)
            var revitApiAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => string.Equals(
                    a.GetName().Name, "RevitAPI", StringComparison.OrdinalIgnoreCase));

            if (revitApiAssembly != null)
            {
                string? dir = Path.GetDirectoryName(revitApiAssembly.Location);
                if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "RevitAPI.xml")))
                    return dir;
            }

            // Fallback: search common install paths
            string[] commonYears = { "2026", "2025", "2024", "2027", "2023", "2022" };
            foreach (string year in commonYears)
            {
                string candidate = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Autodesk", $"Revit {year}");
                if (File.Exists(Path.Combine(candidate, "RevitAPI.xml")))
                    return candidate;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalRAG] [RESOLVE_ERROR] {ex.Message}");
            return null;
        }
    }

    private static string NormalizeSummary(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string trimmed = raw!.Replace("\r", " ").Replace("\n", " ").Trim();
        while (trimmed.Contains("  "))
            trimmed = trimmed.Replace("  ", " ");
        return trimmed;
    }

    private static string Clip(string? text, int maxLen = 80)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = text!.Replace("\r", " ").Replace("\n", " ");
        return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
    }

    // -- Internal data types --

    private sealed class ClassAccumulator
    {
        public string Namespace = "";
        public string ClassName = "";
        public string ClassSummary = "";
        public string ClassRemarks = "";
        public List<MemberEntry> Members = new List<MemberEntry>();
    }

    private sealed class MemberEntry
    {
        public string MemberName = "";
        public string Summary = "";
        public string Remarks = "";
        public string Returns = "";
        public List<string> ParamDescriptions = new List<string>();
        public char MemberTypePrefix;  // M/P/F/E
    }
}
