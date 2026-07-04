using Frosty.Core;
using System;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>
    /// Backs the Modify String window - Flammenwerfer's flagship dialog for adding,
    /// modifying, reverting, or removing a single localized string by hash or string ID.
    /// </summary>
    public sealed class ModifyStringViewModel : LanguageAwareViewModelBase
    {
        private static readonly string[] StateDependentProperties =
        {
            nameof(IsValid), nameof(IsRemoved), nameof(HasStringValue), nameof(StringValue),
            nameof(StatusMessage), nameof(IsModified), nameof(ShowIdToHash), nameof(IdToHash),
            nameof(CanModify), nameof(CanRevert), nameof(CanRemove),
        };

        private readonly bool closeAfterAction;

        private string hashOrId = string.Empty;
        private string editText = string.Empty;

        /// <param name="database">The active string database.</param>
        /// <param name="closeAfterAction">
        /// Whether Modify/Revert/Remove should close the window once they've acted -
        /// matches the original FsLocalizationPlugin's single-action-then-close dialog.
        /// Pass <see langword="false"/> for a stay-open, batch-editing experience instead.
        /// </param>
        public ModifyStringViewModel(FsLocalizationStringDatabase database, bool closeAfterAction = true) : base(database)
        {
            this.closeAfterAction = closeAfterAction;

            ModifyCommand = new RelayCommand(_ => Modify(), _ => CanModify);
            RevertCommand = new RelayCommand(_ => Revert(), _ => CanRevert);
            RemoveCommand = new RelayCommand(_ => Remove(), _ => CanRemove);
            CopyAboveCommand = new RelayCommand(_ => EditText = StringValue);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        /// <summary>Raised when the window should close, with the DialogResult to use.</summary>
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

        /// <summary>Whether HashOrId could be parsed into a hash at all.</summary>
        public bool IsValid => ParsedHash.HasValue;

        /// <summary>Whether the string has been marked for removal.</summary>
        public bool IsRemoved => ParsedHash is uint hash && Database.IsStringRemoved(hash);

        /// <summary>Whether there's a current value to preview (baseline or modified, and not removed).</summary>
        public bool HasStringValue => ParsedHash is uint hash && Database.TryGetString(hash, out _);

        /// <summary>The current value of the string, when <see cref="HasStringValue"/> is true.</summary>
        public string StringValue => ParsedHash is uint hash && Database.TryGetString(hash, out string value) ? value : string.Empty;

        /// <summary>Explains why the value preview is hidden, for the invalid/not-found/removed states.</summary>
        public string StatusMessage
        {
            get
            {
                if (!IsValid)
                    return "Invalid hash or string ID";
                if (IsRemoved)
                    return "String is removed";
                if (!HasStringValue)
                    return "No string exists";
                return string.Empty;
            }
        }

        /// <summary>Whether the current value shown is a modification rather than the unmodified baseline value.</summary>
        public bool IsModified => ParsedHash is uint hash && Database.isStringEdited(hash);

        /// <summary>Whether HashOrId looks like a string ID (e.g. <c>ID_FLAME</c>) rather than a raw hash, so the UI can show the computed hash alongside it.</summary>
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

            Database.SetString(hash, EditText);
            // EditText is arbitrary (possibly user-typed, or containing the game's own
            // "{PlaceholderName}"-style tokens) - pass it as a format argument rather than
            // baking it into the template, or a literal brace in it would make ILogger's
            // internal string.Format throw.
            App.Logger.Log("String {0:X8} added/modified, value: {1}", hash, EditText);
            OnPropertiesChanged(StateDependentProperties);
            CloseIfConfigured();
        }

        private void Revert()
        {
            if (!(ParsedHash is uint hash))
                return;

            Database.RevertString(hash);
            App.Logger.Log($"String {hash:X8} reverted to its original value");
            OnPropertiesChanged(StateDependentProperties);
            CloseIfConfigured();
        }

        private void Remove()
        {
            if (!(ParsedHash is uint hash))
                return;

            Database.RemoveString(hash);
            App.Logger.Log($"String {hash:X8} removed");
            OnPropertiesChanged(StateDependentProperties);
            CloseIfConfigured();
        }

        private void CloseIfConfigured()
        {
            if (closeAfterAction)
                CloseRequested?.Invoke(true);
        }
    }
}
