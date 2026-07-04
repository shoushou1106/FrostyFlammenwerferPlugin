using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
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
    /// The diff Flammenwerfer records for a single localized-text asset: strings added or
    /// changed, plus (as a Flammenwerfer-only extension) strings marked for removal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ReadInternal"/>/<see cref="SaveInternal"/> read and write three format
    /// layers, in this order, and this layering is the entire reason Flammenwerfer can claim
    /// two-way project/mod compatibility with the original FsLocalizationPlugin:
    /// </para>
    /// <list type="number">
    /// <item>Legacy FsLocalizationPlugin format (no magic; the first value read is
    /// directly a string count, one byte per character).</item>
    /// <item>Current FsLocalizationPlugin format (magic <c>0xABCD0001</c>, two bytes per
    /// character).</item>
    /// <item>Flammenwerfer's own trailing extension (magic <c>0xF1A88E22</c>, "FLAMMENN"),
    /// carrying the removal list. The original FsLocalizationPlugin never writes this and
    /// never reads it - it just runs out of bytes at that point, which
    /// <see cref="ReadInternal"/> turns into "nothing was removed" rather than a crash.</item>
    /// </list>
    /// <para>
    /// The first two layers are byte-for-byte compatible with the original
    /// FsLocalizationPlugin and must never change. The third layer is entirely
    /// Flammenwerfer's own and safe to extend - it carries its own format-version field for
    /// exactly that reason - as long as the read stays wrapped in a try/catch so files
    /// without it (i.e. saved by the original FsLocalizationPlugin) still load cleanly.
    /// </para>
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
                // Legacy FsLocalizationPlugin format - see the type-level remarks.
                // Do not change this branch.
#if FROSTY_DEVELOPER
                App.Logger.Log("[Debug] FsLocalization Old Format Detected");
