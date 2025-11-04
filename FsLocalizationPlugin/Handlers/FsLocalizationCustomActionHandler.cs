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

//namespace FsLocalizationPlugin.Handlers
// Frosty cannot detect a different namespace
namespace FsLocalizationPlugin
{
    public class FsLocalizationCustomActionHandler : ICustomActionHandler
    {
        public HandlerUsage Usage => HandlerUsage.Merge;

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

        public void SaveToMod(FrostyModWriter writer, AssetEntry entry)
        {
            writer.AddResource(new FsLocalizationResource(entry as EbxAssetEntry, writer.ResourceManifest));
        }

        #endregion

        #region -- Mod Manager Specific --

        public IEnumerable<string> GetResourceActions(string name, byte[] data)
        {
            return new List<string>();
        }

        public object Load(object existing, byte[] newData)
        {
            ModifiedFsLocalizationAsset newFs = (ModifiedFsLocalizationAsset)ModifiedResource.Read(newData);
            ModifiedFsLocalizationAsset oldFs = (ModifiedFsLocalizationAsset)existing;

            if (oldFs == null)
                return newFs;

            oldFs.Merge(newFs);
            return oldFs;
        }

        public void Modify(AssetEntry origEntry, AssetManager am, RuntimeResources runtimeResources, object data, out byte[] outData)
        {
            try
            {
                ModifiedFsLocalizationAsset modFs = data as ModifiedFsLocalizationAsset;

                EbxAsset ebxAsset = am.GetEbx(am.GetEbxEntry(origEntry.Name));
                dynamic localizedText = ebxAsset.RootObject;

                ChunkAssetEntry histogramEntry = am.GetChunkEntry(localizedText.HistogramChunk);
                ChunkAssetEntry stringChunkEntry = am.GetChunkEntry(localizedText.BinaryChunk);

                if (stringChunkEntry != null && histogramEntry != null)
                {
                    ChunkAssetEntry newHistogramChunkEntry = new ChunkAssetEntry();
                    ChunkAssetEntry newStringChunkEntry = new ChunkAssetEntry();

                    // Modify Chunks
                    Flammen.Flammen.WriteAll(am, histogramEntry, stringChunkEntry, modFs.strings,
                        out byte[] newHistogramData,
                        out byte[] newStringData);

                    // Process Histogram Chunk
                    localizedText.HistogramChunkSize = (uint)newHistogramData.Length;

                    newHistogramChunkEntry.LogicalSize = (uint)newHistogramData.Length;
                    newHistogramData = Utils.CompressFile(newHistogramData);

                    newHistogramChunkEntry.Id = histogramEntry.Id;
                    newHistogramChunkEntry.Sha1 = Utils.GenerateSha1(newHistogramData);
                    newHistogramChunkEntry.Size = newHistogramData.Length;
                    newHistogramChunkEntry.H32 = Fnv1.HashString(origEntry.Name.ToLower());
                    newHistogramChunkEntry.FirstMip = -1;
                    newHistogramChunkEntry.IsTocChunk = true;

                    runtimeResources.AddResource(new RuntimeChunkResource(newHistogramChunkEntry), newHistogramData);

                    // Process String Chunk
                    localizedText.BinaryChunkSize = (uint)newStringData.Length;

                    newStringChunkEntry.LogicalSize = (uint)newStringData.Length;
                    newStringData = Utils.CompressFile(newStringData);

                    newStringChunkEntry.Id = stringChunkEntry.Id;
                    newStringChunkEntry.Sha1 = Utils.GenerateSha1(newStringData);
                    newStringChunkEntry.Size = newStringData.Length;
                    newStringChunkEntry.H32 = Fnv1.HashString(origEntry.Name.ToLower());
                    newStringChunkEntry.FirstMip = -1;
                    newStringChunkEntry.IsTocChunk = true;

                    runtimeResources.AddResource(new RuntimeChunkResource(newStringChunkEntry), newStringData);
                }

                using (EbxBaseWriter writer = EbxBaseWriter.CreateWriter(new MemoryStream()))
                {
                    writer.WriteAsset(ebxAsset);
                    origEntry.OriginalSize = writer.Length;
                    outData = Utils.CompressFile(writer.ToByteArray());
                }

                origEntry.Size = outData.Length;
                origEntry.Sha1 = Utils.GenerateSha1(outData);
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
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for NativeWriter to support localization string operations.
    /// </summary>
    public static class WriterStringExtension
    {
        /// <summary>
        /// Writes a null-terminated string with one byte per character.
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
