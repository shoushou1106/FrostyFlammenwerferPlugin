﻿using FlammenwerferPlugin.Windows;
using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlammenwerferPlugin.Resources
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
            for (int i = 0; i < stringId.Length; i++)
            {
                result = stringId[i] + 33 * result;
            }
            return result;
        }
    }

    public class ModifiedFsLocalizationAsset : ModifiedResource
    {
        public Dictionary<uint, string> strings = new Dictionary<uint, string>();

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
        /// Removes a string with the specified ID.
        /// </summary>
        /// <param name="id">The hash ID of the string to remove.</param>
        public void RemoveString(uint id)
        {
            if (strings.ContainsKey(id))
            {
                strings.Remove(id);
            }
            else
            {
                App.Logger.Log($"String 0x{id:X8} is not a modified string");
            }
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

        public IEnumerable<uint> EnumerateStrings()
        {
            return modified.EnumerateStrings();
        }

        public Dictionary<uint, string> GetStrings()
        {
            return modified.strings;
        }
    }

    public class FsLocalizationStringDatabase : ILocalizedStringDatabase
    {
        private Dictionary<uint, string> strings = new Dictionary<uint, string>();
        private FsLocalizationAsset loadedDatabase = null;

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
            AddStringWindow win = new AddStringWindow();
            win.ShowDialog();
        }

        public void BulkReplaceWindow()
        {
            ReplaceMultipleStringWindow win = new ReplaceMultipleStringWindow();
            win.ShowDialog();
        }

        public bool isStringEdited(uint id)
        {
            return loadedDatabase.EnumerateStrings().Contains(id);
        }
    }
}
