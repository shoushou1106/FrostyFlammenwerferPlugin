using Frosty.Core;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif


namespace FsLocalizationPlugin
{
    /// <summary>
    /// Shared helpers for localization string IDs, chunk-owning language discovery, and
    /// the handful of small routines that used to be copy-pasted across every Windows/*
    /// code-behind file. Kept in the flat <c>FsLocalizationPlugin</c> namespace alongside
    /// <see cref="Flammen"/> so both the editor windows and the format layer can reach it
    /// without an extra <c>using</c>.
    /// </summary>
    public static class LocalizationHelper
    {
        private static List<string> cachedLanguages;
        private static string cachedProfileName;

        /// <summary>
        /// Computes a hash for a string ID using a custom hashing algorithm.
        /// This is compatible with Frostbite's FsLocalization string ID hashing.
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

        /// <summary>
        /// Attempts to resolve a user-typed value - a string ID (<c>ID_FLAME</c>), a bare
        /// 8-digit hex hash (<c>DEADBEEF</c>), or a <c>0x</c>-prefixed hex hash - into its
        /// 32-bit string hash.
        /// </summary>
        /// <param name="hashOrId">The raw text entered by the user. May be null.</param>
        /// <param name="hash">The resolved hash, if parsing succeeded.</param>
        /// <returns><see langword="true"/> if <paramref name="hashOrId"/> could be parsed.</returns>
        public static bool TryParseHashOrId(string hashOrId, out uint hash)
        {
            hash = 0;
            if (hashOrId == null)
                return false;

            try
            {
                if (hashOrId.StartsWith("ID"))
                {
                    hash = HashStringId(hashOrId);
                    return true;
                }
                if (hashOrId.Length == 8)
                {
                    hash = Convert.ToUInt32(hashOrId, 16);
                    return true;
                }
                if (hashOrId.Length == 10 && (hashOrId.StartsWith("0x") || hashOrId.StartsWith("0X")))
                {
                    hash = Convert.ToUInt32(hashOrId.Remove(0, 2), 16);
                    return true;
                }
            }
            catch
            {
                // Malformed hex, or a wildly out-of-range hash - treat it as unparsable.
            }
            return false;
        }

        /// <summary>
        /// Enumerates every language a <c>LocalizationAsset</c> in the currently loaded
        /// profile provides text for.
        /// </summary>
        /// <param name="modifiedOnly">
        /// When <see langword="true"/>, only languages that currently have a modified
        /// (edited) localized-text asset are returned. Never cached, since modification
        /// state changes as the user edits strings.
        /// </param>
        /// <returns>
        /// A sorted, de-duplicated list of language names (e.g. <c>English</c>). When
        /// <paramref name="modifiedOnly"/> is <see langword="false"/> and no
        /// <c>LocalizationAsset</c> is found at all, falls back to a single-item list
        /// containing <c>English</c>. When <paramref name="modifiedOnly"/> is
        /// <see langword="true"/>, an empty result simply means nothing is modified yet.
        /// </returns>
        public static List<string> GetLocalizedLanguages(bool modifiedOnly = false)
        {
            if (!modifiedOnly && cachedLanguages != null && cachedProfileName == ProfilesLibrary.ProfileName)
                return cachedLanguages;

            HashSet<string> languages = new HashSet<string>();
            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx("LocalizationAsset"))
            {
                // Read the master localization asset.
                dynamic localizationAsset = App.AssetManager.GetEbxAs<FsLocalizationAsset>(entry).RootObject;

                foreach (PointerRef pointer in localizationAsset.LocalizedTexts)
                {
                    EbxAssetEntry textEntry = App.AssetManager.GetEbxEntry(pointer.External.FileGuid);
                    if (textEntry == null)
                        continue;

                    if (modifiedOnly && !textEntry.IsModified)
                        continue;

                    dynamic localizedText = App.AssetManager.GetEbxAs<FsLocalizationAsset>(textEntry).RootObject;
                    string lang = localizedText.Language.ToString().Replace("LanguageFormat_", "");
                    languages.Add(lang);
                }
            }

            if (!modifiedOnly && !languages.Any())
                languages.Add("English");

            List<string> result = languages.OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList();

            if (!modifiedOnly)
            {
                cachedLanguages = result;
                cachedProfileName = ProfilesLibrary.ProfileName;
            }

            return result;
        }

        /// <summary>
        /// Clears the cached full language list built by <see cref="GetLocalizedLanguages"/>.
        /// The set of languages a game profile provides is effectively fixed for the
        /// lifetime of an editor session, so this only needs to be called if a different
        /// game profile is loaded into the same process.
        /// </summary>
        public static void InvalidateLanguageCache()
        {
            cachedLanguages = null;
            cachedProfileName = null;
        }

        /// <summary>
        /// Reports progress for a multi-part, multi-item background operation through a
        /// <see cref="FrostyTaskWindow"/>'s logger, using the <c>"progress:"</c> prefix
        /// convention <see cref="FrostyTaskLogger"/> understands.
        /// </summary>
        /// <param name="logger">The task window's logger (<c>task.TaskLogger</c>).</param>
        /// <param name="current">The 1-based index of the current item within its part.</param>
        /// <param name="total">The total number of items in the current part.</param>
        /// <param name="currentPart">The 1-based index of the current part of the overall operation.</param>
        /// <param name="totalParts">The total number of parts in the overall operation.</param>
        /// <param name="detail">The 1-based index of the current sub-step of an item, if items have sub-steps.</param>
        /// <param name="totalDetails">The total number of sub-steps per item.</param>
        /// <remarks>
        /// <see cref="ILogger.Log"/> already runs on the background thread that the task
        /// window's callback executes on and returns immediately, so this reports
        /// synchronously - no need to hop through another <c>Task.Run</c>.
        /// </remarks>
        public static void ReportProgress(ILogger logger, double current, double total, double currentPart = 1, double totalParts = 1, double detail = 1, double totalDetails = 1)
        {
            if (total <= 0)
                return;

            // (((((p - 1) * t) + (c - 1)) * td + d) / (tp * t * td)) * 100%
            double percent = (((((currentPart - 1) * total) + (current - 1)) * totalDetails) + detail) / (totalDetails * total * totalParts) * 100.0d;
            logger.Log("progress:" + percent);
        }
    }
}
