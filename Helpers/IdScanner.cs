using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FsLocalizationPlugin.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

namespace FsLocalizationPlugin.Helpers
{
    /// <summary>
    /// Scans the game for string IDs. Any text whose hash matches an existing string is an ID.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>IDs are NOT always "ID_" prefixed, so every candidate is hash-tested.</para>
    /// Sources: <br/>
    /// <list type="bullet">
    /// <item>CString and List of CString properties, recursing into nested structs. Most common referencing method. (e.g. UI, Logics)</item>
    /// <item>Uint "StringHash" properties. Non-FsLoc style reference, hash only. (e.g. BioWare format, found in SWBF2 which mixed CString and StringHash)</item>
    /// <item>EBX and RES asset names. Usually an EA staff's mistake.</item>
    /// <item>EBX type names and enum member names. Usually an EA staff's mistake.</item>
    /// <item>Printable strings inside SwfMovie resources. Scaleform GFx Flash UI, heavily used by old games. (e.g. PvZGW1, PvZGW2)</item>
    /// </list>
    /// Possible ID types: (All examples are from PvZGW2) <br/>
    /// <list type="number">
    /// <item>Common "ID_" prefixed string, only contains uppercase letters (ABC), numbers (123) and underscores (_). e.g. ID_FLAME, ID_M_GOAT, ID_AINAME_CACTUS_BASE_01</item>
    /// <item>Other prefixed string, only contains uppercase letters (ABC), numbers (123) and underscores (_). e.g. PL_01, ALL_CHARACTERS</item>
    /// <item>A asset name or path. e.g. Levels/Level_Herb_Zomburbia/Level_Herb_Zomburbia_Terrain/Level_Herb_Zomburbia_Terrain__0MD_2MD_3MD_10MD__3d__0</item>
    /// <item>A type name or enum member name. e.g. WindComponentData (seems like legacy from BF3/4)</item>
    /// <item>A hash, yes, a hash. e.g. 0xc9e6d993</item>
    /// </list>
    /// Typed reflection with a per-type property cache, no dynamic dispatch.
    /// </remarks>
    public static class IdScanner
    {
        // Disabled for now. Unnecessary before any real user encounters it.
        //private const int MaxRecursionDepth = 16; // Guards against stack overflow on deeply nested structs.
        private const int TotalParts = 3; // EBX scan, SWF scan, Pattern expansion

        public class ScanResult
        {
            public int AssetsScanned;
            public int SwfScanned;
            public int IdsFound;
            public int RefsFound;
            public bool Cancelled;
        }

        /// <summary>The properties of one ebx type worth inspecting. Null when the type has none.</summary>
        private class TypeProps
        {
            public List<PropertyInfo> CStrings;
            public List<PropertyInfo> CStringLists;
            public List<PropertyInfo> StringHashes;
            public List<PropertyInfo> SubObjects;             // Game-defined struct/class values, recursed
            public List<PropertyInfo> SubObjectLists; // List of game-defined struct/class values
        }

        /// <summary>Shared scan state</summary>
        private class Context
        {
            public HashSet<uint> ValidHashes;
            public IdDatabase Db;
            public Dictionary<uint, HashSet<string>> NewRefs;
            public Dictionary<Type, TypeProps> PropCache = new Dictionary<Type, TypeProps>();
            public HashSet<Type> SeenEnumTypes = new HashSet<Type>();
            public ScanResult Result = new ScanResult();
            public CancellationToken Token;
            public FrostySdk.Interfaces.ILogger Logger;
            public FrostyTaskWindow Task;
        }

