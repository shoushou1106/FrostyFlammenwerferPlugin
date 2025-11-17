using FsLocalizationPlugin.Windows;
using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

//using FrostySdk.Managers.Entries; // Uncomment this line [For 1.0.7]

//namespace FsLocalizationPlugin.Resources
// Frosty cannot detect a different namespace
namespace FsLocalizationPlugin
{
    /// <summary>
    /// Provides utility methods for localization string operations.
    /// </summary>
    public static class LocalizationHelper
    {
        /// <summary>
        /// Computes a hash for a string ID using a custom hashing algorithm.
        /// This is compatible with the Frostbite engine's string ID hashing.
        /// </summary>
        /// <param name="stringId">The string ID to hash.</param>
        /// <returns>The 32-bit hash value.</returns>
        public static uint HashStringId(string stringId)
        {
            if (string.IsNullOrEmpty(stringId))
                return 0xFFFFFFFF;

            uint result = 0xFFFFFFFF;
            foreach (char c in stringId)
            {
                result = c + 33 * result;
            }
            return result;
        }
    }

    public class ModifiedFsLocalizationAsset : ModifiedResource
    {
        public Dictionary<uint, string> strings = new Dictionary<uint, string>();

        public List<uint> stringsToRemove = new List<uint>();

        public ModifiedFsLocalizationAsset()
        {
        }

        public override void ReadInternal(NativeReader reader)
        {
            uint magic = reader.ReadUInt();
            if (magic != 0xABCD0001)
            {
                int countOld = (int)magic;
                for (int i = 0; i < countOld; i++)
                {
                    uint hash = reader.ReadUInt();
                    string str = reader.ReadNullTerminatedString();
                    AddString(hash, str);
                }
                return;
            }
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                uint hash = reader.ReadUInt();
                string str = reader.ReadNullTerminatedWideString();
                AddString(hash, str);
            }
        }

        public override void SaveInternal(NativeWriter writer)
        {
            writer.Write(0xABCD0001);
            writer.Write(strings.Count);
            for (int i = 0; i < strings.Count; i++)
            {
                writer.Write(strings.Keys.ElementAt(i));
                string s = strings.Values.ElementAt(i);
                foreach (char c in s)
                    writer.Write((ushort)c);
                writer.Write((ushort)0);
            }
        }

        /// <summary>
        /// Adds or updates a string with the specified ID.
        /// </summary>
        /// <param name="id">The hash ID of the string.</param>
        /// <param name="str">The string value.</param>
        public void AddString(uint id, string str)
        {
            strings[id] = str;
        }

        /// <summary>
        /// Revert a string with the specified ID.
        /// </summary>
        /// <param name="id">The hash ID of the string to revert.</param>
        public void RemoveString(uint id)
        {
            strings.Remove(id);
        }

        /// <summary>
        /// Remove a string with the specified ID.
        /// </summary>
        /// <param name="id">The hash ID of the string to remove.</param>
        public void DeleteString(uint id)
        {
            strings.Remove(id);
            stringsToRemove.Add(id);
        }

        public string GetString(uint id)
        {
            if (!strings.ContainsKey(id))
                return null;
            return strings[id];
        }

        public IEnumerable<uint> EnumerateStrings()
        {
            foreach (uint key in strings.Keys)
                yield return key;
        }

