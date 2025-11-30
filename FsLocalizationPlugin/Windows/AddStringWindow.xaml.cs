using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Ebx;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FsLocalizationPlugin.Windows
{
    public partial class AddStringWindow : FrostyDockableWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public FsLocalizationStringDatabase db = LocalizedStringDatabase.Current as FsLocalizationStringDatabase;

        private string _hashOrId;
        public string HashOrId
        {
            get { return _hashOrId; }
            set
            {
                if (_hashOrId != value)
                {
                    _hashOrId = value;
                    OnPropertyChanged(nameof(HashOrId));
                    OnPropertyChanged(nameof(StringValue));
                    OnPropertyChanged(nameof(ShowIdToHash));
                    OnPropertyChanged(nameof(IdToHash));
                }
            }
        }

        public string StringValue => HashOrIdToId(HashOrId) == null ? "No Hash or ID" : db.GetString(HashOrIdToId(HashOrId).Value);

        public bool ShowIdToHash => HashOrId.StartsWith("ID");

        public string IdToHash => LocalizationHelper.HashStringId(HashOrId).ToString("X8");
        public bool CanAdd => HashOrIdToId(HashOrId) != null;

        public AddStringWindow(Window owner)
        {
            Owner = owner;

            InitializeComponent();

            Left = Owner.Left + (Owner.Width / 2.0) - (ActualWidth / 2.0);
            Top = Owner.Top + (Owner.Height / 2.0) - (ActualHeight / 2.0);

            this.DataContext = this;

            Dispatcher.UnhandledException += UnhandledException;

            LanguageComboBox.Items.Clear();
            GetLocalizedLanguages().ForEach(x => LanguageComboBox.Items.Add(x));
            LanguageComboBox.SelectedIndex = LanguageComboBox.Items.IndexOf(Config.Get<string>("Language", "English", ConfigScope.Game));
            // Manually set to avoid exception
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
        }

        public static List<string> GetLocalizedLanguages()
        {
            HashSet<string> languages = new HashSet<string>();
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
                    dynamic localizedText = App.AssetManager.GetEbx(textEntry).RootObject;

                    string lang = localizedText.Language.ToString();
                    lang = lang.Replace("LanguageFormat_", "");

                    languages.Add(lang);
                }
            }

            if (!languages.Any())
                languages.Add("English");

            return languages.ToList();
        }

        private void UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            FrostyExceptionBox.Show(e.Exception, "Add String - Flammenwerfer");
            DialogResult = false;
            Close();
        }

        private uint? HashOrIdToId(string hashOrId)
        {
            try
            {
                if (hashOrId.StartsWith("ID"))
                {
                    return LocalizationHelper.HashStringId(hashOrId);
                }
                else if (hashOrId.Length == 8)
                {
                    return Convert.ToUInt32(hashOrId, 16);
                }
                else if (hashOrId.Length == 10 && (hashOrId.StartsWith("0x") || hashOrId.StartsWith("0X")))
                {
                    return Convert.ToUInt32(hashOrId.Remove(0, 2), 16);
                }
            }
            catch { }
            return null;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            uint? id = HashOrIdToId(HashOrId);
            if (id == null)
            {
                return;
            }
            else
            {
                db.SetString(id.Value, EditTextBox.Text);
                App.Logger.Log($"String {id.Value.ToString("X8")} added, value: {EditTextBox.Text}");
                OnPropertyChanged(nameof(StringValue));
            }
        }
        private void CopyAboveButton_Click(object sender, RoutedEventArgs e)
        {
            EditTextBox.Text = StringValue;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Config.Add("Language", LanguageComboBox.SelectedItem.ToString(), ConfigScope.Game);
            Config.Save();
            db.Initialize();
            OnPropertyChanged(nameof(StringValue));
        }
    }
}
