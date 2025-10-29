using FlammenwerferPlugin.Resources;
using Frosty.Core;
using Frosty.Core.IO;
using Frosty.Core.Mod;
using Frosty.Hash;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FlammenwerferPlugin.Handlers
{
    /// <summary>
    /// Custom action handler for FsLocalization assets that provides merging support for localized strings.
    /// This handler processes histogram and binary string chunks to enable multi-language support in Frostbite games.
    /// </summary>
    public class FsLocalizationCustomActionHandler : ICustomActionHandler
    {
        public HandlerUsage Usage => HandlerUsage.Merge;

        /// <summary>
        /// Represents a localization resource for mod packaging.
        /// </summary>
        private class FsLocalizationResource : EditorModResource
        {
            public override ModResourceType Type => ModResourceType.Ebx;
            public FsLocalizationResource(EbxAssetEntry entry, FrostyModWriter.Manifest manifest)
                : base(entry)
            {
                ModifiedResource md = entry.ModifiedEntry.DataObject as ModifiedResource;
                byte[] data = md.Save();

                name = entry.Name.ToLower();
                sha1 = Utils.GenerateSha1(data);
                resourceIndex = manifest.Add(sha1, data);
                size = data.Length;
                handlerHash = Fnv1.HashString(entry.Type.ToLower());
            }

            public override void Write(NativeWriter writer)
            {
                base.Write(writer);
            }
        }

        #region -- Editor Specific --

        /// <summary>
        /// Saves the localization asset to a mod file.
        /// </summary>
        public void SaveToMod(FrostyModWriter writer, AssetEntry entry)
        {
            writer.AddResource(new FsLocalizationResource(entry as EbxAssetEntry, writer.ResourceManifest));
        }

        #endregion

        #region -- Mod Manager Specific --

        /// <summary>
        /// Gets the list of available resource actions (currently none).
        /// </summary>
        public IEnumerable<string> GetResourceActions(string name, byte[] data)
        {
            return new List<string>();
        }

        /// <summary>
        /// Loads and merges localization data from multiple mods.
        /// </summary>
        public object Load(object existing, byte[] newData)
        {
            ModifiedFsLocalizationAsset newFs = (ModifiedFsLocalizationAsset)ModifiedResource.Read(newData);
            ModifiedFsLocalizationAsset oldFs = (ModifiedFsLocalizationAsset)existing;

            if (oldFs == null)
                return newFs;

            oldFs.Merge(newFs);
            return oldFs;
        }

        /// <summary>
        /// Modifies the localization asset by updating histogram and string binary chunks.
        /// </summary>
        public void Modify(AssetEntry origEntry, AssetManager am, RuntimeResources runtimeResources, object data, out byte[] outData)
        {
#if DEVELOPER___DEBUG
            try
            {
#endif
                ModifiedFsLocalizationAsset modFs = data as ModifiedFsLocalizationAsset;

                EbxAsset ebxAsset = am.GetEbx(am.GetEbxEntry(origEntry.Name));
                dynamic localizedText = ebxAsset.RootObject;

                ChunkAssetEntry histogramEntry = am.GetChunkEntry(localizedText.HistogramChunk);
                ChunkAssetEntry stringChunkEntry = am.GetChunkEntry(localizedText.BinaryChunk);

                if (stringChunkEntry != null && histogramEntry != null)
                {
                    // Generate new histogram and string data
                    Flammen.Flammen.WriteAll(histogramEntry, stringChunkEntry, modFs.strings,
                        out byte[] newHistogramData,
                        out byte[] newStringData);

                    // Process and add histogram chunk
                    ProcessHistogramChunk(localizedText, histogramEntry, newHistogramData, 
                        origEntry.Name, runtimeResources);

                    // Process and add string chunk
                    ProcessStringChunk(localizedText, stringChunkEntry, newStringData, 
                        origEntry.Name, runtimeResources);
                }

                using (EbxBaseWriter writer = EbxBaseWriter.CreateWriter(new MemoryStream()))
                {
                    writer.WriteAsset(ebxAsset);
                    origEntry.OriginalSize = writer.Length;
                    outData = Utils.CompressFile(writer.ToByteArray());
                }

                origEntry.Size = outData.Length;
                origEntry.Sha1 = Utils.GenerateSha1(outData);
#if DEVELOPER___DEBUG
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("An unhandled exception has occurred");
                sb.Append("Type=");
                sb.AppendLine(ex.GetType().ToString());
                sb.Append("HResult=");
                sb.AppendLine("0x" + ex.HResult.ToString("X"));
                sb.Append("Message=");
                sb.AppendLine(ex.Message);
                sb.Append("Source=");
                sb.AppendLine(ex.Source);
                sb.AppendLine("StackTrace:");
                sb.AppendLine(ex.StackTrace);
                App.Logger.LogError(sb.ToString());

                outData = null;
            }
#endif
        }

        #endregion

        #region -- Helper Methods --

        /// <summary>
        /// Processes the histogram chunk by compressing and adding it to runtime resources.
        /// </summary>
        private void ProcessHistogramChunk(dynamic localizedText, ChunkAssetEntry originalEntry, 
            byte[] data, string entryName, RuntimeResources runtimeResources)
        {
            localizedText.HistogramChunkSize = (uint)data.Length;

            ChunkAssetEntry newEntry = new ChunkAssetEntry
            {
                LogicalSize = (uint)data.Length,
                Id = originalEntry.Id,
                H32 = Fnv1.HashString(entryName.ToLower()),
                FirstMip = -1,
                IsTocChunk = true
            };

            byte[] compressedData = Utils.CompressFile(data);
            newEntry.Sha1 = Utils.GenerateSha1(compressedData);
            newEntry.Size = compressedData.Length;

            runtimeResources.AddResource(new RuntimeChunkResource(newEntry), compressedData);
        }

        /// <summary>
        /// Processes the string binary chunk by compressing and adding it to runtime resources.
        /// </summary>
        private void ProcessStringChunk(dynamic localizedText, ChunkAssetEntry originalEntry, 
            byte[] data, string entryName, RuntimeResources runtimeResources)
        {
            localizedText.BinaryChunkSize = (uint)data.Length;

            ChunkAssetEntry newEntry = new ChunkAssetEntry
            {
                LogicalSize = (uint)data.Length,
                Id = originalEntry.Id,
                H32 = Fnv1.HashString(entryName.ToLower()),
                FirstMip = -1,
                IsTocChunk = true
            };

            byte[] compressedData = Utils.CompressFile(data);
            newEntry.Sha1 = Utils.GenerateSha1(compressedData);
            newEntry.Size = compressedData.Length;

            runtimeResources.AddResource(new RuntimeChunkResource(newEntry), compressedData);
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for NativeWriter to support localization string operations.
    /// </summary>
    public static class WriterStringExtension
    {
        /// <summary>
        /// Writes a null-terminated string with one byte per character (truncating to byte range).
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="str">The string to write.</param>
        public static void WriteNullTerminatedOneBytePerCharString(this NativeWriter writer, string str)
        {
            foreach (char c in str)
            {
                writer.Write((byte)c);
            }
            writer.Write((byte)0x00);
        }
    }
}