        /// <summary>Runs inside FrostyTaskWindow</summary>
        public static ScanResult Scan(FsLocalizationStringDatabase db, IdDatabase idDb, FrostyTaskWindow task, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            idDb.EnsureLoaded();
            // Create a unified data bundle
            Context ctx = new Context
            {
                ValidHashes = new HashSet<uint>(db.EnumerateStrings()),
                Db = idDb,
                NewRefs = new Dictionary<uint, HashSet<string>>(),
                Token = token,
                Logger = task.TaskLogger,
                Task = task,
            };

            int total = (int)App.AssetManager.GetEbxCount();
            int index = 0;

            DebugLogHelper.Log("IdScanner.Scan", "Scanning {0} ebx asset(s) against {1} string hash(es)", total, ctx.ValidHashes.Count);

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
            {
                if (token.IsCancellationRequested)
                {
                    ctx.Result.Cancelled = true;
                    break;
                }

                index++;
                ctx.Task.Update(entry.Name);
                LocalizationHelper.ReportProgress(ctx.Logger, index, total, 1, TotalParts);

                // Type 3: A asset name or path.
                Consider(ctx, entry.Name, entry.Name);
                Consider(ctx, entry.Filename, entry.Name);
                Consider(ctx, entry.Path, entry.Name);
                // Type 4: A type name or enum member name.
                Consider(ctx, entry.Type, entry.Name);

                EbxAsset asset;
                try
                {
                    asset = App.AssetManager.GetEbx(entry);
                }
                catch (Exception ex)
                {
                    App.Logger.LogError("Read failed on {0}: {1}", entry.Name, ex.Message);
                    continue;
                }
                if (asset == null)
                    continue;

                ctx.Result.AssetsScanned++;

                try
                {
                    HashSet<object> visited = new HashSet<object>(ReferenceComparer.Instance);
                    foreach (object obj in asset.Objects)
                        WalkObject(ctx, obj, entry.Name, 0, visited);
                }
                catch (Exception ex)
                {
                    App.Logger.LogError("Walk failed on {0}: {1}", entry.Name, ex.Message);
                }
            }

            if (!ctx.Result.Cancelled)
                ScanSwfMovies(ctx, token);
            if (!ctx.Result.Cancelled)
                ExpandPatterns(ctx, token);

            ctx.Task.Update("Saving");
            LocalizationHelper.ReportProgress(ctx.Logger, 1, 1, TotalParts, TotalParts);

            foreach (KeyValuePair<uint, HashSet<string>> kvp in ctx.NewRefs)
                idDb.SetScanReferences(kvp.Key, kvp.Value);
            idDb.Save();

            stopwatch.Stop();
            App.Logger.Log("Scanned {0} asset(s) and {1} swf(s) in {2}s: {3} new ID(s), {4} reference(s), cancelled: {5}",
                ctx.Result.AssetsScanned, ctx.Result.SwfScanned, stopwatch.Elapsed.TotalSeconds.ToString("F1"), ctx.Result.IdsFound, ctx.NewRefs.Count, ctx.Result.Cancelled);

            return ctx.Result;
        }

        /// <summary>Compares by reference so the walk never re-visits a shared instance.</summary>
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>Inspects one ebx object (or nested struct value) through the cached candidate properties.</summary>
        private static void WalkObject(Context ctx, object obj, string assetName, int depth, HashSet<object> visited)
        {
            //if (obj == null || depth > MaxRecursionDepth || ctx.Token.IsCancellationRequested)
            if (obj == null || ctx.Token.IsCancellationRequested)
                return;

            // Ebx "struct" values are class instances; a shared or cyclic reference must not loop the walk.
            if (!obj.GetType().IsValueType && !visited.Add(obj))
                return;

            TypeProps props = GetTypeProps(ctx, obj.GetType(), assetName);
            if (props == null)
                return;

            if (props.CStrings != null)
            {
                foreach (PropertyInfo pi in props.CStrings)
                {
                    string text = (CString)pi.GetValue(obj);
                    Consider(ctx, text, assetName);
                    ConsiderScriptText(ctx, text, assetName);
                }
            }
            if (props.CStringLists != null)
            {
                foreach (PropertyInfo pi in props.CStringLists)
                {
                    if (pi.GetValue(obj) is List<CString> list)
                    {
                        foreach (CString cst in list)
                            Consider(ctx, cst, assetName);
                    }
                }
            }
            if (props.StringHashes != null)
            {
                foreach (PropertyInfo pi in props.StringHashes)
                {
                    // Non-FsLoc style reference: the hash is stored directly, no ID text to learn.
                    uint hash = (uint)pi.GetValue(obj);
                    if (ctx.ValidHashes.Contains(hash))
                        AddRef(ctx, hash, assetName);
                }
            }
            if (props.SubObjects != null)
            {
                foreach (PropertyInfo pi in props.SubObjects)
                    WalkObject(ctx, pi.GetValue(obj), assetName, depth + 1, visited);
            }
            if (props.SubObjectLists != null)
            {
                foreach (PropertyInfo pi in props.SubObjectLists)
                {
                    if (pi.GetValue(obj) is IList list)
                    {
                        foreach (object item in list)
                            WalkObject(ctx, item, assetName, depth + 1, visited);
                    }
                }
            }
        }