        public void Merge(ModifiedFsLocalizationAsset other)
        {
            foreach (uint key in other.strings.Keys)
            {
                if (strings.ContainsKey(key))
                    strings[key] = other.strings[key];
                else
                    strings.Add(key, other.strings[key]);
            }
        }
    }

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

        public void RemoveString(uint id)
        {
            modified.RemoveString(id);
        }
        public void DeleteString(uint id)
        {
            modified.DeleteString(id);
        }

        public IEnumerable<uint> EnumerateStrings()
        {
            return modified.EnumerateStrings();
        }

        public Dictionary<uint, string> GetStrings()
        {
            return modified.strings;
        }

        public List<uint> GetStringsToRemove()
        {
            return modified.stringsToRemove;
        }
    }

    public class FsLocalizationStringDatabase : ILocalizedStringDatabase
    {
        private Dictionary<uint, string> strings = new Dictionary<uint, string>();
        private FsLocalizationAsset loadedDatabase = null;

        /// <summary>
        /// Initializes the localization database by loading strings for the configured language.
        /// </summary>
        public void Initialize()
        {
            string language = "LanguageFormat_" + Config.Get("Language", "English", ConfigScope.Game);

            Guid stringChunk = Guid.Empty;
            Guid histogramChunk = Guid.Empty;

            strings.Clear();

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx("LocalizationAsset"))
            {
                // read master localization asset
                dynamic localizationAsset = App.AssetManager.GetEbx(entry).RootObject;

                // iterate through localized texts
                foreach (PointerRef pointer in localizationAsset.LocalizedTexts)
                {
                    EbxAssetEntry textEntry = App.AssetManager.GetEbxEntry(pointer.External.FileGuid);
                    if (textEntry == null)
                        continue;

                    // read localized text asset
                    loadedDatabase = App.AssetManager.GetEbxAs<FsLocalizationAsset>(textEntry);
                    dynamic localizedText = loadedDatabase.RootObject;

                    // check for language
                    if (localizedText.Language.ToString() == language)
                    {
                        textEntry.AssetModified += (o, e) =>
                        {
                            loadedDatabase = App.AssetManager.GetEbxAs<FsLocalizationAsset>(textEntry);
                        };
                        stringChunk = localizedText.BinaryChunk;
                        histogramChunk = localizedText.HistogramChunk;
                        break;
                    }
                }
            }

            // load chunk
            if (stringChunk != Guid.Empty && histogramChunk != Guid.Empty)
            {
                ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(stringChunk);
                ChunkAssetEntry histogramEntry = App.AssetManager.GetChunkEntry(histogramChunk);

                if (chunkEntry != null && histogramEntry != null)
                {
                    // only load if chunk exists
                    strings = strings.Concat(Flammen.Flammen.ReadStrings(histogramEntry, chunkEntry)).ToDictionary(k => k.Key, v => v.Value);
                    foreach (uint key in loadedDatabase.GetStringsToRemove())
                    {
                        strings.Remove(key);
                    }
                }
            }
        }

        public IEnumerable<uint> EnumerateStrings()
        {
            foreach (uint key in strings.Keys)
                yield return key;
            foreach (uint key in loadedDatabase.EnumerateStrings())
                yield return key;
        }
        public IEnumerable<uint> EnumerateModifiedStrings()
        {
            foreach (uint key in loadedDatabase.EnumerateStrings())
                yield return key;
        }

        public string GetString(uint id)
        {
            string value = loadedDatabase.GetString(id);
            if (value != null)
                return value;

            if (!strings.ContainsKey(id))
            {
                if (id == 0)
                    return "";
                return string.Format("Invalid StringId: {0}", id.ToString("X8"));
            }

            return strings[id];
        }

        public string GetString(string stringId)
        {
            return GetString(LocalizationHelper.HashStringId(stringId));
        }

        public uint AddString(string id, string value)
        {
            uint hash = LocalizationHelper.HashStringId(id);

            loadedDatabase.AddString(hash, value);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);

            return hash;
        }

        public void RevertString(uint id)
        {
            loadedDatabase.RemoveString(id);
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
            AddStringWindow win = new AddStringWindow(Application.Current.MainWindow);
            win.ShowDialog();
        }

        public void BulkReplaceWindow()
        {
            ReplaceMultipleStringWindow win = new ReplaceMultipleStringWindow(Application.Current.MainWindow);
            win.ShowDialog();
        }

        public bool isStringEdited(uint id)
        {
            return loadedDatabase.EnumerateStrings().Contains(id);
        }

        public void DeleteString(uint id)
        {
            loadedDatabase.DeleteString(id);
            strings.Remove(id);
            App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(loadedDatabase.FileGuid).Name, loadedDatabase);
        }
    }
}
