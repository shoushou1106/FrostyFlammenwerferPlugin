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
using System.Text;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

namespace FsLocalizationPlugin.Resources
{

    /// <summary>A reference to a string ID, including how it get there.</summary>
    public sealed class IdReference
    {
        /// <summary>Found by <see cref="IdScanner"/></summary>
        public const string ScanSource = "scan";

        /// <summary>Added by user</summary>
        public const string ManualSource = "manual";

        [JsonProperty("path")]
        public string Path = string.Empty;

        [JsonProperty("source")]
        public string Source = ScanSource;
    }

    /// <summary>For the cached database to store a ID.</summary>
    public sealed class IdEntry
    {
        /// <summary>The ID text. Can be empty.</summary>
        [JsonProperty("id")]
        public string Id = string.Empty;

        [JsonProperty("refs")]
        public List<IdReference> References = new List<IdReference>();

        [JsonIgnore]
        public bool IsEmpty => Id.Length == 0 && References.Count == 0;
    }

    /// <summary>
    /// For the project database to store a ID.
    /// Serialized as JSON into the ebx asset's UserData.
    /// </summary>
    public sealed class ProjectIdEntry
    {
        /// <summary>The ID text. Can be empty.</summary>
        [JsonProperty("id")]
        public string Id = string.Empty;

        [JsonProperty("comment")]
        public string Comment = string.Empty;

        /// <summary>Asset paths the creator linked to this string by hand.</summary>
        [JsonProperty("refs")]
        public List<string> References = new List<string>();

        [JsonIgnore]
        public bool IsEmpty => Id.Length == 0 && Comment.Length == 0 && References.Count == 0;
    }

    /// <summary>
    /// The front door to both ID stores. Read them as one, and tell them when strings come and go.
    /// </summary>
    public static class IdIndex
    {
        /// <summary>Whether either store knows this hash.</summary>
        public static bool Contains(uint hash)
        {
            return TryGet(hash, out string _);
        }

        /// <summary>Resolves a hash to its ID text, project store first.</summary>
        public static bool TryGet(uint hash, out string id)
        {
            if (ProjectIdDatabase.TryGet(hash, out id))
                return true;

            IdDatabase.Instance.EnsureLoaded();
            return IdDatabase.Instance.TryGetId(hash, out id);
        }

        /// <summary>
        /// Every known hash-to-ID pair, each hash yielded once.
        /// <para>
        /// Recording routes a hash to exactly one store, so the two normally do not overlap and the
        /// dedupe set is never even allocated. It is still needed because three paths get around the
        /// routing: importing a shared project ID file (that import takes anything in the file),
        /// registering a game string's hash by hand to attach something to it, and a hash that is an
        /// original string in one language but not in another, which routes differently depending on
        /// the language loaded at the time.
        /// </para>
        /// </summary>
        public static IEnumerable<KeyValuePair<uint, string>> Enumerate()
        {
            HashSet<uint> fromProject = null;
            foreach (KeyValuePair<uint, string> kvp in ProjectIdDatabase.Enumerate())
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

        /// <summary>
        /// Records the ID text a string was just added or changed under. A game string's ID is game
        /// knowledge and belongs in the shared cached database; anything else belongs to this
        /// project. Called with the ID as typed, the only moment it exists: everywhere else in the
        /// pipeline a string is just a hash.
        /// </summary>
        public static void Record(string idText, uint hash)
        {
            // IDs can be any text (PL_01, WindComponentData, ...), except raw-hash-shaped input.
            if (string.IsNullOrEmpty(idText) || LocalizationHelper.TryParseHexHash(idText, out uint _))
                return;

            if (IsGameString(hash))
            {
                IdDatabase.Instance.EnsureLoaded();
                if (IdDatabase.Instance.AddId(idText))
                    IdDatabase.Instance.Save();
            }
            else
            {
                ProjectIdDatabase.SetIdText(idText);
            }
        }

        /// <summary>
        /// Reports that a string was reverted or removed. A game string's ID stays in the cached
        /// database, since the game still has the string, so only the project store is asked to prune.
        /// </summary>
        public static void Forget(uint hash)
        {
            if (!IsGameString(hash))
                ProjectIdDatabase.PruneIfUnused(hash);
        }

        /// <summary>Whether the hash is one of the game's own strings rather than one the project added.</summary>
        private static bool IsGameString(uint hash)
        {
            return LocalizedStringDatabase.Current is FsLocalizationStringDatabase db
                && db.TryGetOriginalString(hash, out string _);
        }
    }

