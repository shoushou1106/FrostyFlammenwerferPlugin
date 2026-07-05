using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using FsLocalizationPlugin.Options;
using FsLocalizationPlugin.Windows;
using System;
using System.Collections.Generic;
using System.Windows;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

#pragma warning disable IDE0130 // Namespace does not match folder structure
// Not using correct namespace due to FsLocalization backwards compatibility
//namespace FsLocalizationPlugin.Resources
namespace FsLocalizationPlugin
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// The diff Flammenwerfer records for a localized-text asset: strings added, and removed.
    /// </summary>
    /// <remarks>
    /// Read/write three format layers in order - this is why Flammenwerfer stays two-way
    /// compatible with the original FsLocalizationPlugin:
    /// 1. Legacy format: magic is the count, one byte per char. Never change.
    /// 2. Current format: magic 0xABCD0001, two bytes per char. Never change.
    /// 3. Flammenwerfer's own trailing extension (magic 0xF1A88E22, "FLAMMENN")
    ///    The original plugin never writes or reads this, it just runs out of bytes.
    /// Layer 3 is ours and safe to extend (it has its own format-version field) as long as
    /// the read stays wrapped in try/catch.
    /// </remarks>
    public class ModifiedFsLocalizationAsset : ModifiedResource
    {
        private const uint FlammenwerferExtensionMagic = 0xF1A88E22; // "FLAMMENN"
        private const uint FlammenwerferExtensionFormatVersion = 1;

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
                FlammenwerferOptions.DebugLog("ModifiedResource.ReadInternal", "FsLocalization Old Format Detected");
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
            strings.Clear();
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
                    FlammenwerferOptions.DebugLog("ModifiedResource.ReadInternal", "Flammenwerfer Extended Format Detected, Version {0}", formatVersion);
                    if (formatVersion == FlammenwerferExtensionFormatVersion)
                    {
                        // FormatVersion: 1
                        // String removal support
                        int stringsToRemoveCount = reader.ReadInt();
                        stringsToRemove.Clear();
                        for (int i = 0; i < stringsToRemoveCount; i++)
                        {
                            uint hash = reader.ReadUInt();
                            RemoveString(hash);
                        }
                    }
                    // Else: newer, unrecognized format version - nothing more to read safely.
                }
                else
                {
                    FlammenwerferOptions.DebugLog("ModifiedResource.ReadInternal", "FsLocalization New Format Detected");
                }
            }
            catch
            {
                stringsToRemove.Clear();
            }
        }

        public override void SaveInternal(NativeWriter writer)
        {
            // New FsLocalizationPlugin format
            writer.Write(0xABCD0001);
            writer.Write(strings.Count);

            foreach (KeyValuePair<uint, string> kvp in strings)
            {
                writer.Write(kvp.Key);
                string s = kvp.Value;
                foreach (char c in s)
                {
                    writer.Write((ushort)c);
                }
                writer.Write((ushort)0);
            }

            // Flammenwerfer extension.
            // Vanilla-saved files will run out of bytes here.
            writer.Write(FlammenwerferExtensionMagic);
            writer.Write(FlammenwerferExtensionFormatVersion);

            // FormatVersion: 1
            // String removal support
            writer.Write(stringsToRemove.Count);
            foreach (uint value in stringsToRemove)
                writer.Write(value);
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

    /// <summary>Flammenwerfer's ILocalizedStringDatabase. How the editor (and other plugins) read/edit localized text.</summary>
    public class FsLocalizationStringDatabase : ILocalizedStringDatabase
    {
        private Dictionary<uint, string> strings = new Dictionary<uint, string>();
        private FsLocalizationAsset loadedDatabase = null;
        private EbxAssetEntry subscribedTextEntry = null;

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
                FlammenwerferOptions.DebugLog("Database.Initialize", "No LocalizationAsset found for language {0}", language);
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
                    FlammenwerferOptions.DebugLog("Database.Initialize", "Loaded {0} strings for language {1}", strings.Count, language);
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

        /// <summary>Whether a string has been marked for removal.</summary>
        public bool IsStringRemoved(uint id)
        {
            return loadedDatabase != null && loadedDatabase.GetStringsToRemove().Contains(id);
        }

        /// <summary>Adds a new string under a string ID (e.g. <c>ID_FLAME</c>) and returns its hash.</summary>
        public uint AddString(string id, string value)
        {
            uint hash = LocalizationHelper.HashStringId(id);

            loadedDatabase.AddString(hash, value);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);

            return hash;
        }

        public void RevertString(uint id)
        {
            loadedDatabase.RevertString(id);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);
        }

        public void SetString(uint id, string value)
        {
            loadedDatabase.AddString(id, value);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);
        }

        public void SetString(string id, string value)
        {
            SetString(LocalizationHelper.HashStringId(id), value);
        }

        public void AddStringWindow()
        {
            new ModifyStringWindow(Application.Current.MainWindow).ShowDialog();
        }

        public void BulkReplaceWindow()
        {
            new ModifyMultipleStringsWindow(Application.Current.MainWindow).ShowDialog();
        }

        public bool isStringEdited(uint id)
        {
            return loadedDatabase != null && loadedDatabase.GetStrings().ContainsKey(id);
        }

        /// <summary>Marks a string for removal. Not supported by the original FsLocalizationPlugin.</summary>
        public void RemoveString(uint id)
        {
            loadedDatabase.RemoveString(id);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);
        }
    }
}
