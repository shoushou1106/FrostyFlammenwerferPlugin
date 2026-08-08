using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FsLocalizationPlugin.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

namespace FsLocalizationPlugin.Resources
{
    /// <summary>
    /// What both databases store for one hash.
    /// The hash is the key, the ID text and the references come after it,
    /// and either can be empty.
    /// </summary>
    public sealed class IdEntry
    {
        /// <summary>The ID text this hash was made from. Empty when only references are known.</summary>
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>Asset paths that use this string.</summary>
        [JsonProperty("refs")]
        public List<string> References { get; } = new List<string>();

        /// <summary>Nothing left to store. The stores drop these.</summary>
        [JsonIgnore]
        public bool IsEmpty => Id.Length == 0 && References.Count == 0;

        /// <summary>Cleans up what a partial or hand-edited document left out.</summary>
        internal IdEntry Normalize()
        {
            Id = Id ?? string.Empty;
            References.RemoveAll(string.IsNullOrEmpty);
            return this;
        }

        /// <summary>Takes what this entry is missing from another. Returns whether it gained anything.</summary>
        internal bool FillFrom(IdEntry other)
        {
            bool changed = false;

            if (Id.Length == 0 && other.Id.Length > 0)
            {
                Id = other.Id;
                changed = true;
            }
            foreach (string path in other.References)
            {
                if (!References.Contains(path))
                {
                    References.Add(path);
                    changed = true;
                }
            }
            return changed;
        }
    }

    /// <summary>
    /// The document both databases read and write. Entries keyed by hash in hex.
    /// </summary>
    /// <remarks>
    /// The cached database's file, the project database's payload
    /// and any exported file all share this shape,
    /// so one importer reads all three and either database can take the other's file.
    /// </remarks>
    public sealed class IdDocument
    {
        public const int CurrentVersion = 1;

        private const string DocumentNote = "Flammenwerfer ID Database. Share and merge freely.";

        /// <summary>Exported and cached files are meant to be read and diffed by people.</summary>
        private static readonly JsonSerializerSettings FileJson = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
        };