        /// <summary>
        /// Hash test a string, add it and <paramref name="refAssetName"/> to <paramref name="ctx"/> if vaild.
        /// </summary>
        private static void Consider(Context ctx, string text, string refAssetName)
        {
            if (string.IsNullOrEmpty(text))
                return;

            uint hash = LocalizationHelper.HashStringId(text);
            if (!ctx.ValidHashes.Contains(hash))
                return;

            if (ctx.Db.AddId(text))
                ctx.Result.IdsFound++;
            if (!string.IsNullOrEmpty(refAssetName))
                AddRef(ctx, hash, refAssetName);
        }

        /// <summary>
        /// Extract IDs from a script. Usually Lua script.
        /// </summary>
        /// <remarks>
        /// Long CStrings can be whole embedded scripts (SWBF2 stores Lua UI source that way).
        /// The IDs hide in quoted literals, so test those separately.
        /// </remarks>
        private static void ConsiderScriptText(Context ctx, string text, string refAssetName)
        {
            if (text == null || text.Length < 64)
                return;

            for (int i = 0; i < text.Length; i++)
            {
                char quote = text[i];
                if (quote != '"' && quote != '\'')
                    continue;

                int end = text.IndexOf(quote, i + 1);
                if (end < 0)
                    break;
                if (end - i > 1 && end - i <= 256)
                    Consider(ctx, text.Substring(i + 1, end - i - 1), refAssetName);
                i = end;
            }
        }

        private static void AddRef(Context ctx, uint hash, string assetName)
        {
            if (!ctx.NewRefs.TryGetValue(hash, out HashSet<string> paths))
            {
                paths = new HashSet<string>();
                ctx.NewRefs[hash] = paths;
            }
            if (paths.Add(assetName))
                ctx.Result.RefsFound++;
        }

        private static TypeProps GetTypeProps(Context ctx, Type type, string assetName)
        {
            if (ctx.PropCache.TryGetValue(type, out TypeProps cached))
                return cached;

            // Type 4: A type name or enum member name
            Consider(ctx, type.Name, assetName);

            TypeProps props = null;
            TypeProps Props()
            {
                return props ?? (props = new TypeProps());
            }

            foreach (PropertyInfo pi in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (pi.GetIndexParameters().Length > 0 || !pi.CanRead)
                    continue;

                Type pt = pi.PropertyType;

                if (pt == typeof(CString))
                {
                    TypeProps p = Props();
                    if (p.CStrings == null)
                        p.CStrings = new List<PropertyInfo>();
                    p.CStrings.Add(pi);
                }
                else if (pt == typeof(uint) && pi.Name == "StringHash")
                {
                    TypeProps p = Props();
                    if (p.StringHashes == null)
                        p.StringHashes = new List<PropertyInfo>();
                    p.StringHashes.Add(pi);
                }
                else if (pt.IsEnum)
                {
                    ConsiderEnumNames(ctx, pt, assetName);
                }
                else if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(List<>))
                {
                    Type itemType = pt.GetGenericArguments()[0];
                    if (itemType == typeof(CString))
                    {
                        TypeProps p = Props();
                        if (p.CStringLists == null)
                            p.CStringLists = new List<PropertyInfo>();
                        p.CStringLists.Add(pi);
                    }
                    else if (itemType.IsEnum)
                    {
                        ConsiderEnumNames(ctx, itemType, assetName);
                    }
                    else if (IsGameType(itemType, type))
                    {
                        TypeProps p = Props();
                        if (p.SubObjectLists == null)
                            p.SubObjectLists = new List<PropertyInfo>();
                        p.SubObjectLists.Add(pi);
                    }
                }
                else if (IsGameType(pt, type))
                {
                    TypeProps p = Props();
                    if (p.SubObjects == null)
                        p.SubObjects = new List<PropertyInfo>();
                    p.SubObjects.Add(pi);
                }
            }

