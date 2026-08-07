using Frosty.Core;
using FsLocalizationPlugin.Helpers;
using FsLocalizationPlugin.Resources;
using System;
using System.Globalization;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>
    /// Backs the Modify String Window: add / modify, revert, or remove a string.
    /// Typing ID: Fills the hash
    /// Typing Hash: Looks up the ID in the ID database
    /// Generate Hash: Picks an unused random hash for a new string.
    /// </summary>
    public sealed class ModifyStringViewModel : LanguageAwareViewModelBase
    {
        private static readonly string[] StateDependentProperties =
        {
            nameof(IsValid), nameof(IsRemoved), nameof(HasStringValue), nameof(StringValue),
            nameof(StatusMessage), nameof(IsModified),
            nameof(CanModify), nameof(CanRevert), nameof(CanRemove),
        };

        private static readonly Random Rng = new Random();

        private string hashText = string.Empty;
        private string idText = string.Empty;
        private string editText = string.Empty;
        private bool syncingFields;

        public ModifyStringViewModel(FsLocalizationStringDatabase database) : base(database)
        {
            ModifyCommand = new RelayCommand(_ => Modify(), _ => CanModify);
            RevertCommand = new RelayCommand(_ => Revert(), _ => CanRevert);
            RemoveCommand = new RelayCommand(_ => Remove(), _ => CanRemove);
            GenerateHashCommand = new RelayCommand(_ => GenerateHash());
            CopyAboveCommand = new RelayCommand(_ => EditText = StringValue);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        /// <summary>Raised to close the window, with the DialogResult.</summary>
        public event Action<bool?> CloseRequested;

        /// <summary>The 8-digit hex hash. Editing it looks up the ID in the ID database.</summary>
        public string HashText
        {
            get => hashText;
            set
            {
                if (!SetProperty(ref hashText, value ?? string.Empty))
                    return;

                if (!syncingFields)
                {
                    syncingFields = true;
                    if (LocalizationHelper.TryParseHexHash(hashText, out uint hash) && IdIndex.TryGet(hash, out string knownId))
                        IdText = knownId;
                    else
                        IdText = string.Empty;
                    syncingFields = false;
                }
                OnPropertiesChanged(StateDependentProperties);
            }
        }

        /// <summary>The string ID text. Editing it fills the hash field.</summary>
        public string IdText
        {
            get => idText;
            set
            {
                if (!SetProperty(ref idText, value ?? string.Empty))
                    return;

                if (!syncingFields)
                {
                    syncingFields = true;
                    HashText = idText.Length > 0 ? LocalizationHelper.HashStringId(idText).ToString("X8", CultureInfo.InvariantCulture) : string.Empty;
                    syncingFields = false;
                }
                OnPropertiesChanged(StateDependentProperties);
            }
        }

        public string EditText
        {
            get => editText;
            set => SetProperty(ref editText, value ?? string.Empty);
        }

        private uint? ParsedHash => LocalizationHelper.TryParseHexHash(HashText, out uint hash) ? hash : (uint?)null;

        /// <summary>Whether the typed ID belongs to the typed hash (it always does when the ID filled the hash).</summary>
        private bool IdMatchesHash => IdText.Length > 0 && ParsedHash is uint hash && LocalizationHelper.HashStringId(IdText) == hash;

        public bool IsValid => ParsedHash.HasValue;

        public bool IsRemoved => ParsedHash is uint hash && Database.IsStringRemoved(hash);

        /// <summary>Whether there's a value to preview (original or modified, not removed).</summary>
        public bool HasStringValue => ParsedHash is uint hash && Database.TryGetString(hash, out _);

        public string StringValue => ParsedHash is uint hash && Database.TryGetString(hash, out string value) ? value : string.Empty;

        /// <summary>Why the value preview is hidden, for the invalid/not-found/removed states.</summary>
        public string StatusMessage
        {
            get
            {
                if (!IsValid)
                    return "Invalid Hash";
                if (IsRemoved)
                    return "String is Removed";
                if (!HasStringValue)
                    return "No String Exists";
                return string.Empty;
            }
        }

        /// <summary>Whether the current value is a modification, not the unmodified original.</summary>
        public bool IsModified => ParsedHash is uint hash && Database.isStringEdited(hash);

        public bool CanModify => IsValid;
        public bool CanRevert => IsValid && (IsRemoved || IsModified);
        public bool CanRemove => IsValid && HasStringValue;

        public RelayCommand ModifyCommand { get; }
        public RelayCommand RevertCommand { get; }
        public RelayCommand RemoveCommand { get; }
        public RelayCommand GenerateHashCommand { get; }
        public RelayCommand CopyAboveCommand { get; }
        public RelayCommand CancelCommand { get; }

        protected override void OnLanguageChanged()
        {
            OnPropertiesChanged(StateDependentProperties);
        }

        /// <summary>Picks a random hash not in use.</summary>
        private void GenerateHash()
        {
            byte[] buffer = new byte[4];
            uint hash;
            do
            {
                Rng.NextBytes(buffer);
                hash = BitConverter.ToUInt32(buffer, 0);
            }
            while (hash == 0 || hash == 0xFFFFFFFF
                || Database.TryGetString(hash, out string _)
                || Database.IsStringRemoved(hash)
                || IdIndex.TryGet(hash, out string _));

            syncingFields = true;
            HashText = hash.ToString("X8", CultureInfo.InvariantCulture);
            IdText = string.Empty;
            syncingFields = false;
            OnPropertiesChanged(StateDependentProperties);
        }

        private void Modify()
        {
            if (!(ParsedHash is uint hash))
                return;

            if (HasStringValue)
                App.Logger.Log("Flame forged! String {0} modified, value: {1}", hash.ToString("X8", CultureInfo.InvariantCulture), EditText);
            else
                App.Logger.Log("Flame forged! String {0} added, value: {1}", hash.ToString("X8", CultureInfo.InvariantCulture), EditText);

            // The string overload records the ID text into the ID database.
            if (IdMatchesHash)
                Database.SetString(IdText, EditText);
            else
                Database.SetString(hash, EditText);
            OnPropertiesChanged(StateDependentProperties);
            CloseRequested?.Invoke(true);
        }

        private void Revert()
        {
            if (!(ParsedHash is uint hash))
                return;

            Database.RevertString(hash);
            App.Logger.Log("Flame extinguished! String {0} reverted", hash.ToString("X8", CultureInfo.InvariantCulture));
            OnPropertiesChanged(StateDependentProperties);
            CloseRequested?.Invoke(true);
        }

        private void Remove()
        {
            if (!(ParsedHash is uint hash))
                return;

            Database.RemoveString(hash);
            App.Logger.Log("Flame scorched! String {0} removed", hash.ToString("X8", CultureInfo.InvariantCulture));
            OnPropertiesChanged(StateDependentProperties);
            CloseRequested?.Invoke(true);
        }
    }
}
