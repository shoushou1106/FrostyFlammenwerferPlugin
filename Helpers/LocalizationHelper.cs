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

namespace FsLocalizationPlugin.Helpers
{
    /// <summary>
    /// Shared helpers for string IDs and language discovery, used by the editor windows
    /// and the format layer. Kept flat alongside <see cref="Flammen"/>.
    /// </summary>
    public static class LocalizationHelper
    {
        private static List<string> cachedLanguages;
        private static string cachedProfileName;

        /// <summary>Hashes a string ID, compatible with Frostbite's FsLocalization hashing.</summary>
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

        /// <summary>Parse a string hex hash as uint</summary>
        /// <param name="input">Input hex, can be 8 hex digits or 0x plus 8 hex digits</param>
        public static bool TryParseHexHash(string input, out uint hash)
        {
            hash = 0;
            if (input == null)
                return false;

            string hex = input;
            if (input.Length == 10 && (input.StartsWith("0x") || input.StartsWith("0X")))
                hex = input.Substring(2);
            else if (input.Length != 8)
                return false;

            return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out hash);
        }

        /// <summary>Auto detects and resolves a hex hash (see <see cref="TryParseHexHash"/>) or any other text as a string ID into its uint hash.</summary>
        public static bool TryParseHashOrId(string hashOrId, out uint hash)
        {
            return TryParseHashOrId(hashOrId, out hash, out bool _);
        }

        /// <summary>Auto detects and resolves a hex hash (see <see cref="TryParseHexHash"/>) or any other text as a string ID into its uint hash. Reports whether the input was treated as ID text or a raw hash.</summary>
        public static bool TryParseHashOrId(string hashOrId, out uint hash, out bool isStringId)
        {
            isStringId = false;

            if (TryParseHexHash(hashOrId, out hash))
                return true;

            if (!string.IsNullOrEmpty(hashOrId))
            {
                hash = HashStringId(hashOrId);
                isStringId = true;
                return true;
            }
            return false;
        }

        /// <summary>Every language a LocalizationAsset in the current profile provides text for.</summary>
        /// <param name="modifiedOnly">Only languages with a modified localized-text asset. Never cached.</param>
        /// <returns>Sorted, de-duplicated language names. Falls back to "English" if none found (unless modifiedOnly).</returns>
        public static List<string> GetLocalizedLanguages(bool modifiedOnly = false)
        {
            if (!modifiedOnly && cachedLanguages != null && cachedProfileName == ProfilesLibrary.ProfileName)
            {
                DebugLogHelper.Log("LocalizationHelper.GetLocalizedLanguages", "Using cached languages for profile: {0}", cachedProfileName);
                return cachedLanguages;
            }

            if (!modifiedOnly && cachedProfileName != ProfilesLibrary.ProfileName)
                InvalidateLanguageCache();

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
                DebugLogHelper.Log("LocalizationHelper.GetLocalizedLanguages", "Found {0} languages for profile: {1}, cache performed", result.Count, cachedProfileName);
            }

            if (modifiedOnly)
            {
                DebugLogHelper.Log("LocalizationHelper.GetLocalizedLanguages", "Found {0} modified languages", result.Count);
            }

            return result;
        }

        /// <summary>Clears the cached language list. Only needed if a different game profile loads into the same process.</summary>
        public static void InvalidateLanguageCache()
        {
            DebugLogHelper.Log("LocalizationHelper.InvalidateLanguageCache", "Cached languages cleared for profile: {0}", cachedProfileName);
            cachedLanguages = null;
            cachedProfileName = null;
        }

        /// <summary>Percent of a multi-part background operation.</summary>
        public static double ComputeProgress(double current, double total, double currentPart = 1, double totalParts = 1, double detail = 1, double totalDetails = 1)
        {
            if (total <= 0 || totalParts <= 0 || totalDetails <= 0)
                return 0.0d;

            // (((((p - 1) * t) + (c - 1)) * td + d) / (tp * t * td)) * 100%
            return (((((currentPart - 1) * total) + (current - 1)) * totalDetails) + detail) / (totalDetails * total * totalParts) * 100.0d;
        }

        /// <summary>Reports progress for a multi-part background operation via a FrostyTaskWindow's logger.</summary>
        public static void ReportProgress(ILogger logger, double current, double total, double currentPart = 1, double totalParts = 1, double detail = 1, double totalDetails = 1)
        {
            if (total <= 0)
                return;

            logger.Log("progress:" + ComputeProgress(current, total, currentPart, totalParts, detail, totalDetails));
        }
    }
}
