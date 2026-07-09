using Frosty.Core;
using FsLocalizationPlugin.Helpers;
using System;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>Backs the Modify String window: add, modify, revert, or remove one string by hash or string ID.</summary>
    public sealed class ModifyStringViewModel : LanguageAwareViewModelBase
    {
        private static readonly string[] StateDependentProperties =
        {
            nameof(IsValid), nameof(IsRemoved), nameof(HasStringValue), nameof(StringValue),
            nameof(StatusMessage), nameof(IsModified), nameof(ShowIdToHash), nameof(IdToHash),
            nameof(CanModify), nameof(CanRevert), nameof(CanRemove),
        };

        private string hashOrId = string.Empty;
        private string editText = string.Empty;

        public ModifyStringViewModel(FsLocalizationStringDatabase database) : base(database)
        {
            ModifyCommand = new RelayCommand(_ => Modify(), _ => CanModify);
            RevertCommand = new RelayCommand(_ => Revert(), _ => CanRevert);
            RemoveCommand = new RelayCommand(_ => Remove(), _ => CanRemove);
            CopyAboveCommand = new RelayCommand(_ => EditText = StringValue);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        /// <summary>Raised to close the window, with the DialogResult.</summary>
        public event Action<bool?> CloseRequested;

        public string HashOrId
        {
            get => hashOrId;
            set
            {
                if (SetProperty(ref hashOrId, value ?? string.Empty))
                    OnPropertiesChanged(StateDependentProperties);
            }
        }

        public string EditText
        {
            get => editText;
            set => SetProperty(ref editText, value ?? string.Empty);
        }

        private uint? ParsedHash => LocalizationHelper.TryParseHashOrId(HashOrId, out uint hash) ? hash : (uint?)null;

        public bool IsValid => ParsedHash.HasValue;

        public bool IsRemoved => ParsedHash is uint hash && Database.IsStringRemoved(hash);

        /// <summary>Whether there's a value to preview (baseline or modified, not removed).</summary>
        public bool HasStringValue => ParsedHash is uint hash && Database.TryGetString(hash, out _);

        public string StringValue => ParsedHash is uint hash && Database.TryGetString(hash, out string value) ? value : string.Empty;

        /// <summary>Why the value preview is hidden, for the invalid/not-found/removed states.</summary>
        public string StatusMessage
        {
            get
            {
                if (!IsValid)
                    return "Invalid Hash or String ID";
                if (IsRemoved)
                    return "String is Removed";
                if (!HasStringValue)
                    return "No String Exists";
                return string.Empty;
            }
        }

        /// <summary>Whether the current value is a modification, not the unmodified baseline.</summary>
        public bool IsModified => ParsedHash is uint hash && Database.isStringEdited(hash);

        /// <summary>Whether HashOrId looks like a string ID (e.g. <c>ID_FLAME</c>) rather than a raw hash.</summary>
        public bool ShowIdToHash => HashOrId.StartsWith("ID");

        public string IdToHash => LocalizationHelper.HashStringId(HashOrId).ToString("X8");

        public bool CanModify => IsValid;
        public bool CanRevert => IsValid && (IsRemoved || IsModified);
        public bool CanRemove => IsValid && HasStringValue;

        public RelayCommand ModifyCommand { get; }
        public RelayCommand RevertCommand { get; }
        public RelayCommand RemoveCommand { get; }
        public RelayCommand CopyAboveCommand { get; }
        public RelayCommand CancelCommand { get; }

        protected override void OnLanguageChanged()
        {
            OnPropertiesChanged(StateDependentProperties);
        }

        private void Modify()
        {
            if (!(ParsedHash is uint hash))
                return;

            // EditText may contain literal braces (e.g. "{PlayerName}") - pass as an arg, not the format template.
            if (HasStringValue)
                App.Logger.Log("Flame forged! String {0} modified, value: {1}", hash.ToString("X8"), EditText);
            else
                App.Logger.Log("Flame forged! String {0} added, value: {1}", hash.ToString("X8"), EditText);

            Database.SetString(hash, EditText);
            OnPropertiesChanged(StateDependentProperties);
            CloseRequested?.Invoke(true);
        }

        private void Revert()
        {
            if (!(ParsedHash is uint hash))
                return;

            Database.RevertString(hash);
            App.Logger.Log("Flame extinguished! String {0} reverted", hash.ToString("X8"));
            OnPropertiesChanged(StateDependentProperties);
            CloseRequested?.Invoke(true);
        }

        private void Remove()
        {
            if (!(ParsedHash is uint hash))
                return;

            Database.RemoveString(hash);
            App.Logger.Log("Flame scorched! String {0} removed", hash.ToString("X8"));
            OnPropertiesChanged(StateDependentProperties);
            CloseRequested?.Invoke(true);
        }

    }
}
