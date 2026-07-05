using Frosty.Core;
using Frosty.Core.IO;
using Frosty.Core.Mod;
using Frosty.Hash;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using FsLocalizationPlugin.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

#pragma warning disable IDE0130 // Namespace does not match folder structure
// Not using correct namespace due to FsLocalization backwards compatibility
//namespace FsLocalizationPlugin.Handlers
namespace FsLocalizationPlugin
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// The mod merge/bake pipeline for <c>UITextDatabase</c> ebx assets, registered in
    /// <c>Properties/AssemblyInfo.cs</c>.
    /// </summary>
    /// <remarks>
    /// Editor side: <see cref="SaveToMod"/> serializes the diff into the .fbmod file.
    /// Mod Manager side: <see cref="Load"/> merges diffs from multiple mods,
    /// <see cref="GetResourceActions"/> describes changes for the mod info UI, and
    /// <see cref="Modify"/> applies the merged diff into new histogram/strings-binary chunks.
    /// </remarks>
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
            FlammenwerferOptions.DebugLog("ActionHandler.SaveToMod", "Saving {0} to mod", entry.Name);
            writer.AddResource(new FsLocalizationResource(entry as EbxAssetEntry, writer.ResourceManifest));
        }

        #endregion

        #region -- Mod Manager Specific --

        public IEnumerable<string> GetResourceActions(string name, byte[] data)
        {
            ModifiedFsLocalizationAsset newFs = (ModifiedFsLocalizationAsset)ModifiedResource.Read(data);
            List<string> actions = new List<string>();
            string AssetName = name;
            foreach (uint stringId in newFs.EnumerateStrings())
            {
                string resourceName = stringId.ToString("x8");
                string resourceType = "ebx";
                string action = "Add";

                actions.Add(AssetName + " [" + resourceName + "];" + resourceType + ";" + action);
            }

            FlammenwerferOptions.DebugLog("ActionHandler.GetResourceActions", "{0} has {1} action(s)", name, actions.Count);
            return actions;
        }

        public object Load(object existing, byte[] newData)
        {
            ModifiedFsLocalizationAsset newFs = (ModifiedFsLocalizationAsset)ModifiedResource.Read(newData);
            ModifiedFsLocalizationAsset oldFs = (ModifiedFsLocalizationAsset)existing;

            if (oldFs == null)
            {
                FlammenwerferOptions.DebugLog("ActionHandler.Load", "First mod touching this asset, {0} string(s), {1} removal(s)", newFs.strings.Count, newFs.stringsToRemove.Count);
                return newFs;
            }

            oldFs.Merge(newFs);
            FlammenwerferOptions.DebugLog("ActionHandler.Load", "Merged mod, now {0} string(s), {1} removal(s)", oldFs.strings.Count, oldFs.stringsToRemove.Count);
            return oldFs;
        }

        public void Modify(AssetEntry origEntry, AssetManager am, RuntimeResources runtimeResources, object data, out byte[] outData)
        {
            FlammenwerferOptions.DebugLog("ActionHandler.Modify", "Start applying handler");

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

                    Flammen.WriteAll(am, histogramEntry, stringChunkEntry, modFs.strings, modFs.stringsToRemove,
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
#if !FROSTY_107
                    newHistogramChunkEntry.IsTocChunk = true;
#endif

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
#if !FROSTY_107
                    newStringChunkEntry.IsTocChunk = true;
#endif

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
                sb.AppendLine("Inferno Out of Control! An unhandled exception has occurred.");
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
                sb.AppendLine("Returning a placeholder object. Your game may crash. But Frosty survive for you to read this message.");
                App.Logger.LogError("{0}", sb.ToString());

                // Return placeholder data instead of null, so Mod Manager doesn't crash and the
                // user can read the log above. The mod is already broken at this point.
                using (EbxBaseWriter writer = EbxBaseWriter.CreateWriter(new MemoryStream()))
                {
                    // Create a new EbxAsset without calling AssetManager. So it works on some 1.0.7 Mod Manager.
                    EbxAsset ebxAsset = new EbxAsset(TypeLibrary.CreateObject(origEntry.Type));
                    writer.WriteAsset(ebxAsset);
                    origEntry.OriginalSize = writer.Length;
                    outData = Utils.CompressFile(writer.ToByteArray());
                }
            }
            FlammenwerferOptions.DebugLog("ActionHandler.Modify", "End applying handler");
        }

        #endregion
    }
}