            ctx.PropCache[type] = props;
            return props;
        }

        /// <summary>Test enum member names (Type 4)</summary>
        private static void ConsiderEnumNames(Context ctx, Type enumType, string assetName)
        {
            // Type 4: A type name or enum member name
            if (!ctx.SeenEnumTypes.Add(enumType))
                return;
            foreach (string name in Enum.GetNames(enumType))
                Consider(ctx, name, assetName);
        }

        /// <summary>
        /// Detect is <paramref name="type"/> is a game-defined <paramref name="declaringType"/>, not a FrostySdk or BCL type.
        /// </summary>
        private static bool IsGameType(Type type, Type declaringType)
        {
            return !type.IsEnum && !type.IsPrimitive && type.Assembly == declaringType.Assembly;
        }

        /// <summary>Decompress and extract printable strings from every SwfMovie resource and hash-tests them.</summary>
        private static void ScanSwfMovies(Context ctx, CancellationToken token)
        {
            List<ResAssetEntry> swfEntries = new List<ResAssetEntry>(App.AssetManager.EnumerateRes(resType: (uint)ResourceType.SwfMovie));
            int index = 0;

            foreach (ResAssetEntry resEntry in swfEntries)
            {
                if (token.IsCancellationRequested)
                {
                    ctx.Result.Cancelled = true;
                    return;
                }

                index++;
                ctx.Task.Update($"(SWF) {resEntry.Name}");
                LocalizationHelper.ReportProgress(ctx.Logger, index, swfEntries.Count, 2, TotalParts);

                byte[] data;
                try
                {
                    using (Stream stream = App.AssetManager.GetRes(resEntry))
                    using (MemoryStream ms = new MemoryStream())
                    {
                        if (stream == null)
                            continue;
                        stream.CopyTo(ms);
                        data = ms.ToArray();
                    }
                }
                catch
                {
                    continue;
                }

                data = DecompressSwf(data);
                if (data == null)
                    continue;

                ctx.Result.SwfScanned++;

                // A ref path only helps if it resolves to an ebx; the UI ebx usually shares the res name.
                string refName = App.AssetManager.GetEbxEntry(resEntry.Name) != null ? resEntry.Name : null;
                ExtractPrintableStrings(ctx, data, refName);
            }
        }