    /// <summary>
    /// Per-game database of known string IDs, stored as one JSON file in Frosty's Caches folder:
    /// "&lt;cache&gt;_flammenwerfer_ids.json", holding each hash's ID text and its references together
    /// so the whole thing can be shared as a single file. IDs are hashed from their own text, so a
    /// shared file merges into any copy of the same game.
    /// Editor tooling only. Never loads under Frosty Mod Manager.
    /// </summary>
    public sealed class IdDatabase
    {
        private const int CurrentVersion = 1;
        private const string FileNote = "Flammenwerfer ID database. Share and merge freely: every hash is derived from its ID text.";

        private static IdDatabase instance;

        private readonly Dictionary<uint, IdEntry> entries = new Dictionary<uint, IdEntry>();
        private readonly object sync = new object();
        private bool isLoaded;

        /// <summary>
        /// The profile the loaded data belongs to. This singleton outlives a profile switch
        /// (1.0.7 can change games in place), and the file paths follow the new profile, so
        /// saving without this check would overwrite one game's ID database with another's.
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

        public string DatabaseFilePath => CachePrefix + "_flammenwerfer_ids.json";


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
                    DebugLogHelper.Log("IdDatabase.Save", "Skipped: data belongs to profile {0}", loadedProfile);
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

        /// <summary>Hashes the scan saw referenced (e.g. via StringHash fields) but never found ID text for.</summary>
        public IEnumerable<uint> EnumerateRefOnlyHashes()
        {
            lock (sync)
                return entries.Where(kvp => kvp.Value.Id.Length == 0).Select(kvp => kvp.Key).ToList();
        }

        /// <summary>Every entry, ID and references together. A snapshot: safe to walk while editing.</summary>
        public IEnumerable<KeyValuePair<uint, IdEntry>> EnumerateEntries()
        {
            lock (sync)
                return new List<KeyValuePair<uint, IdEntry>>(entries);
        }

        #endregion

        #region -- ID text --
        /// <summary>Adds an ID. Returns false if the hash already has one. Caller decides when to Save().</summary>
        public bool AddId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            uint hash = LocalizationHelper.HashStringId(id);
            lock (sync)
            {
                IdEntry entry = GetOrAdd(hash);
                if (entry.Id.Length > 0)
                    return false;
                entry.Id = id;
                return true;
            }
        }

        /// <summary>Drops an ID text, keeping its references. Used when the detail editor replaces a wrong ID.</summary>
        public bool RemoveId(uint hash)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(hash, out IdEntry entry) || entry.Id.Length == 0)
                    return false;