#endif
                int legacyCount = (int)fsMagic;
                for (int i = 0; i < legacyCount; i++)
                {
                    uint hash = reader.ReadUInt();
                    string str = reader.ReadNullTerminatedString();
                    AddString(hash, str);
                }
                return;
            }

            // New FsLocalizationPlugin format - see the type-level remarks.
            // Do not change this branch.
            int stringsCount = reader.ReadInt();
            strings.Clear();
            for (int i = 0; i < stringsCount; i++)
            {
                uint hash = reader.ReadUInt();
                string str = reader.ReadNullTerminatedWideString();
                AddString(hash, str);
            }

            // Flammenwerfer extension - see the type-level remarks. A project/mod saved by
            // the original FsLocalizationPlugin simply has no more bytes here, so any read
            // failure (end of stream, garbage magic) is treated as nothing.
            try
            {
                uint flammenMagic = reader.ReadUInt();
                if (flammenMagic == FlammenwerferExtensionMagic)
                {
                    uint formatVersion = reader.ReadUInt();
#if FROSTY_DEVELOPER
                    App.Logger.Log($"[Debug] Flammenwerfer Extended Format Detected, Version {formatVersion}");
#endif
                    if (formatVersion == FlammenwerferExtensionFormatVersion)
                    {
                        // FormatVersion: 1
                        int stringsToRemoveCount = reader.ReadInt();
                        stringsToRemove.Clear();
                        for (int i = 0; i < stringsToRemoveCount; i++)
                        {
                            uint hash = reader.ReadUInt();
                            RemoveString(hash);
                        }
                    }
                    // Else: a newer, unrecognized extension format version. Everything
                    // before it parsed fine, but there's nothing more we can safely read
                    // from this trailing block.
                }
#if FROSTY_DEVELOPER
                else
                {
                    App.Logger.Log("[Debug] FsLocalization New Format Detected");
                }
#endif
            }
            catch
            {
                stringsToRemove.Clear();
            }
        }

        public override void SaveInternal(NativeWriter writer)
        {
            // New FsLocalizationPlugin format - see the type-level remarks.
            // Do not change this section.
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

            // Flammenwerfer extension - see the type-level remarks.
            writer.Write(FlammenwerferExtensionMagic);
            writer.Write(FlammenwerferExtensionFormatVersion);

            // FormatVersion: 1
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
        /// Reverts a string with the specified ID back to its unmodified, baseline value.
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
        /// Gets the modified value of a string, or <see langword="null"/> if this diff
        /// doesn't touch it.
        /// </summary>
        public string GetString(uint id)
        {
            return strings.TryGetValue(id, out string value) ? value : null;
        }

        /// <summary>
        /// Enumerates the hash IDs of every string this diff adds or changes (not
        /// including removals).
        /// </summary>
        public IEnumerable<uint> EnumerateStrings()
        {
            foreach (uint key in strings.Keys)
                yield return key;
        }

        /// <summary>
        /// Merges another diff into this one - used when Frosty Mod Manager combines two
        /// mods that both touch the same localized-text asset. <paramref name="other"/>'s
        /// added/changed strings win on conflict.
        /// </summary>
        public void Merge(ModifiedFsLocalizationAsset other)
        {
            foreach (uint key in other.strings.Keys)
                strings[key] = other.strings[key];

            try
            {
                // Defensive: guards against a stringsToRemove that failed to come back
                // populated from vanilla FsLocalizationPlugin ModifiedResource.
                stringsToRemove.UnionWith(other.stringsToRemove);
            }
            catch { }
        }
    }

    /// <summary>
    /// The <see cref="EbxAsset"/> wrapper Frosty hands us for a <c>UITextDatabase</c>'s
    /// localized-text child asset. Thin - it just forwards to the underlying
    /// <see cref="ModifiedFsLocalizationAsset"/> diff.
    /// </summary>
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

    /// <summary>
    /// Flammenwerfer's implementation of Frosty's <see cref="ILocalizedStringDatabase"/> -
    /// the interface the editor (and any other plugin, e.g. LocalizedStringPlugin) uses to
    /// read and edit localized text without caring which localization system a given game
    /// actually uses under the hood.
    /// </summary>
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

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx("LocalizationAsset"))
            {
                // read master localization asset
                dynamic localizationAsset = App.AssetManager.GetEbx(entry).RootObject;

                bool foundLanguage = false;

                // iterate through localized texts
                foreach (PointerRef pointer in localizationAsset.LocalizedTexts)
                {
                    EbxAssetEntry textEntry = App.AssetManager.GetEbxEntry(pointer.External.FileGuid);
                    if (textEntry == null)
                        continue;

                    loadedDatabase = App.AssetManager.GetEbxAs<FsLocalizationAsset>(textEntry);

                    // Peek at the language before committing to this as loadedDatabase - a
                    // LocalizationAsset can list texts for several languages, and only one
                    // of them should ever end up as the active database.
                    dynamic localizedText = loadedDatabase.RootObject;
                    if (localizedText.Language.ToString() != language)
                        continue;

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

            // load chunk
            if (stringChunk != Guid.Empty && histogramChunk != Guid.Empty)
            {
                ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(stringChunk);
                ChunkAssetEntry histogramEntry = App.AssetManager.GetChunkEntry(histogramChunk);

                if (chunkEntry != null && histogramEntry != null)
                {
                    // only load if chunk exists
                    strings = Flammen.ReadStrings(histogramEntry, chunkEntry);
                }
            }
        }

        private void OnLoadedTextAssetModified(object sender, EventArgs e)
        {
            loadedDatabase = App.AssetManager.GetEbxAs<FsLocalizationAsset>(subscribedTextEntry);
        }

        /// <summary>
        /// Enumerates every string hash visible for the current language: everything in
        /// the modified diff (minus anything marked for removal), plus every baseline
        /// string not shadowed by a modification.
        /// </summary>
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

        /// <summary>
        /// Enumerates only the string hashes touched by the modified diff (added, changed,
        /// or - unlike <see cref="EnumerateStrings"/> - still including removed ones).
        /// </summary>
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

        /// <summary>
        /// Attempts to get the current value of a string, without the "[Error] ..."
        /// placeholder text <see cref="GetString(uint)"/> returns for display purposes.
        /// </summary>
        /// <param name="id">The hash ID of the string.</param>
        /// <param name="value">The string's current value, if it has one.</param>
        /// <returns><see langword="true"/> if the string exists and isn't removed.</returns>
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

        /// <summary>
        /// Adds a new string under the given string ID (e.g. <c>ID_FLAME</c>) and returns
        /// its computed hash.
        /// </summary>
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

        /// <summary>
        /// Marks a string for removal. Not supported by the original FsLocalizationPlugin -
        /// see the removal-list section of <see cref="ModifiedFsLocalizationAsset"/>'s remarks.
        /// </summary>
        public void RemoveString(uint id)
        {
            loadedDatabase.RemoveString(id);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);
        }
    }
}