        /// <summary>
        /// Returns the raw flash body.
        /// Accepts leading zero bytes (res meta padding).
        /// </summary>
        /// <remarks>
        /// Plain SWF (FWS) and Scaleform GFx (GFX) pass through, CWS/CFX are zlib, ZWS/ZFX (lzma) are skipped.
        /// </remarks>
        private static byte[] DecompressSwf(byte[] data)
        {
            int offset = 0;
            while (offset < data.Length && offset < 16 && data[offset] == 0)
                offset++;
            if (data.Length - offset < 10)
                return null;

            byte s0 = data[offset];
            bool swfFamily = (data[offset + 1] == 'W' && data[offset + 2] == 'S')   // SWF
                          || (data[offset + 1] == 'F' && data[offset + 2] == 'X');  // Scaleform GFx

            if (!swfFamily)
                return null;

            if (s0 == 'F' || s0 == 'G')
            {
                if (offset == 0)
                    return data;
                byte[] body = new byte[data.Length - offset];
                Array.Copy(data, offset, body, 0, body.Length);
                return body;
            }

            if (s0 == 'C')
            {
                try
                {
                    // 8 byte header, then a zlib stream. Skip its 2 byte header for DeflateStream.
                    using (MemoryStream input = new MemoryStream(data, offset + 10, data.Length - offset - 10))
                    using (DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress))
                    using (MemoryStream output = new MemoryStream())
                    {
                        deflate.CopyTo(output);
                        return output.ToArray();
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Extract printable strings from raw flash data.
        /// </summary>
        private static void ExtractPrintableStrings(Context ctx, byte[] data, string refName)
        {
            StringBuilder run = new StringBuilder();
            for (int i = 0; i <= data.Length; i++)
            {
                byte b = i < data.Length ? data[i] : (byte)0;
                if (b >= 0x20 && b <= 0x7E)
                {
                    run.Append((char)b);
                    continue;
                }

                if (run.Length >= 2)
                    Consider(ctx, run.ToString(), refName);
                run.Clear();
            }
        }

        /// <summary>
        /// Generates candidates from the IDs found so far and hash-tests them.
        /// Numeric families (TIP_114 implies TIP_115) and common suffix swaps (X_TITLE implies X_DESCRIPTION).
        /// Repeats while new IDs keep seeding new candidates.
        /// </summary>
        private static void ExpandPatterns(Context ctx, CancellationToken token)
        {
            const int MaxRounds = 5;
            const int MaxNumber = 1000;
            const int MinTokenUses = 5; // How many times a suffix was used before it's considered common enough to swap.

            for (int round = 0; round < MaxRounds; round++)
            {
                ctx.Task.Update("Expanding ID patterns");
                LocalizationHelper.ReportProgress(ctx.Logger, round + 1, MaxRounds, 3, TotalParts);
                int before = ctx.Result.IdsFound;

                List<string> seeds = new List<string>();
                foreach (KeyValuePair<uint, string> kvp in ctx.Db.EnumerateIds())
                    seeds.Add(kvp.Value);

                // Numeric families: strip trailing digits, regenerate the range in seen paddings.
                HashSet<string> numericBases = new HashSet<string>();
                foreach (string seed in seeds)
                {
                    int end = seed.Length;
                    while (end > 0 && char.IsDigit(seed[end - 1]))
                        end--;
                    if (end < seed.Length && end > 0)
                        numericBases.Add(seed.Substring(0, end));
                }
                foreach (string numericBase in numericBases)
                {
                    if (token.IsCancellationRequested)
                    {
                        ctx.Result.Cancelled = true;
                        return;
                    }
                    for (int n = 0; n < MaxNumber; n++)
                    {
                        Consider(ctx, numericBase + n, null);
                        if (n < 10)
                            Consider(ctx, numericBase + n.ToString("D1"), null); // TIP_1
                        if (n < 100)
                            Consider(ctx, numericBase + n.ToString("D2"), null); // TIP_01
                        Consider(ctx, numericBase + n.ToString("D3"), null);     // TIP_001
                    }
                }

                // Suffix tokens common across the corpus (TITLE, DESCRIPTION, OBJ, ...).
                Dictionary<string, int> tokenCounts = new Dictionary<string, int>();
                foreach (string seed in seeds)
                {
                    int us = seed.LastIndexOf('_');
                    if (us <= 0 || us == seed.Length - 1)
                        continue;
                    string tok = seed.Substring(us + 1);
                    tokenCounts.TryGetValue(tok, out int c);
                    tokenCounts[tok] = c + 1;
                }
                List<string> commonTokens = new List<string>();
                foreach (KeyValuePair<string, int> kvp in tokenCounts)
                {
                    if (kvp.Value >= MinTokenUses && !char.IsDigit(kvp.Key[0]))
                        commonTokens.Add(kvp.Key);
                }

                foreach (string seed in seeds)
                {
                    if (token.IsCancellationRequested)
                    {
                        ctx.Result.Cancelled = true;
                        return;
                    }
                    int us = seed.LastIndexOf('_');
                    string stem = us > 0 ? seed.Substring(0, us) : seed;
                    foreach (string tok in commonTokens)
                    {
                        Consider(ctx, seed + "_" + tok, null);
                        if (us > 0)
                            Consider(ctx, stem + "_" + tok, null);
                    }
                    if (us > 0)
                        Consider(ctx, stem, null);
                }

                int gained = ctx.Result.IdsFound - before;
                DebugLogHelper.Log("IdScanner.ExpandPatterns", "Round {0}: {1} ID(s) from pattern expansion", round + 1, gained);
                if (gained == 0)
                    break;
            }
        }
    }
}