                entry.Id = string.Empty;
                if (entry.IsEmpty)
                    entries.Remove(hash);
                return true;
            }
        }

        #endregion

        #region -- References --
        public IReadOnlyList<string> GetReferences(uint hash)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(hash, out IdEntry entry))
                    return Array.Empty<string>();
                return entry.References.Select(r => r.Path).ToList();
            }
        }

        /// <summary>Where one reference came from, or null when the hash does not carry that path.</summary>
        public string GetReferenceSource(uint hash, string assetPath)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(hash, out IdEntry entry))
                    return null;
                return entry.References.FirstOrDefault(r => r.Path == assetPath)?.Source;
            }
        }

        /// <summary>Records a reference a creator found by hand. Survives every later scan.</summary>
        public bool AddReference(uint hash, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            lock (sync)
            {
                IdEntry entry = GetOrAdd(hash);
                if (entry.References.Any(r => r.Path == assetPath))
                    return false;

                entry.References.Add(new IdReference { Path = assetPath, Source = IdReference.ManualSource });
                return true;
            }
        }

        public bool RemoveReference(uint hash, string assetPath)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(hash, out IdEntry entry))
                    return false;

                if (entry.References.RemoveAll(r => r.Path == assetPath) == 0)
                    return false;
                if (entry.IsEmpty)
                    entries.Remove(hash);
                return true;
            }
        }

        /// <summary>
        /// Replaces the scan's reference paths for a hash, leaving manual references alone.
        /// The scan bulk-writes these.
        /// </summary>
        public void SetScanReferences(uint hash, IEnumerable<string> assetPaths)
        {
            lock (sync)
            {
                IdEntry entry = GetOrAdd(hash);
                entry.References.RemoveAll(r => r.Source != IdReference.ManualSource);
                foreach (string path in assetPaths)
                {
                    if (!entry.References.Any(r => r.Path == path))
                        entry.References.Add(new IdReference { Path = path, Source = IdReference.ScanSource });
                }
            }
        }

        #endregion

        #region -- Whole entries --
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

        #endregion

        #region -- Sharing --
        /// <summary>The database as a shareable document, the same text the Caches file holds.</summary>
        public string ExportJson()
        {
            lock (sync)
                return JsonConvert.SerializeObject(BuildDocument(), Formatting.Indented);
        }

        /// <summary>
        /// Merges a shared database file. Accepts the JSON format and the plain one-ID-per-line text
        /// the first builds wrote. <paramref name="accept"/> decides which hashes belong here (the
        /// caller keeps out anything that is not a string of this game). Reports what it did, then
        /// saves and raises Changed. Throws if the text is JSON but not one of ours.
        /// </summary>
        public void ImportFile(string path, Func<uint, bool> accept)
        {
            EnsureLoaded();

            string text = File.ReadAllText(path);
            int added = 0;
            int updated = 0;
            int skipped = 0;

            lock (sync)
            {
                if (IsJson(text))
                    ImportDocument(ParseDocument(text, path), accept, ref added, ref updated, ref skipped);
                else
                    ImportIdLines(text, accept, ref added, ref skipped);
            }

            App.Logger.Log("Import complete: {0} new ID(s), {1} filled in, {2} skipped", added, updated, skipped);
            if (added > 0 || updated > 0)
                Save();
        }

        #endregion

        #region -- Entry helpers --
        private IdEntry GetOrAdd(uint hash)
        {
            if (!entries.TryGetValue(hash, out IdEntry entry))
            {
                entry = new IdEntry();
                entries.Add(hash, entry);
            }
            return entry;
        }

        /// <summary>Fills in what a partial or hand-edited document left out.</summary>
        private static IdEntry Normalize(IdEntry entry)
        {
            entry.Id = entry.Id ?? string.Empty;
            entry.References = entry.References ?? new List<IdReference>();
            entry.References.RemoveAll(r => r == null || string.IsNullOrEmpty(r.Path));
            foreach (IdReference reference in entry.References)
                reference.Source = string.IsNullOrEmpty(reference.Source) ? IdReference.ScanSource : reference.Source;
            return entry;
        }

        /// <summary>Copies over what the stored entry is missing. Returns whether it gained anything.</summary>
        private static bool Fill(IdEntry stored, IdEntry incoming)
        {
            bool changed = false;

            if (stored.Id.Length == 0 && incoming.Id.Length > 0)
            {
                stored.Id = incoming.Id;
                changed = true;
            }
            foreach (IdReference reference in incoming.References)
            {
                if (!stored.References.Any(r => r.Path == reference.Path))
                {
                    stored.References.Add(reference);
                    changed = true;
                }
            }
            return changed;
        }

        #endregion

        #region -- File format --
        /// <summary>The file's shape. Entries are keyed by hash in hex.</summary>
        private sealed class Document
        {
            [JsonProperty("version")]
            public int Version = CurrentVersion;

            [JsonProperty("note")]
            public string Note = FileNote;

            [JsonProperty("game")]
            public string Game = string.Empty;

            [JsonProperty("entries")]
            public Dictionary<string, IdEntry> Entries = new Dictionary<string, IdEntry>();
        }

        /// <summary>Snapshots the entries into the document shape. Call under the lock.</summary>
        private Document BuildDocument()
        {
            Document document = new Document { Game = ProfilesLibrary.ProfileName };
            // Sorted by ID text so two people scanning the same game produce comparable files.
            foreach (KeyValuePair<uint, IdEntry> kvp in entries.OrderBy(kvp => kvp.Value.Id, StringComparer.Ordinal).ThenBy(kvp => kvp.Key))
                document.Entries.Add(kvp.Key.ToString("x8"), kvp.Value);
            return document;
        }

        private static Document ParseDocument(string text, string path)
        {
            Document document = JsonConvert.DeserializeObject<Document>(text);
            if (document?.Entries == null)
                throw new FormatException("Not a Flammenwerfer ID database.");

            if (document.Game.Length > 0 && document.Game != ProfilesLibrary.ProfileName)
                DebugLogHelper.Log("IdDatabase.ParseDocument", "{0} was made for {1}", path, document.Game);
            return document;
        }

        /// <summary>Tells the two accepted shapes apart without copying the whole file.</summary>
        private static bool IsJson(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsWhiteSpace(c))
                    return c == '{';
            }
            return false;
        }

        private void ImportDocument(Document document, Func<uint, bool> accept, ref int added, ref int updated, ref int skipped)
        {
            foreach (KeyValuePair<string, IdEntry> kvp in document.Entries)
            {
                if (kvp.Value == null || !uint.TryParse(kvp.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash) || !accept(hash))
                {
                    skipped++;
                    continue;
                }

                IdEntry incoming = Normalize(kvp.Value);
                if (!entries.TryGetValue(hash, out IdEntry stored))
                {
                    entries.Add(hash, incoming);
                    added++;
                }
                else if (Fill(stored, incoming))
                {
                    updated++;
                }
            }
        }

        private void ImportIdLines(string text, Func<uint, bool> accept, ref int added, ref int skipped)
        {
            foreach (string line in text.Split('\n'))
            {
                // Only strip the line ending: IDs are hashed exactly as written.
                string id = line.TrimEnd('\r');
                if (id.Length == 0 || id.StartsWith("#"))
                    continue;

                uint hash = LocalizationHelper.HashStringId(id);
                if (!accept(hash))
                    skipped++;
                else if (AddId(id))
                    added++;
            }
        }

        /// <summary>A corrupt file leaves the database empty rather than being silently replaced.</summary>
        private void LoadFile()
        {
            if (!File.Exists(DatabaseFilePath))
                return;

            try
            {
                foreach (KeyValuePair<string, IdEntry> kvp in ParseDocument(File.ReadAllText(DatabaseFilePath), DatabaseFilePath).Entries)
                {
                    if (kvp.Value != null && uint.TryParse(kvp.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash))
                        entries[hash] = Normalize(kvp.Value);
                }
                DebugLogHelper.Log("IdDatabase.LoadFile", "Loaded {0} entrie(s) from {1}", entries.Count, DatabaseFilePath);
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
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(DatabaseFilePath)));
                File.WriteAllText(DatabaseFilePath, JsonConvert.SerializeObject(BuildDocument(), Formatting.Indented));
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
    /// The project ID database: IDs, comments, and manual references for user-added strings.
    /// Unlike the string edits themselves, this is not per-language: it lives in ONE dedicated added
    /// ebx asset with a generic root object (every Frostbite SDK has one), and keeps its payload as
    /// JSON in that entry's UserData. Frosty saves UserData with the project and vanilla
    /// FsLocalizationPlugin never looks at it, so projects stay loadable there. IsTransientModified
    /// keeps the asset in the project file but out of exported mods.
    /// </summary>
    public static class ProjectIdDatabase
    {
        /// <summary>
        /// The carrier asset, at the root of the ebx tree so it is easy to find and never buried in
        /// a game's locale folder. Lookups match on the END of the name, so a project written by an
        /// earlier build, which put it beside the game's localization files, still resolves.
        /// </summary>
        private const string AssetName = "Flammenwerfer/ProjectIdDatabase";

        /// <summary>The asset's Name field, the one thing someone opening it in the editor sees.</summary>
        private const string StoreNote = "Flammenwerfer keeps the project's string IDs here. FsLocalizationPlugin cannot break this asset and it is never exported into a mod. Revert it to delete every project ID.";

        /// <summary>Root object type for the store asset, the first one the game has wins.</summary>
        private static readonly string[] RootTypes = { "Asset", "DataContainer" };

        /// <summary>
        /// Frosty reads UserData back one byte per char, so the stored payload has to stay ASCII.
        /// Escaping non-ASCII keeps CJK comments and emoji round-tripping exactly.
        /// </summary>
        private static readonly JsonSerializerSettings StoreJson = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
        };

        /// <summary>Exported files are meant to be read and diffed by people.</summary>
        private static readonly JsonSerializerSettings FileJson = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
        };

        // Parse cache, keyed by the exact text it came from so it can never serve stale data.
        private static string parsedFrom;
        private static Dictionary<uint, ProjectIdEntry> parsedIds;

        /// <summary>
        /// The "Project ID Database" option, per game: a creator may prefer tracking IDs the
        /// traditional way rather than have an extra asset appear in their project. Read on demand
        /// because Config.Get is an in-memory lookup, and a cached copy would go stale the moment
        /// the option is toggled or the profile switches. Only writes are gated: reads stay live so
        /// IDs already stored in a project keep resolving.
        /// </summary>
        public static bool Enabled => Config.Get("Flammenwerfer_ProjectIdDatabaseEnabled", true, ConfigScope.Game);

        /// <summary>How many hashes the project stores anything for.</summary>
        public static int Count => Read().Count;

        #region -- Reading --
        /// <summary>
        /// Whether the store holds this hash. Reads the asset manager every call: another window
        /// (Modify String, Loc Studio) can add a string while the ID Database tab is open.
        /// </summary>
        public static bool Contains(uint hash) => Read().ContainsKey(hash);

        public static bool TryGet(uint hash, out string idText)
        {
            idText = null;
            if (Read().TryGetValue(hash, out ProjectIdEntry entry) && entry.Id.Length > 0)
            {
                idText = entry.Id;
                return true;
            }
            return false;
        }

        /// <summary>The creator's note for a hash, empty when there is none.</summary>
        public static string GetComment(uint hash)
        {
            return Read().TryGetValue(hash, out ProjectIdEntry entry) ? entry.Comment : string.Empty;
        }

        public static IReadOnlyList<string> GetReferences(uint hash)
        {
            return Read().TryGetValue(hash, out ProjectIdEntry entry) ? (IReadOnlyList<string>)entry.References : Array.Empty<string>();
        }

        /// <summary>Hash-to-ID pairs, skipping entries that carry no ID text.</summary>
        public static IEnumerable<KeyValuePair<uint, string>> Enumerate()
        {
            foreach (KeyValuePair<uint, ProjectIdEntry> kvp in Read())
            {
                if (kvp.Value.Id.Length > 0)
                    yield return new KeyValuePair<uint, string>(kvp.Key, kvp.Value.Id);
            }
        }

        /// <summary>Every entry, so callers can read id and comment without a lookup per field.</summary>
        public static IEnumerable<KeyValuePair<uint, ProjectIdEntry>> EnumerateEntries()
        {
            // A snapshot: callers walk this while editing rows, which can write back into the store.
            return new List<KeyValuePair<uint, ProjectIdEntry>>(Read());
        }

        #endregion

        #region -- ID text and comments --
        public static void SetIdText(string idText)
        {
            if (!Enabled || string.IsNullOrEmpty(idText))
                return;

            Mutate(create: true, ids => GetOrAdd(ids, LocalizationHelper.HashStringId(idText)).Id = idText);
        }

        /// <summary>Drops just the ID text, keeping any comment and references.</summary>
        public static void RemoveIdText(uint hash)
        {
            Mutate(create: false, ids =>
            {
                if (ids.TryGetValue(hash, out ProjectIdEntry entry))
                    entry.Id = string.Empty;
            });
        }

        /// <summary>Registers a hash without an ID text, so it can carry a comment.</summary>
        public static void RegisterHash(uint hash)
        {
            if (!Enabled)
                return;

            Mutate(create: true, ids => GetOrAdd(ids, hash));
        }

        public static void SetComment(uint hash, string comment)
        {
            if (!Enabled)
                return;
            comment = comment ?? string.Empty;

            // Clearing a comment must never create the store asset.
            Mutate(create: comment.Length > 0, ids =>
            {
                if (comment.Length > 0)
                    GetOrAdd(ids, hash).Comment = comment;
                else if (ids.TryGetValue(hash, out ProjectIdEntry entry))
                    entry.Comment = string.Empty;
            });
        }

        #endregion

        #region -- References --
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
                if (ids.TryGetValue(hash, out ProjectIdEntry entry))
                    entry.References.Remove(assetPath);
            });
        }

        #endregion

        #region -- Whole entries --
        /// <summary>Drops the whole entry: ID, comment and references together.</summary>
        public static void RemoveEntry(uint hash)
        {
            Mutate(create: false, ids => ids.Remove(hash));
        }

        /// <summary>
        /// Drops an entry once its string is gone from every language. The same ID can be added in
        /// several languages, so removing one of them is not enough to retire the entry: only when
        /// no language still carries the string do its ID, comment and references become meaningless.
        /// </summary>
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

        /// <summary>Empties the store, which reverts the asset out of the project.</summary>
        public static void Clear()
        {
            Mutate(create: false, ids => ids.Clear());
        }

        #endregion

        #region -- Sharing --
        /// <summary>The store as a shareable document, or an empty string when there is nothing to share.</summary>
        public static string ExportJson()
        {
            Dictionary<uint, ProjectIdEntry> ids = Read();
            return ids.Count == 0 ? string.Empty : JsonConvert.SerializeObject(ToPayload(ids), FileJson);
        }

        /// <summary>
        /// Merges an exported document into the store. Stored IDs and comments win and references are
        /// unioned, so importing the same file twice changes nothing. Reports what it did.
        /// Throws if the text is not ours.
        /// </summary>
        public static void ImportJson(string json)
        {
            Payload payload = JsonConvert.DeserializeObject<Payload>(json);
            if (payload?.Ids == null)
                throw new FormatException("Not a Flammenwerfer project ID database.");

            int added = 0;
            int updated = 0;
            int skipped = 0;

            Mutate(create: payload.Ids.Count > 0, ids =>
            {
                foreach (KeyValuePair<string, ProjectIdEntry> kvp in payload.Ids)
                {
                    if (kvp.Value == null || !uint.TryParse(kvp.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash))
                    {
                        skipped++;
                        continue;
                    }

                    ProjectIdEntry incoming = Normalize(kvp.Value);
                    if (!ids.TryGetValue(hash, out ProjectIdEntry stored))
                    {
                        ids.Add(hash, incoming);
                        added++;
                    }
                    else if (Fill(stored, incoming))
                    {
                        updated++;
                    }
                }
            });

            App.Logger.Log("Import complete: {0} new entrie(s), {1} filled in, {2} skipped", added, updated, skipped);
        }

        #endregion

        #region -- Payload: the JSON in the asset's UserData --
        /// <summary>The stored document: a format version, and the entries keyed by hash in hex.</summary>
        private sealed class Payload
        {
            public const int CurrentVersion = 1;

            [JsonProperty("version")]
            public int Version = CurrentVersion;

            [JsonProperty("ids")]
            public Dictionary<string, ProjectIdEntry> Ids = new Dictionary<string, ProjectIdEntry>();
        }

        /// <summary>The stored entries. Only <see cref="Mutate"/> is allowed to change them.</summary>
        private static Dictionary<uint, ProjectIdEntry> Read() => Read(FindEntry());

        private static Dictionary<uint, ProjectIdEntry> Read(EbxAssetEntry entry)
        {
            string json = entry?.ModifiedEntry?.UserData ?? string.Empty;
            if (parsedIds != null && parsedFrom == json)
                return parsedIds;

            Dictionary<uint, ProjectIdEntry> ids = new Dictionary<uint, ProjectIdEntry>();
            if (json.Length > 0)
            {
                try
                {
                    Payload payload = JsonConvert.DeserializeObject<Payload>(json);
                    if (payload?.Ids != null)
                    {
                        foreach (KeyValuePair<string, ProjectIdEntry> kvp in payload.Ids)
                        {
                            if (kvp.Value != null && uint.TryParse(kvp.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash))
                                ids[hash] = Normalize(kvp.Value);
                        }
                    }
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
        private static void Mutate(bool create, Action<Dictionary<uint, ProjectIdEntry>> change)
        {
            EbxAssetEntry entry = create ? GetOrCreateEntry() : FindEntry();
            if (entry == null)
                return;

            Dictionary<uint, ProjectIdEntry> ids = Read(entry);
            change(ids);

            if (ids.Count == 0)
            {
                DebugLogHelper.Log("ProjectIdDatabase.Mutate", "Store empty, reverting {0}", entry.Name);
                App.AssetManager.RevertAsset(entry);
                parsedFrom = null;
                parsedIds = null;

                // The asset is out of the manager, but not out of the tree the explorer already built.
                RefreshExplorer();
                return;
            }

            string json = JsonConvert.SerializeObject(ToPayload(ids), StoreJson);
            entry.ModifiedEntry.UserData = json;
            parsedFrom = json;
            parsedIds = ids;
            MarkDirty(entry);
        }

        private static Payload ToPayload(Dictionary<uint, ProjectIdEntry> ids)
        {
            Payload payload = new Payload();
            foreach (KeyValuePair<uint, ProjectIdEntry> kvp in ids)
                payload.Ids.Add(kvp.Key.ToString("x8"), kvp.Value);
            return payload;
        }

        private static ProjectIdEntry GetOrAdd(Dictionary<uint, ProjectIdEntry> ids, uint hash)
        {
            if (!ids.TryGetValue(hash, out ProjectIdEntry entry))
            {
                entry = new ProjectIdEntry();
                ids.Add(hash, entry);
            }
            return entry;
        }

        /// <summary>Fills in what a partial or hand-edited document left out.</summary>
        private static ProjectIdEntry Normalize(ProjectIdEntry entry)
        {
            entry.Id = entry.Id ?? string.Empty;
            entry.Comment = entry.Comment ?? string.Empty;
            entry.References = entry.References ?? new List<string>();
            return entry;
        }

        /// <summary>Copies over what the stored entry is missing. Returns whether it gained anything.</summary>
        private static bool Fill(ProjectIdEntry stored, ProjectIdEntry incoming)
        {
            bool changed = false;

            if (stored.Id.Length == 0 && incoming.Id.Length > 0)
            {
                stored.Id = incoming.Id;
                changed = true;
            }
            if (stored.Comment.Length == 0 && incoming.Comment.Length > 0)
            {
                stored.Comment = incoming.Comment;
                changed = true;
            }
            foreach (string path in incoming.References)
            {
                if (!string.IsNullOrEmpty(path) && !stored.References.Contains(path))
                {
                    stored.References.Add(path);
                    changed = true;
                }
            }
            return changed;
        }

        #endregion

        #region -- The carrier asset --
        /// <summary>
        /// Finds the store asset by suffix among the modified assets. Nothing is cached between
        /// calls on purpose: a cached entry would go stale when the user creates a new project or
        /// switches profile, and serving another project's IDs is not worth the saved lookup.
        /// The name is only resolved when the asset has to be created, since that reads ebx.
        /// </summary>
        private static EbxAssetEntry FindEntry()
        {
            foreach (EbxAssetEntry candidate in App.AssetManager.EnumerateEbx(modifiedOnly: true))
            {
                if (!candidate.IsAdded
                    || !candidate.Name.EndsWith(AssetName, StringComparison.OrdinalIgnoreCase)
                    || !(candidate.ModifiedEntry?.DataObject is EbxAsset))
                {
                    continue;
                }

                // Cheap insurance: whatever we write to must never reach an exported mod.
                candidate.ModifiedEntry.IsTransientModified = true;
                return candidate;
            }
            return null;
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
                    // DuplicationPlugin's create-new pattern, minus bundles: nothing references this in-game.
                    entry = App.AssetManager.AddEbx(AssetName, CreateStoreAsset(Guid.NewGuid()));
                }
                else
                {
                    // Left over from a revert that only cleared the data. Rebuild it in place.
                    entry.ModifiedEntry = new ModifiedAssetEntry { DataObject = CreateStoreAsset(entry.Guid) };
                    entry.IsAdded = true;
                }

                entry.ModifiedEntry.IsTransientModified = true;
                MarkDirty(entry);
                RefreshExplorer();

                DebugLogHelper.Log("ProjectIdDatabase.GetOrCreateEntry", "Created project ID store: {0}", entry.Name);
                return entry;
            }
            catch (Exception ex)
            {
                App.Logger.LogError("Failed to create the project ID store asset: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// The store's ebx. Real ebx data (rather than a ModifiedResource) is what lets Frosty open
        /// the asset in the editor instead of failing to read data that was never written.
        /// </summary>
        private static EbxAsset CreateStoreAsset(Guid fileGuid)
        {
            EbxAsset asset = new EbxAsset(CreateRootObject());
            asset.SetFileGuid(fileGuid);

            dynamic root = asset.RootObject;
            Type rootType = (Type)root.GetType();
            root.SetInstanceGuid(new AssetClassGuid(Utils.GenerateDeterministicGuid(asset.Objects, rootType, asset.FileGuid), -1));

            // Asset-derived roots have a Name. The property grid shows it read-only, so it is a safe
            // place to explain the asset to whoever opens it.
            if (rootType.GetProperty("Name") != null || rootType.GetField("Name") != null)
                root.Name = StoreNote;

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
            throw new Exception("This game's SDK has no generic asset type to store IDs in");
        }

        private static void MarkDirty(EbxAssetEntry entry)
        {
#if !FROSTY_107
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
