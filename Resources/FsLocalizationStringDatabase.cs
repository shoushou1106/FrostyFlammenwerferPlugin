using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using FsLocalizationPlugin.Helpers;
using FsLocalizationPlugin.Resources;
using FsLocalizationPlugin.Windows;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

#pragma warning disable IDE0130 // Namespace does not match folder structure
// Flat namespace on purpose to stay compatible with original FsLocalizationPlugin.
namespace FsLocalizationPlugin
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// The diff Flammenwerfer records for a localized-text asset: strings added, and removed.
    /// </summary>
    /// <remarks>
    /// <para>Read/write three format layers in order,
    /// stays two-way compatible with the original FsLocalizationPlugin:</para>
    /// <list type="number">
    /// <item>
    /// <term>Legacy format</term>
    /// <description>Magic is the count, <strong>one</strong> byte per char. (Never change)</description>
    /// </item>
    /// <item>
    /// <term>Current format</term>
    /// <description>Magic <c>0xABCD0001</c>, <strong>two</strong> bytes per char. (Never change)</description>
    /// </item>
    /// <item>
    /// <term>Flammenwerfer extended</term>
    /// <description>Magic <c>0xF1A88E22</c> ("FLAMMENN").
    /// The original plugin never writes or reads this, it will run out of bytes.
    /// This is ours and safe to extend, having it's own format version field.</description>
    /// </item>
    /// </list>
    /// <para>この世界は好都合に未完成</para>
    /// <para>だから知りたいんだ</para>
    /// </remarks>
    public class ModifiedFsLocalizationAsset : ModifiedResource
    {
        // Cumulative extension sections, only read/write every section this build knows about,
        // newer sections are ignored safely.
        // v1: stringsToRemove.
        // v2: UTF-8 overwrites for chars above 0xFFFF.
        private const uint FlammenwerferExtensionMagic = 0xF1A88E22; // "FLAMMENN"
        private const uint FlammenwerferExtensionFormatVersion = 2;

        public Dictionary<uint, string> strings = new Dictionary<uint, string>();

        public HashSet<uint> stringsToRemove = new HashSet<uint>();

        public ModifiedFsLocalizationAsset()
        {
        }

        public override void ReadInternal(NativeReader reader)
        {
            uint fsMagic = reader.ReadUInt();
            if (fsMagic != 0xABCD0001)
            {
                // Legacy FsLocalizationPlugin format
                DebugLogHelper.Log("ModifiedResource.ReadInternal", "FsLocalization Old Format Detected");
                int legacyCount = (int)fsMagic;
                for (int i = 0; i < legacyCount; i++)
                {
                    uint hash = reader.ReadUInt();
                    string str = reader.ReadNullTerminatedString();
                    AddString(hash, str);
                }
                return;
            }

            // New FsLocalizationPlugin format
            int stringsCount = reader.ReadInt();
            for (int i = 0; i < stringsCount; i++)
            {
                uint hash = reader.ReadUInt();
                string str = reader.ReadNullTerminatedWideString();
                AddString(hash, str);
            }

            // Flammenwerfer extension.
            // Vanilla-saved files will run out of bytes here.
            try
            {
                uint flammenMagic = reader.ReadUInt();
                if (flammenMagic == FlammenwerferExtensionMagic)
                {
                    uint formatVersion = reader.ReadUInt();
                    DebugLogHelper.Log("ModifiedResource.ReadInternal", "Flammenwerfer Extended Format Detected, Version {0}", formatVersion);

                    // Cumulative, read every section this build knows about, newer sections are ignored safely.
                    if (formatVersion >= 1)
                    {
                        // Section 1: String removal support
                        int stringsToRemoveCount = reader.ReadInt();
                        for (int i = 0; i < stringsToRemoveCount; i++)
                        {
                            uint hash = reader.ReadUInt();
                            RemoveString(hash);
                        }
                    }
                    if (formatVersion >= 2)
                    {
                        // Section 2: True UTF-8 values for strings escaped in the FsLoc block
                        int overwriteCount = reader.ReadInt();
                        for (int i = 0; i < overwriteCount; i++)
                        {
                            uint hash = reader.ReadUInt();
                            strings[hash] = ReadUtf8(reader);
                        }
                    }
                }
                else
                {
                    DebugLogHelper.Log("ModifiedResource.ReadInternal", "FsLocalization New Format Detected");
                }
            }
            catch
            {
            }
        }

        public override void SaveInternal(NativeWriter writer)
        {
            // Chars above 0xFFFF cannot survive the FsLoc block
            // Write them escaped there and carry the true UTF-8 value in section 2.
            Dictionary<uint, string> overwrites = new Dictionary<uint, string>();

            // New FsLocalizationPlugin format
            writer.Write(0xABCD0001);
            writer.Write(strings.Count);

            foreach (KeyValuePair<uint, string> kvp in strings)
            {
                string s = kvp.Value;
                if (ContainsNonBmp(s))
                {
                    overwrites[kvp.Key] = s;
                    s = EscapeNonBmp(s);
                }

                writer.Write(kvp.Key);
                foreach (char c in s)
                    writer.Write((ushort)c);
                writer.Write((ushort)0);
            }

            // Flammenwerfer extension start here
            // Vanilla-saved files will run out of bytes
            if (stringsToRemove.Count == 0
                && overwrites.Count == 0)
            {
                // Nothing to write here, skipping
                // IMPORTANT: Update the condition when a new section added.
                return;
            }
            writer.Write(FlammenwerferExtensionMagic);
            writer.Write(FlammenwerferExtensionFormatVersion);

            // FormatVersion: 1
            // Section 1: String removal
            writer.Write(stringsToRemove.Count);
            foreach (uint value in stringsToRemove)
                writer.Write(value);

            // FormatVersion: 2
            // Section 2: True UTF-8 values for the escaped strings above
            writer.Write(overwrites.Count);
            foreach (KeyValuePair<uint, string> kvp in overwrites)
            {
                writer.Write(kvp.Key);
                WriteUtf8(writer, kvp.Value);
            }
        }

        private static void WriteUtf8(NativeWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadUtf8(NativeReader reader)
        {
            int length = reader.ReadInt();
            return length > 0 ? Encoding.UTF8.GetString(reader.ReadBytes(length)) : string.Empty;
        }

        /// <summary>
        /// Check if a string contains char above 0xFFFF
        /// </summary>
        private static bool ContainsNonBmp(string value)
        {
            foreach (char c in value)
            {
                if (char.IsSurrogate(c))
                    return true;
            }
            return false;
        }

        /// <summary>Replaces each char above 0xFFFF with a readable [U+XXXXX] marker.</summary>
        private static string EscapeNonBmp(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length + 16);
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    int codePoint = char.ConvertToUtf32(value[i], value[i + 1]);
                    sb.Append("[U+").Append(codePoint.ToString(CultureInfo.InvariantCulture)).Append(']');
                    i++;
                }
                else if (!char.IsSurrogate(value[i]))
                {
                    sb.Append(value[i]);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Adds or updates a string with the specified ID.
        /// </summary>
        /// <param name="id">The hash ID of the string.</param>
        /// <param name="str">The string value.</param>
        public void AddString(uint id, string str)
        {
            strings[id] = str;
            stringsToRemove.Remove(id);
        }

        /// <summary>
        /// Reverts a string with the specified ID back to its original value.
        /// </summary>
        /// <param name="id">The hash ID of the string to revert.</param>
        public void RevertString(uint id)
        {
            strings.Remove(id);
            stringsToRemove.Remove(id);
        }

        /// <summary>
        /// Marks a string with the specified ID for removal.
        /// </summary>
        /// <param name="id">The hash ID of the string to remove.</param>
        public void RemoveString(uint id)
        {
            strings.Remove(id);
            stringsToRemove.Add(id);
        }

        /// <summary>
        /// Gets the modified value of a string, or <see langword="null"/> if this diff doesn't touch it.
        /// </summary>
        public string GetString(uint id)
        {
            return strings.TryGetValue(id, out string value) ? value : null;
        }

        /// <summary>
        /// Enumerates the hash IDs of every string this diff adds or changes (not including removals).
        /// </summary>
        public IEnumerable<uint> EnumerateStrings()
        {
            foreach (uint key in strings.Keys)
                yield return key;
        }

        /// <summary>Merges another diff into this one. For Mod Manager use.</summary>
        public void Merge(ModifiedFsLocalizationAsset other)
        {
            foreach (uint key in other.strings.Keys)
                strings[key] = other.strings[key];

            try
            {
                // Guards a stringsToRemove that failed to come back populated from a
                // vanilla FsLocalizationPlugin ModifiedResource.
                stringsToRemove.UnionWith(other.stringsToRemove);
            }
            catch { }
        }
    }

    /// <summary>The EbxAsset wrapper for a UITextDatabase's localized-text child asset. Forwards to the underlying diff.</summary>
    public class FsLocalizationAsset : EbxAsset
    {
        private ModifiedFsLocalizationAsset modified = new ModifiedFsLocalizationAsset();
        public override ModifiedResource SaveModifiedResource()
        {
            return modified;
        }

        public override void ApplyModifiedResource(ModifiedResource modifiedResource)
        {
            modified = modifiedResource as ModifiedFsLocalizationAsset;
        }

        public void AddString(uint id, string str)
        {
            modified.AddString(id, str);
        }

        public string GetString(uint id)
        {
            return modified.GetString(id);
        }

        public void RevertString(uint id)
        {
            modified.RevertString(id);
        }
        public void RemoveString(uint id)
        {
            modified.RemoveString(id);
        }

        public IEnumerable<uint> EnumerateStrings() => modified.EnumerateStrings();

        public Dictionary<uint, string> GetStrings() => modified.strings;

        public HashSet<uint> GetStringsToRemove() => modified.stringsToRemove;
    }

    /// <summary>Flammenwerfer's ILocalizedStringDatabase. How the editor and other plugins read/edit localized text.</summary>
    public class FsLocalizationStringDatabase : ILocalizedStringDatabase
    {
        /// <summary>Handshake anchor for other plugins, see <see cref="FlammenwerferApi"/>.</summary>
        public static int FlammenwerferApiVersion => FlammenwerferApi.Version;

        private Dictionary<uint, string> strings = new Dictionary<uint, string>();
        private FsLocalizationAsset loadedDatabase;
        private EbxAssetEntry subscribedTextEntry;

        /// <summary>
        /// Initializes the localization database by loading strings for the configured language.
        /// </summary>
        public void Initialize()
        {
            string language = "LanguageFormat_" + Config.Get("Language", "English", ConfigScope.Game);

            Guid stringChunk = Guid.Empty;
            Guid histogramChunk = Guid.Empty;

            strings.Clear();
            loadedDatabase = null;

            if (subscribedTextEntry != null)
            {
                subscribedTextEntry.AssetModified -= OnLoadedTextAssetModified;
                subscribedTextEntry = null;
            }

            bool foundLanguage = false;

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx("LocalizationAsset"))
            {
                dynamic localizationAsset = App.AssetManager.GetEbx(entry).RootObject;

                foreach (PointerRef pointer in localizationAsset.LocalizedTexts)
                {
                    EbxAssetEntry textEntry = App.AssetManager.GetEbxEntry(pointer.External.FileGuid);
                    if (textEntry == null)
                        continue;

                    // Peek at the language before committing.
                    // One LocalizationAsset can list texts for several languages.
                    FsLocalizationAsset candidate = App.AssetManager.GetEbxAs<FsLocalizationAsset>(textEntry);
                    dynamic localizedText = candidate.RootObject;
                    if (localizedText.Language.ToString() != language)
                        continue;

                    loadedDatabase = candidate;
                    subscribedTextEntry = textEntry;
                    textEntry.AssetModified += OnLoadedTextAssetModified;

                    stringChunk = localizedText.BinaryChunk;
                    histogramChunk = localizedText.HistogramChunk;
                    foundLanguage = true;
                    break;
                }

                if (foundLanguage)
                    break;
            }

            if (!foundLanguage)
            {
                DebugLogHelper.Log("Database.Initialize", "No LocalizationAsset found for language {0}", language);
                return;
            }

            // Load chunk
            if (stringChunk != Guid.Empty && histogramChunk != Guid.Empty)
            {
                ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(stringChunk);
                ChunkAssetEntry histogramEntry = App.AssetManager.GetChunkEntry(histogramChunk);

                if (chunkEntry != null && histogramEntry != null)
                {
                    // Only load if chunk exists
                    strings = Flammen.ReadStrings(histogramEntry, chunkEntry);
                    DebugLogHelper.Log("Database.Initialize", "Loaded {0} strings for language {1}", strings.Count, language);
                }
            }
        }

        private void OnLoadedTextAssetModified(object sender, EventArgs e)
        {
            loadedDatabase = App.AssetManager.GetEbxAs<FsLocalizationAsset>(subscribedTextEntry);
        }

        /// <summary>Every string hash visible for the current language: modified diff (minus removals), plus original strings not shadowed.</summary>
        public IEnumerable<uint> EnumerateStrings()
        {
            if (loadedDatabase == null)
                yield break;

            HashSet<uint> removed = loadedDatabase.GetStringsToRemove();
            var yieldedKeys = new HashSet<uint>();

            foreach (uint key in loadedDatabase.EnumerateStrings())
            {
                if (!removed.Contains(key))
                {
                    yield return key;
                    yieldedKeys.Add(key);
                }
            }

            foreach (uint key in strings.Keys)
            {
                if (!removed.Contains(key) && !yieldedKeys.Contains(key))
                    yield return key;
            }
        }

        /// <summary>Hashes touched by the modified diff, including removed ones (unlike <see cref="EnumerateStrings"/>).</summary>
        public IEnumerable<uint> EnumerateModifiedStrings()
        {
            if (loadedDatabase == null)
                yield break;

            foreach (uint key in loadedDatabase.EnumerateStrings())
                yield return key;
        }

        /// <summary>Hashes marked for removal by the modified diff.</summary>
        public IEnumerable<uint> EnumerateRemovedStrings()
        {
            if (loadedDatabase == null)
                yield break;

            foreach (uint key in loadedDatabase.GetStringsToRemove())
                yield return key;
        }

        public IEnumerable<uint> EnumerateOriginalStrings()
        {
            if (strings == null)
                yield break;

            foreach (uint key in strings.Keys)
                yield return key;
        }

        public string GetString(uint id)
        {
            if (TryGetString(id, out string value))
                return value;

            return IsStringRemoved(id) ? $"[Error] String Removed: {id:X8}" : $"[Error] Invalid String ID: {id:X8}";
        }

        public string GetString(string stringId)
        {
            return GetString(LocalizationHelper.HashStringId(stringId));
        }

        /// <summary>Gets a string's current value, without the "[Error] ..." placeholder <see cref="GetString(uint)"/> uses for display.</summary>
        public bool TryGetString(uint id, out string value)
        {
            if (loadedDatabase != null)
            {
                if (loadedDatabase.GetStringsToRemove().Contains(id))
                {
                    value = null;
                    return false;
                }

                string modifiedValue = loadedDatabase.GetString(id);
                if (modifiedValue != null)
                {
                    value = modifiedValue;
                    return true;
                }
            }

            return strings.TryGetValue(id, out value);
        }

        /// <summary>Gets a string's unmodified original value from the game chunks, ignoring the modified diff.</summary>
        public bool TryGetOriginalString(uint id, out string value)
        {
            return strings.TryGetValue(id, out value);
        }

        /// <summary>Whether a string has been marked for removal.</summary>
        public bool IsStringRemoved(uint id)
        {
            return loadedDatabase != null && loadedDatabase.GetStringsToRemove().Contains(id);
        }

        /// <summary>Whether a string carries an edit from this project.</summary>
        public bool isStringEdited(uint id)
        {
            return loadedDatabase != null && loadedDatabase.GetStrings().ContainsKey(id);
        }

        #region -- Writing --
        // The overloads that take an ID text tell IdIndex about it,
        // since this is the only place that text exists.
        // IdIndex decides which database it belongs in.

        /// <summary>Adds a new string under a string ID (e.g. <c>ID_FLAME</c>) and returns its hash.</summary>
        public uint AddString(string id, string value)
        {
            uint hash = LocalizationHelper.HashStringId(id);

            loadedDatabase.AddString(hash, value);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);

            IdIndex.Record(id, hash);
            return hash;
        }

        public void SetString(uint id, string value)
        {
            loadedDatabase.AddString(id, value);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);
        }

        public void SetString(string id, string value)
        {
            uint hash = LocalizationHelper.HashStringId(id);
            SetString(hash, value);
            IdIndex.Record(id, hash);
        }

        /// <summary>
        /// Marks a string for removal. Not supported by the original FsLocalizationPlugin.
        /// A string the project added is reverted instead. The game never had it, so a removal
        /// marker would only tell the mod to delete something that is not there.
        /// </summary>
        public void RemoveString(uint id)
        {
            if (!strings.ContainsKey(id))
            {
                RevertString(id);
                return;
            }

            loadedDatabase.RemoveString(id);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);

            IdIndex.Forget(id);
        }

        /// <summary>Drops this project's edit, putting the game's own value back.</summary>
        public void RevertString(uint id)
        {
            loadedDatabase.RevertString(id);

            EbxAssetEntry entry = App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid);
            if (loadedDatabase.GetStrings().Count == 0 && loadedDatabase.GetStringsToRemove().Count == 0)
            {
                // Nothing left in the diff, so revert the asset instead of leaving it marked modified.
                App.AssetManager.RevertAsset(entry, dataOnly: false, suppressOnModify: false);
            }
            else
            {
                App.AssetManager.ModifyEbx(entry.Name, loadedDatabase);
            }

            IdIndex.Forget(id);
        }

        #endregion

        #region -- Windows --
        public void AddStringWindow()
        {
            new ModifyStringWindow(Application.Current.MainWindow).ShowDialog();
        }

        public void BulkReplaceWindow()
        {
            new ModifyMultipleStringsWindow(Application.Current.MainWindow).ShowDialog();
        }

        #endregion
    }
}