        /// <summary>
        /// Frosty reads an asset's UserData back one byte per char,
        /// so a payload stored there has to stay ASCII.
        /// Escaping non-ASCII keeps CJK and emoji round-tripping exactly.
        /// </summary>
        private static readonly JsonSerializerSettings AsciiJson = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
        };

        [JsonProperty("version")]
        public int Version { get; set; } = CurrentVersion;

        [JsonProperty("note")]
        public string Note { get; set; } = DocumentNote;

        [JsonProperty("game")]
        public string Game { get; set; } = string.Empty;

        [JsonProperty("entries")]
        public Dictionary<string, IdEntry> Entries { get; } = new Dictionary<string, IdEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Snapshots entries into a document.
        /// Sorted by ID text, so two people scanning the same game produce comparable files
        /// and a project's payload diffs cleanly between saves.
        /// </summary>
        internal static IdDocument FromEntries(IEnumerable<KeyValuePair<uint, IdEntry>> entries)
        {
            IdDocument document = new IdDocument { Game = ProfilesLibrary.ProfileName };
            foreach (KeyValuePair<uint, IdEntry> kvp in entries.OrderBy(kvp => kvp.Value.Id, StringComparer.Ordinal).ThenBy(kvp => kvp.Key))
                document.Entries.Add(kvp.Key.ToString("x8", CultureInfo.InvariantCulture), kvp.Value);
            return document;
        }

        /// <summary>The entries whose key is a real hash, normalized. Junk keys are skipped.</summary>
        internal IEnumerable<KeyValuePair<uint, IdEntry>> ReadEntries()
        {
            foreach (KeyValuePair<string, IdEntry> kvp in Entries)
            {
                if (kvp.Value != null && uint.TryParse(kvp.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash))
                    yield return new KeyValuePair<uint, IdEntry>(hash, kvp.Value.Normalize());
            }
        }

        /// <summary>
        /// Reads a document from text.
        /// </summary>
        /// <exception cref="FormatException">The document is unrecognized.</exception>
        internal static IdDocument Parse(string text, string source)
        {
            IdDocument document = JsonConvert.DeserializeObject<IdDocument>(text);
            if (document?.Entries == null)
                throw new FormatException("Not a Flammenwerfer ID database.");

            if (document.Version > CurrentVersion)
                DebugLogHelper.Log("IdDocument.Parse", "{0} is version {1}, newer than this build reads", source, document.Version);
            if (document.Game.Length > 0 && document.Game != ProfilesLibrary.ProfileName)
                DebugLogHelper.Log("IdDocument.Parse", "{0} was made for {1}", source, document.Game);
            return document;
        }

        /// <summary>Tells JSON from the plain one-ID-per-line text without copying the whole file.</summary>
        internal static bool LooksLikeJson(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsWhiteSpace(c))
                    return c == '{';
            }
            return false;
        }

        /// <summary>
        /// The document as text. <paramref name="ascii"/> picks the compact ASCII-escaped form an
        /// asset's UserData needs over the indented form a file gets.
        /// </summary>
        internal string Serialize(bool ascii)
        {
            return JsonConvert.SerializeObject(this, ascii ? AsciiJson : FileJson);
        }
    }

    /// <summary>
    /// The front door to both ID databases. Reads them as one,
    /// routes writes to whichever owns a hash,
    /// and is where a shared file is imported.
    /// </summary>
    public static class IdIndex
    {
        #region -- Reading --
        /// <summary>Whether either database knows this hash.</summary>
        public static bool Contains(uint hash)
        {
            return TryGet(hash, out string _);
        }

        /// <summary>Resolves a hash to its ID text, project database first.</summary>
        public static bool TryGet(uint hash, out string id)
        {
            if (ProjectIdDatabase.TryGetId(hash, out id))
                return true;

            IdDatabase.Instance.EnsureLoaded();
            return IdDatabase.Instance.TryGetId(hash, out id);
        }

        /// <summary>
        /// Every known hash-to-ID pair, each hash yielded once.
        /// </summary>
        /// <remarks>
        /// Writing routes a hash to exactly one database,
        /// so the two normally do not overlap and the dedupe set is never even allocated.
        /// It is still needed because two paths get around the routing:
        /// <list type="bullet">
        /// <item>Importing a file that names a hash both databases already hold</item>
        /// <item>A hash that is an original string in one language but not in another,
        /// which routes differently depending on the language loaded at the time</item>
        /// </list>
        /// </remarks>
        public static IEnumerable<KeyValuePair<uint, string>> Enumerate()
        {
            HashSet<uint> fromProject = null;
            foreach (KeyValuePair<uint, string> kvp in ProjectIdDatabase.EnumerateIds())
            {
                (fromProject ?? (fromProject = new HashSet<uint>())).Add(kvp.Key);
                yield return kvp;
            }

            IdDatabase.Instance.EnsureLoaded();
            foreach (KeyValuePair<uint, string> kvp in IdDatabase.Instance.EnumerateIds())
            {
                if (fromProject == null || !fromProject.Contains(kvp.Key))
                    yield return kvp;
            }
        }

        #endregion

        #region -- Routing --
        // Routing Rule:
        // - A game string's ID is game knowledge and belongs in the shared cached database
        // - Anything else belongs to the project database

        /// <summary>
        /// Whether the hash is one of the game's own strings rather than one the project added.
        /// </summary>
        public static bool IsGameString(uint hash)
        {
            return LocalizedStringDatabase.Current is FsLocalizationStringDatabase db
                && db.TryGetOriginalString(hash, out string _);
        }

        /// <summary>Stores an ID text under its hash, in whichever database owns it. Empty text clears it.</summary>
        public static void Set(uint hash, string id)
        {
            if (IsGameString(hash))
            {
                IdDatabase.Instance.EnsureLoaded();
                if (IdDatabase.Instance.SetId(hash, id))
                    IdDatabase.Instance.Save();
            }
            else
            {
                ProjectIdDatabase.SetId(hash, id);
            }
        }

        /// <summary>
        /// Records the ID text a string was just added or changed under.
        /// </summary>
        /// <remarks>
        /// Called with the ID as typed, the only moment it exists.
        /// Everywhere else in the pipeline a string is just a hash.
        /// <para>
        /// An ID can be any text at all. The cached database records whatever the game uses,
        /// and the project database records whatever the creator chose,
        /// so nothing here judges the shape of the text.
        /// </para>
        /// </remarks>
        public static void Record(string idText, uint hash)
        {
            if (string.IsNullOrEmpty(idText))
                return;

            Set(hash, idText);
        }

        /// <summary>
        /// Reports that a string was reverted or removed.
        /// A game string's ID stays in the cached database,
        /// since the game still has the string,
        /// so only the project database prunes.
        /// </summary>
        public static void Forget(uint hash)
        {
            if (!IsGameString(hash))
                ProjectIdDatabase.PruneIfUnused(hash);
        }

        #endregion

        #region -- Importing --
        /// <summary>
        /// Merges a shared ID file into both databases.
        /// Each hash going wherever it belongs.
        /// Accepts a JSON database and a plain one-ID-per-line text.
        /// Log what it did.
        /// </summary>
        /// <exception cref="FormatException">The document is unrecognized.</exception>
        public static void ImportFile(string path)
        {
            string text = File.ReadAllText(path);
            if (IdDocument.LooksLikeJson(text))
                ImportJson(text);
            else
                Import(ReadIdLines(text));
        }

        /// <summary>
        /// Merges an exported database document.
        /// Stored IDs win and references are unioned,
        /// so importing the same text twice changes nothing.
        /// </summary>
        /// <exception cref="FormatException">The document is unrecognized.</exception>
        public static void ImportJson(string json)
        {
            IdDocument document = IdDocument.Parse(json, "the imported document");
            if (document.Game.Length > 0 && document.Game != ProfilesLibrary.ProfileName)
            {
                App.Logger.LogWarning("This file was made for {0}, not {1}. Anything it names that is not a string of this game lands in the project database.",
                    document.Game, ProfilesLibrary.ProfileName);
            }

            Import(document.ReadEntries());
        }

        /// <summary>Splits entries between the two databases and merges each in one go.</summary>
        private static void Import(IEnumerable<KeyValuePair<uint, IdEntry>> entries)
        {
            IdDatabase.Instance.EnsureLoaded();

            Dictionary<uint, IdEntry> forProject = new Dictionary<uint, IdEntry>();
            int skipped = 0;
            int cached = 0;

            foreach (KeyValuePair<uint, IdEntry> kvp in entries)
            {
                if (IsGameString(kvp.Key))
                    cached += IdDatabase.Instance.Merge(kvp.Key, kvp.Value) ? 1 : 0;
                else if (ProjectIdDatabase.Enabled)
                    forProject[kvp.Key] = kvp.Value;
                else
                    skipped++;
            }

            if (cached > 0)
                IdDatabase.Instance.Save();
            int project = ProjectIdDatabase.Merge(forProject);

            App.Logger.Log("Import complete: {0} into the cached database, {1} into the project database, {2} skipped", cached, project, skipped);
            if (skipped > 0)
                App.Logger.Log("Skipped entries are strings this game does not have. Turn on the project ID database to keep them.");
        }

        /// <summary>Reads the plain format. One ID per line, '#' comments out a line.</summary>
        private static IEnumerable<KeyValuePair<uint, IdEntry>> ReadIdLines(string text)
        {
            using (StringReader reader = new StringReader(text))
            {
                string id;
                while ((id = reader.ReadLine()) != null)
                {
                    if (id.Length == 0 || id.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    yield return new KeyValuePair<uint, IdEntry>(LocalizationHelper.HashStringId(id), new IdEntry { Id = id });
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// The Cached ID Database. Everything known about the game's own strings.
    /// </summary>
    /// <remarks>
    /// Saved in a JSON file in Frosty's Caches folder,
    /// so it can be shared as a single file.
    /// IDs are hashed from their own text,
    /// so a shared file merges into any copy of the same game.
    /// Editor only. Never loads under Frosty Mod Manager.
    /// </remarks>
    public sealed class IdDatabase
    {
        private static IdDatabase instance;

        private readonly Dictionary<uint, IdEntry> entries = new Dictionary<uint, IdEntry>();
        private readonly object sync = new object();
        private bool isLoaded;

        /// <summary>
        /// The profile the loaded data belongs to.
        /// </summary>
        private string loadedProfile;

        /// <summary>Raised after the data changed. UI refresh hook.</summary>
        public event Action Changed;

        public static IdDatabase Instance => instance ?? (instance = new IdDatabase());

#if FROSTY_107
        private static string CachePrefix => App.FileSystemManager.CacheName;
#else
        private static string CachePrefix => App.FileSystem.CacheName;
#endif

        /// <summary>The shareable file, inside Frosty's Caches folder.</summary>
        public static string FilePath => CachePrefix + "_Flammenwerfer_CachedIdDatabase.json";

        /// <summary>How many hashes the database knows anything about.</summary>
        public int Count
        {
            get
            {
                lock (sync)
                    return entries.Count;
            }
        }

        #region -- Lifecycle --
        /// <summary>Loads the file once per profile. A missing or corrupt file leaves an empty database.</summary>
        public void EnsureLoaded()
        {
            lock (sync)
            {
                if (App.PluginManager.IsManagerType(PluginManagerType.ModManager))
                    return;

                if (isLoaded)
                {
                    if (loadedProfile == ProfilesLibrary.ProfileName)
                        return;

                    DebugLogHelper.Log("IdDatabase.EnsureLoaded", "Profile changed from {0}, reloading", loadedProfile);
                    entries.Clear();
                    isLoaded = false;
                }

                LoadFile();
                loadedProfile = ProfilesLibrary.ProfileName;
                isLoaded = true;
            }
        }

        /// <summary>Re-reads the file, discarding anything unsaved. For the editor tab's file watcher.</summary>
        public void Reload()
        {
            lock (sync)
            {
                if (!isLoaded)
                    return;
                entries.Clear();
                isLoaded = false;
            }
            EnsureLoaded();
            Changed?.Invoke();
        }

        /// <summary>Writes the file and raises Changed. Callers batch their edits and save once.</summary>
        public void Save()
        {
            lock (sync)
            {
                if (!isLoaded)
                    return;

                // The path follows the current profile. Never write one game's IDs into another's file.
                if (loadedProfile != ProfilesLibrary.ProfileName)
                {
                    DebugLogHelper.Log("IdDatabase.Save", "Skipped. Data belongs to profile {0}", loadedProfile);
                    return;
                }

                WriteFile();
            }

            Changed?.Invoke();
        }

        #endregion

        #region -- Reading --
        public bool Contains(uint hash)
        {
            lock (sync)
                return entries.ContainsKey(hash);
        }

        public bool TryGetId(uint hash, out string id)
        {
            lock (sync)
            {
                id = entries.TryGetValue(hash, out IdEntry entry) ? entry.Id : null;
                return !string.IsNullOrEmpty(id);
            }
        }

        public IReadOnlyList<string> GetReferences(uint hash)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(hash, out IdEntry entry))
                    return Array.Empty<string>();
                return entry.References.ToList();
            }
        }

        /// <summary>Hash-to-ID pairs, skipping hashes that are only known by reference.</summary>
        public IEnumerable<KeyValuePair<uint, string>> EnumerateIds()
        {
            lock (sync)
            {
                List<KeyValuePair<uint, string>> result = new List<KeyValuePair<uint, string>>(entries.Count);
                foreach (KeyValuePair<uint, IdEntry> kvp in entries)
                {
                    if (kvp.Value.Id.Length > 0)
                        result.Add(new KeyValuePair<uint, string>(kvp.Key, kvp.Value.Id));
                }
                return result;
            }
        }

        /// <summary>Every entry, ID and references together.</summary>
        /// <returns>A snapshot, so it stays safe to walk while the database is edited.</returns>
        public IEnumerable<KeyValuePair<uint, IdEntry>> EnumerateEntries()
        {
            lock (sync)
                return new List<KeyValuePair<uint, IdEntry>>(entries);
        }

        #endregion

        #region -- Writing --
        /// <summary>
        /// Sets the ID text for a hash, empty clears it.
        /// Returns whether anything changed.
        /// Callers decide when to <see cref="Save"/>.
        /// </summary>
        public bool SetId(uint hash, string id)
        {
            id = id ?? string.Empty;
            lock (sync)
            {
                if (!entries.TryGetValue(hash, out IdEntry entry))
                {
                    if (id.Length == 0)
                        return false;

                    entries.Add(hash, new IdEntry { Id = id });
                    return true;
                }

                if (string.Equals(entry.Id, id, StringComparison.Ordinal))
                    return false;

                entry.Id = id;
                if (entry.IsEmpty)
                    entries.Remove(hash);
                return true;
            }
        }

        /// <summary>
        /// Fills in an ID text only when the hash has none.
        /// The scan uses this, so the first text it finds for a hash wins
        /// and a later text that happens to hash the same cannot replace it.
        /// </summary>
        public bool AddId(uint hash, string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            lock (sync)
            {
                IdEntry entry = GetOrAdd(hash);
                if (entry.Id.Length > 0)
                    return false;

                entry.Id = id;
                return true;
            }
        }

        /// <summary>Records a reference. Returns false when the hash already carries that path.</summary>
        public bool AddReference(uint hash, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            lock (sync)
            {
                IdEntry entry = GetOrAdd(hash);
                if (entry.References.Contains(assetPath))
                    return false;

                entry.References.Add(assetPath);
                return true;
            }
        }

        /// <summary>
        /// Adds every reference the scan found for a hash.
        /// References are only ever added, never replaced.
        /// </summary>
        public void AddReferences(uint hash, IEnumerable<string> assetPaths)
        {
            lock (sync)
            {
                IdEntry entry = GetOrAdd(hash);
                foreach (string path in assetPaths)
                {
                    if (!string.IsNullOrEmpty(path) && !entry.References.Contains(path))
                        entry.References.Add(path);
                }
            }
        }

        public bool RemoveReference(uint hash, string assetPath)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(hash, out IdEntry entry))
                    return false;

                if (entry.References.RemoveAll(r => r == assetPath) == 0)
                    return false;
                if (entry.IsEmpty)
                    entries.Remove(hash);
                return true;
            }
        }

        /// <summary>
        /// Takes what one entry knows that this database does not.
        /// </summary>
        /// <returns>Whether it gained anything.</returns>
        public bool Merge(uint hash, IdEntry incoming)
        {
            if (incoming == null)
                return false;

            lock (sync)
            {
                if (!entries.TryGetValue(hash, out IdEntry stored))
                {
                    if (incoming.IsEmpty)
                        return false;

                    entries.Add(hash, incoming);
                    return true;
                }
                return stored.FillFrom(incoming);
            }
        }

        /// <summary>Forgets a hash completely, references included. A later scan can find it again.</summary>
        public bool RemoveEntry(uint hash)
        {
            lock (sync)
                return entries.Remove(hash);
        }

        /// <summary>Empties the database. The file is only rewritten when the caller saves.</summary>
        public void Clear()
        {
            lock (sync)
                entries.Clear();
        }

        private IdEntry GetOrAdd(uint hash)
        {
            if (!entries.TryGetValue(hash, out IdEntry entry))
            {
                entry = new IdEntry();
                entries.Add(hash, entry);
            }
            return entry;
        }

        #endregion

        #region -- File --
        /// <summary>The database as a shareable document, the same text the Caches file holds.</summary>
        public string ExportJson()
        {
            lock (sync)
                return IdDocument.FromEntries(entries).Serialize(ascii: false);
        }

        /// <summary>A corrupt file leaves the database empty rather than being silently replaced.</summary>
        private void LoadFile()
        {
            if (!File.Exists(FilePath))
                return;

            try
            {
                foreach (KeyValuePair<uint, IdEntry> kvp in IdDocument.Parse(File.ReadAllText(FilePath), FilePath).ReadEntries())
                    entries[kvp.Key] = kvp.Value;

                DebugLogHelper.Log("IdDatabase.LoadFile", "Loaded {0} entrie(s) from {1}", entries.Count, FilePath);
            }
            catch (Exception ex)
            {
                entries.Clear();
                App.Logger.LogError("The ID database could not be read, starting empty: {0}", ex.Message);
            }
        }

        private void WriteFile()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath)));
                File.WriteAllText(FilePath, IdDocument.FromEntries(entries).Serialize(ascii: false));
                DebugLogHelper.Log("IdDatabase.WriteFile", "Saved {0} entrie(s)", entries.Count);
            }
            catch (Exception ex)
            {
                App.Logger.LogError("Failed to save the ID database: {0}", ex.Message);
            }
        }

        #endregion
    }

    /// <summary>
    /// The project ID database. What the creator records about the strings their project added.
    /// Unlike the string edits themselves, this is not per-language.
    /// It lives in ONE dedicated added EBX asset with a generic root object (every Frostbite SDK has one),
    /// and keeps its payload as JSON in that entry's UserData.
    /// Frosty saves UserData with the project and vanilla FsLocalizationPlugin never looks at it,
    /// so projects stay loadable there.
    /// IsTransientModified keeps the asset in the project file but out of exported mods.
    /// </summary> 
    public static class ProjectIdDatabase
    {
        /// <summary>
        /// The carrier asset, at the root of the ebx tree so it is easy to find
        /// and never buried in a game's locale folder.
        /// </summary>
        private const string AssetName = "Flammenwerfer/ProjectIdDatabase";

        /// <summary>The asset's Name field, the one thing someone opening it in the editor sees.</summary>
        private const string AssetNote = "Flammenwerfer Project ID Database. Stays alive even when Flammenwerfer is not installed, and never exported to a mod file. To prevent Flammenwerfer creating this, disable in Tools > Options > Flammenwerfer Options";

        private const string EnabledOption = "Flammenwerfer_ProjectIdDatabaseEnabled";

        /// <summary>Root object type for the carrier asset, the first one the game has wins.</summary>
        private static readonly string[] RootTypes = { "Asset", "DataContainer" };

        // Parse cache, keyed by the exact text it came from so it can never serve stale data.
        private static string parsedFrom;
        private static Dictionary<uint, IdEntry> parsedIds;

        private static bool? enabledCache;
        private static string enabledProfile;

        /// <summary>
        /// The "Project ID Database" option, per game.
        /// Creators may prefer tracking IDs the traditional way
        /// rather than have an extra asset appear in their project.
        /// <para>
        /// Cached, and set from FlammenwerferOptions.
        /// The profile it was read for is remembered too,
        /// the option is per game while the cache may not.
        /// </para>
        /// Only writes are gated. Reads stay live, so IDs already in a project keep resolving.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (!enabledCache.HasValue || enabledProfile != ProfilesLibrary.ProfileName)
                {
                    enabledCache = Config.Get(EnabledOption, true, ConfigScope.Game);
                    enabledProfile = ProfilesLibrary.ProfileName;
                    DebugLogHelper.Log("ProjectIdDatabase.Enabled", "Read the option for {0}: {1}", enabledProfile, enabledCache.Value);
                }
                return enabledCache.Value;
            }
            set
            {
                enabledCache = value;
                enabledProfile = ProfilesLibrary.ProfileName;
            }
        }

        /// <summary>How many hashes the project stores anything for.</summary>
        public static int Count => Read().Count;

        #region -- Reading --
        /// <summary>
        /// Whether the database holds this hash.
        /// Reads the asset manager every call to always get newest data.
        /// </summary>
        public static bool Contains(uint hash) => Read().ContainsKey(hash);

        public static bool TryGetId(uint hash, out string id)
        {
            id = null;
            if (Read().TryGetValue(hash, out IdEntry entry) && entry.Id.Length > 0)
            {
                id = entry.Id;
                return true;
            }
            return false;
        }

        public static IReadOnlyList<string> GetReferences(uint hash)
        {
            return Read().TryGetValue(hash, out IdEntry entry) ? entry.References : (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>Hash-to-ID pairs, skipping entries that carry no ID text.</summary>
        public static IEnumerable<KeyValuePair<uint, string>> EnumerateIds()
        {
            foreach (KeyValuePair<uint, IdEntry> kvp in Read())
            {
                if (kvp.Value.Id.Length > 0)
                    yield return new KeyValuePair<uint, string>(kvp.Key, kvp.Value.Id);
            }
        }

        /// <summary>Every entry, ID and references together. Safe to walk while editing.</summary>
        public static IEnumerable<KeyValuePair<uint, IdEntry>> EnumerateEntries()
        {
            return new List<KeyValuePair<uint, IdEntry>>(Read());
        }

        #endregion

        #region -- Writing --
        /// <summary>
        /// Sets the ID text for a hash.
        /// Never creates the asset just to clear.
        /// </summary>
        public static void SetId(uint hash, string id)
        {
            id = id ?? string.Empty;
            if (!Enabled && id.Length > 0)
                return;

            Mutate(create: id.Length > 0, ids =>
            {
                if (id.Length > 0)
                    GetOrAdd(ids, hash).Id = id;
                else if (ids.TryGetValue(hash, out IdEntry entry))
                    Drop(ids, hash, entry, () => entry.Id = string.Empty);
            });
        }

        public static void AddReference(uint hash, string assetPath)
        {
            if (!Enabled || string.IsNullOrEmpty(assetPath))
                return;

            Mutate(create: true, ids =>
            {
                List<string> references = GetOrAdd(ids, hash).References;
                if (!references.Contains(assetPath))
                    references.Add(assetPath);
            });
        }

        public static void RemoveReference(uint hash, string assetPath)
        {
            Mutate(create: false, ids =>
            {
                if (ids.TryGetValue(hash, out IdEntry entry))
                    Drop(ids, hash, entry, () => entry.References.Remove(assetPath));
            });
        }

        /// <summary>
        /// Takes what an imported document knows that the project does not, in one write.
        /// Returns how many entries were added or filled in.
        /// </summary>
        public static int Merge(Dictionary<uint, IdEntry> incoming)
        {
            if (!Enabled || incoming == null || incoming.Count == 0)
                return 0;

            int changed = 0;
            Mutate(create: true, ids =>
            {
                foreach (KeyValuePair<uint, IdEntry> kvp in incoming)
                {
                    if (!ids.TryGetValue(kvp.Key, out IdEntry stored))
                    {
                        if (kvp.Value.IsEmpty)
                            continue;

                        ids.Add(kvp.Key, kvp.Value);
                        changed++;
                    }
                    else if (stored.FillFrom(kvp.Value))
                    {
                        changed++;
                    }
                }
            });
            return changed;
        }

        /// <summary>Drops the whole entry: ID and references together.</summary>
        public static void RemoveEntry(uint hash)
        {
            Mutate(create: false, ids => ids.Remove(hash));
        }

        /// <summary>Drops an entry once its string is gone from every language.</summary>
        /// <remarks>
        /// The same ID can be added in several languages,
        /// so removing it from one of them is not enough to retire the entry.
        /// Only when no language still carries the string
        /// do its ID and references become meaningless.
        /// </remarks>
        public static void PruneIfUnused(uint hash)
        {
            if (!Contains(hash))
                return;

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(type: "UITextDatabase", modifiedOnly: true))
            {
                if (entry.IsAdded)
                    continue;
                if (entry.ModifiedEntry?.DataObject is ModifiedFsLocalizationAsset diff && diff.strings.ContainsKey(hash))
                    return;
            }

            DebugLogHelper.Log("ProjectIdDatabase.PruneIfUnused", "String {0:X8} left the project, dropping its entry", hash);
            RemoveEntry(hash);
        }

        /// <summary>Empties the database, which reverts the asset out of the project.</summary>
        public static void Clear()
        {
            Mutate(create: false, ids => ids.Clear());
        }

        private static IdEntry GetOrAdd(Dictionary<uint, IdEntry> ids, uint hash)
        {
            if (!ids.TryGetValue(hash, out IdEntry entry))
            {
                entry = new IdEntry();
                ids.Add(hash, entry);
            }
            return entry;
        }

        /// <summary>Applies a change that can empty an entry, and drops the entry when it does.</summary>
        private static void Drop(Dictionary<uint, IdEntry> ids, uint hash, IdEntry entry, Action change)
        {
            change();
            if (entry.IsEmpty)
                ids.Remove(hash);
        }

        #endregion

        #region -- UserData Payload --
        /// <summary>The stored entries. Only <see cref="Mutate"/> is allowed to change them.</summary>
        private static Dictionary<uint, IdEntry> Read() => Read(FindEntry());

        private static Dictionary<uint, IdEntry> Read(EbxAssetEntry entry)
        {
            string json = entry?.ModifiedEntry?.UserData ?? string.Empty;
            if (parsedIds != null && parsedFrom == json)
                return parsedIds;

            Dictionary<uint, IdEntry> ids = new Dictionary<uint, IdEntry>();
            if (json.Length > 0)
            {
                try
                {
                    foreach (KeyValuePair<uint, IdEntry> kvp in IdDocument.Parse(json, AssetName).ReadEntries())
                        ids[kvp.Key] = kvp.Value;
                }
                catch (Exception ex)
                {
                    App.Logger.LogError("The project ID database could not be read: {0}", ex.Message);
                }
            }

            parsedFrom = json;
            parsedIds = ids;
            return ids;
        }

        /// <summary>Runs one change, writes the payload back, and reverts the asset once nothing is left.</summary>
        private static void Mutate(bool create, Action<Dictionary<uint, IdEntry>> change)
        {
            EbxAssetEntry entry = create ? GetOrCreateEntry() : FindEntry();
            if (entry == null)
                return;

            Dictionary<uint, IdEntry> ids = Read(entry);
            change(ids);

            if (ids.Count == 0)
            {
                DebugLogHelper.Log("ProjectIdDatabase.Mutate", "Database empty, reverting {0}", entry.Name);
                App.AssetManager.RevertAsset(entry);
                parsedFrom = null;
                parsedIds = null;

                // The asset is out of the manager, but not out of the tree the explorer already built.
                RefreshExplorer();
                return;
            }

            string json = IdDocument.FromEntries(ids).Serialize(ascii: true);
            entry.ModifiedEntry.UserData = json;
            parsedFrom = json;
            parsedIds = ids;
            MarkDirty(entry);


            DebugLogHelper.Log("ProjectIdDatabase.Mutate", "Wrote {0} entrie(s), {1} char(s) of UserData", ids.Count, json.Length);
        }

        /// <summary>The database as a shareable document.</summary>
        public static string ExportJson()
        {
            Dictionary<uint, IdEntry> ids = Read();
            return ids.Count == 0 ? string.Empty : IdDocument.FromEntries(ids).Serialize(ascii: false);
        }

        #endregion

        #region -- Carrier Asset --
        /// <summary>Finds the carrier asset, or null when this project has none.</summary>
        private static EbxAssetEntry FindEntry()
        {
            EbxAssetEntry entry = App.AssetManager.GetEbxEntry(AssetName);
            if (entry == null || !entry.IsAdded || !(entry.ModifiedEntry?.DataObject is EbxAsset))
                return null;

            // Double insurance. Project ID Database must never reach an exported mod.
            entry.ModifiedEntry.IsTransientModified = true;
            return entry;
        }

        private static EbxAssetEntry GetOrCreateEntry()
        {
            EbxAssetEntry entry = FindEntry();
            if (entry != null)
                return entry;

            try
            {
                entry = App.AssetManager.GetEbxEntry(AssetName);
                if (entry == null)
                {
                    // Create-new pattern from DuplicationPlugin.
                    entry = App.AssetManager.AddEbx(AssetName, CreateCarrierAsset(Guid.NewGuid()));
                }
                else
                {
                    // Left over from a revert that only cleared the data. Rebuild it in place.
                    entry.ModifiedEntry = new ModifiedAssetEntry { DataObject = CreateCarrierAsset(entry.Guid) };
                    entry.IsAdded = true;
                }

                entry.ModifiedEntry.IsTransientModified = true;
                MarkDirty(entry);
                RefreshExplorer();

                App.Logger.Log("Created {0} to hold this project's string IDs. It is never exported into a mod, and reverting it deletes whole project ID database.", AssetName);
                return entry;
            }
            catch (Exception ex)
            {
                App.Logger.LogError("Failed to create the project ID database asset: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// The carrier's ebx.
        /// Real ebx data is what lets Frosty open the asset in the editor
        /// instead of failing to read data that was never written, and leads to fatal crash.
        /// </summary>
        private static EbxAsset CreateCarrierAsset(Guid fileGuid)
        {
            EbxAsset asset = new EbxAsset(CreateRootObject());
            asset.SetFileGuid(fileGuid);

            dynamic root = asset.RootObject;
            Type rootType = (Type)root.GetType();
            root.SetInstanceGuid(new AssetClassGuid(Utils.GenerateDeterministicGuid(asset.Objects, rootType, asset.FileGuid), -1));

            // Asset-derived roots have a Name field.
            // The property grid shows it read-only, so it is a safe place to explain the asset to whoever opens it.
            if (rootType.GetProperty("Name") != null || rootType.GetField("Name") != null)
                root.Name = AssetNote;

            return asset;
        }

        private static object CreateRootObject()
        {
            foreach (string typeName in RootTypes)
            {
                object root = TypeLibrary.CreateObject(typeName);
                if (root != null)
                    return root;
            }
            throw new InvalidOperationException("This game's SDK has no generic asset type to store IDs in");
        }

        private static void MarkDirty(EbxAssetEntry entry)
        {
#if FROSTY_1063_LATER
            entry.ModifiedEntry.IsDirty = true;
#endif
            entry.IsDirty = true;
        }

        /// <summary>
        /// The explorer does not notice assets coming and going on its own, same as DuplicationPlugin.
        /// Bulk string edits run on a task thread, so the refresh has to go back to the UI thread.
        /// </summary>
        private static void RefreshExplorer()
        {
            FrostyDataExplorer explorer = App.EditorWindow?.DataExplorer;
            explorer?.Dispatcher.BeginInvoke((Action)(() => explorer.RefreshAll()));
        }

        #endregion
    }
}
