using FlammenwerferPlugin.Editor.Extensions;
using FlammenwerferPlugin.Editor.Windows;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlammenwerferPlugin.Editor.Controls
{
    [TemplatePart(Name = PART_FilterType, Type = typeof(ComboBox))]
    [TemplatePart(Name = PART_Language, Type = typeof(ComboBox))]
    [TemplatePart(Name = PART_AddStringButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_BulkReplaceButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_ImportButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_ExportButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_Refresh, Type = typeof(Button))]
    [TemplatePart(Name = PART_FilterHash, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_FilterText, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_StringIdList, Type = typeof(ListBox))]
    [TemplatePart(Name = PART_LocalizedStringHash, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_UpdateCurrentStringButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_CopyCurrentStringButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_PasteCurrentStringButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_RevertCurrentStringButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_LocalizedString, Type = typeof(TextBox))]
    class LocalizedStringEditor : FrostyBaseEditor
    {
        public override ImageSource Icon => LocalizedStringViewerMenuExtension.imageSource;
        public ILocalizedStringDatabase db => LocalizedStringDatabase.Current;

        private const string PART_FilterType = "PART_FilterType";
        private const string PART_Language = "PART_Language";
        private const string PART_AddStringButton = "PART_AddStringButton";
        private const string PART_BulkReplaceButton = "PART_BulkReplaceButton";
        private const string PART_ImportButton = "PART_ImportButton";
        private const string PART_ExportButton = "PART_ExportButton";
        private const string PART_Refresh = "PART_Refresh";

        private const string PART_FilterHash = "PART_FilterHash";
        private const string PART_FilterText = "PART_FilterText";
        private const string PART_StringIdList = "PART_StringIdList";

        private const string PART_LocalizedStringHash = "PART_LocalizedStringHash";
        private const string PART_UpdateCurrentStringButton = "PART_UpdateCurrentStringButton";
        private const string PART_CopyCurrentStringButton = "PART_CopyCurrentStringButton";
        private const string PART_PasteCurrentStringButton = "PART_PasteCurrentStringButton";
        private const string PART_RevertCurrentStringButton = "PART_RevertCurrentStringButton";

        private const string PART_LocalizedString = "PART_LocalizedString";

        private ComboBox ComboBox_FilterType;
        private ComboBox ComboBox_Language;
        private Button Button_AddString;
        private Button Button_BulkReplace;
        private Button Button_Import;
        private Button Button_Export;
        private Button Button_Refresh;

        private TextBox TextBox_FilterHash;
        private TextBox TextBox_FilterText;
        private ListBox ListBox_StringIdList;

        private TextBox TextBox_LocalizedStringHash;
        private Button Button_UpdateCurrentString;
        private Button Button_CopyCurrentString;
        private Button Button_PasteCurrentString;
        private Button Button_RevertCurrentString;

        private TextBox TextBox_LocalizedString;

        private string CurrentFilterText;
        private string CurrentFilterHash;
        private string ListBoxSelectedString;

        private List<uint> stringIds = new List<uint>();
        private List<string> stringIDListUnfiltered = new List<string>();
        private int currentIndex = 0;
        private bool firstTimeLoad = true;
        private ILogger logger;

        static LocalizedStringEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LocalizedStringEditor), new FrameworkPropertyMetadata(typeof(LocalizedStringEditor)));
        }

        public LocalizedStringEditor(ILogger inLogger)
        {
            logger = inLogger;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            ComboBox_FilterType = GetTemplateChild(PART_FilterType) as ComboBox;
            ComboBox_Language = GetTemplateChild(PART_Language) as ComboBox;
            Button_AddString = GetTemplateChild(PART_AddStringButton) as Button;
            Button_BulkReplace = GetTemplateChild(PART_BulkReplaceButton) as Button;
            Button_Import = GetTemplateChild(PART_ImportButton) as Button;
            Button_Export = GetTemplateChild(PART_ExportButton) as Button;
            Button_Refresh = GetTemplateChild(PART_Refresh) as Button;

            ComboBox_FilterType.Items.Add("Show all strings");
            ComboBox_FilterType.Items.Add("Show modified strings");
            ComboBox_FilterType.Items.Add("Show unmodified strings");
            ComboBox_FilterType.SelectedIndex = 0;
            List<string> languages = GetLocalizedLanguages();
            ComboBox_Language.Items.Clear();
            languages.ForEach(x => ComboBox_Language.Items.Add(x));
            ComboBox_Language.SelectedIndex = ComboBox_Language.Items.IndexOf(Config.Get<string>("Language", "English", ConfigScope.Game));
            Button_AddString.Click += AddStringButton_Click;
            Button_BulkReplace.Click += BulkReplaceButton_Click;
            Button_Import.Click += ImportButton_Click;
            Button_Export.Click += ExportButton_Click;
            Button_Refresh.Click += Refresh_Click;

            TextBox_FilterText = GetTemplateChild(PART_FilterText) as TextBox;
            TextBox_FilterHash = GetTemplateChild(PART_FilterHash) as TextBox;
            ListBox_StringIdList = GetTemplateChild(PART_StringIdList) as ListBox;

            CurrentFilterText = "";
            TextBox_FilterText.LostFocus += Filter_LostFocus;
            TextBox_FilterText.KeyDown += Filter_KeyDown;
            CurrentFilterHash = "";
            TextBox_FilterHash.LostFocus += Filter_LostFocus;
            TextBox_FilterHash.KeyDown += Filter_KeyDown;
            stringIDListUnfiltered = new List<string>();

            TextBox_LocalizedStringHash = GetTemplateChild(PART_LocalizedStringHash) as TextBox;
            TextBox_LocalizedString = GetTemplateChild(PART_LocalizedString) as TextBox;
            Button_UpdateCurrentString = GetTemplateChild(PART_UpdateCurrentStringButton) as Button;
            Button_CopyCurrentString = GetTemplateChild(PART_CopyCurrentStringButton) as Button;
            Button_PasteCurrentString = GetTemplateChild(PART_PasteCurrentStringButton) as Button;
            Button_RevertCurrentString = GetTemplateChild(PART_RevertCurrentStringButton) as Button;

            //TextBox_LocalizedString.KeyDown += LocalizedString_KeyDown;
            TextBox_LocalizedString.TextChanged += LocalizedString_TextChanged;
            TextBox_LocalizedString.Drop += LocalizedString_Drop;
            Button_UpdateCurrentString.IsEnabled = false;
            Button_UpdateCurrentString.Click += UpdateCurrentStringButton_Click;
            Button_CopyCurrentString.IsEnabled = false;
            Button_CopyCurrentString.Click += CopyCurrentStringButton_Click;
            Button_PasteCurrentString.IsEnabled = false;
            Button_PasteCurrentString.Click += PasteCurrentStringButton_Click;
            Button_RevertCurrentString.IsEnabled = false;
            Button_RevertCurrentString.Click += RevertCurrentStringButton_Click;

            // Preventing exceptions
            ComboBox_FilterType.SelectionChanged += FilterType_SelectionChanged;
            ComboBox_Language.SelectionChanged += Language_SelectionChanged;
            ListBox_StringIdList.SelectionChanged += StringIdList_SelectionChanged;

            Loaded += LocalizedStringEditor_Loaded;
        }

        private void LocalizedStringEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (firstTimeLoad)
            {
                FrostyTaskWindow.Show("Loading Strings", "", (task) =>
                {
                    stringIds = db.EnumerateStrings().Distinct().ToList();
                    stringIds.Sort();
                });
                firstTimeLoad = false;
            }

            if (stringIds.Count == 0)
            {
                return;
            }

            FillStringIDs(stringIds);
            RemakeList();
        }

        private void LocalizedStringEditor_Closed(object sender, RoutedEventArgs e)
        {
            if (firstTimeLoad)
            {
                FrostyTaskWindow.Show("Loading Strings", "", (task) =>
                {
                    stringIds = db.EnumerateStrings().Distinct().ToList();
                    stringIds.Sort();
                });
                firstTimeLoad = false;
            }

            if (stringIds.Count == 0)
            {
                return;
            }

            FillStringIDs(stringIds);
            RemakeList();
        }

        private void FilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemakeList();
        }

        private List<string> GetLocalizedLanguages()
        {
            List<string> languages = new List<string>();
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

            if (languages.Count == 0)
                languages.Add("English");

            return languages;
        }

        private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Config.Add("Language", ComboBox_Language.SelectedItem.ToString(), ConfigScope.Game);
            Config.Save();
            db.Initialize();
            Refresh_Click(sender, e);
        }

        private void AddStringButton_Click(object sender, RoutedEventArgs e)
        {
            db.AddStringWindow();
            foreach (uint stringid in db.EnumerateStrings())
            {
                if (!stringIds.Contains(stringid))
                {
                    stringIds.Add(stringid);
                    stringIds.Sort();
                }
            }
            FillStringIDs(stringIds);
            RemakeList();
        }

        private void BulkReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            db.BulkReplaceWindow();
            FillStringIDs(stringIds);
            RemakeList();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            ImportStringWindow importWindow = new ImportStringWindow();
            if (importWindow.ShowDialog() == true)
                Refresh_Click(sender, e);

            //FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Import Localized Strings", "*.csv (CSV File)|*.csv", "LocalizedStrings");
            //if (ofd.ShowDialog())
            //{
            //    int modified = 0;
            //    int added = 0;
            //    FrostyTaskWindow.Show("Importing Localized Strings", "", (task) =>
            //    {
            //        using (StreamReader reader = new StreamReader(ofd.FileName))
            //        {
            //            while (!reader.EndOfStream)
            //            {
            //                string line = reader.ReadLine();
            //                uint hash = uint.Parse(line.Substring(0, 8), System.Globalization.NumberStyles.HexNumber);
            //                string s = line.Substring(10, line.Length - 11);
            //                if (stringIds.Contains(hash) && s != db.GetString(hash))
            //                {
            //                    db.SetString(hash, s);
            //                    modified++;
            //                }
            //                else
            //                {
            //                    db.SetString(hash, s);
            //                    added++;
            //                }
            //            }
            //        }
            //    });
            //    Refresh_Click(sender, e);
            //    logger.Log(string.Format("{0} strings modified and {1} strings added.", modified, added));
            //}
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            //FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Localized Strings", "*.csv (CSV File)|*.csv", "LocalizedStrings");
            //if (sfd.ShowDialog())
            //{
            //    FrostyTaskWindow.Show("Exporting Localized Strings", "", (task) =>
            //    {
            //        using (StreamWriter writer = new StreamWriter(sfd.FileName))
            //        {
            //            int index = 0;
            //            foreach (uint stringId in stringIds)
            //            {
            //                string str = db.GetString(stringId);

            //                str = str.Replace("\r", "");
            //                str = str.Replace("\n", " ");
            //                str = str.Replace("\"", "\"\"");

            //                writer.WriteLine(stringId.ToString("X8") + ",\"" + str + "\"");
            //                task.Update(progress: ((index++) / (double)stringIds.Count) * 100.0);
            //            }
            //        }
            //    });

            //    App.Logger.Log("Localized strings saved to {0}", sfd.FileName);
            //}
        }

        //public static bool HasProperty(object obj, string propertyName)
        //{
        //    return obj.GetType().GetProperty(propertyName) != null;
        //}

        //private void PART_ExportLogButton_Click(object sender, RoutedEventArgs e)
        //{
        //    FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Localized Strings Usage List", "*.txt (Text File)|*.txt", "LocalizedStringsUsage");
        //    if (sfd.ShowDialog())
        //    {
        //        FrostyTaskWindow.Show("Exporting Localized Strings Usage", "", (task) =>
        //        {
        //            uint totalCount = (uint)App.AssetManager.EnumerateEbx().ToList().Count;
        //            uint idx = 0;
        //            Dictionary<string, string> StringInfo = new Dictionary<string, string>();
        //            foreach (uint stringId in stringIds)
        //            {
        //                StringInfo.Add(stringId.ToString("X").ToLower(), stringId.ToString("X8") + ", \"" + db.GetString(stringId).Replace("\r", "").Replace("\n", " ") + "\"");
        //            }
        //            foreach (EbxAssetEntry refEntry in App.AssetManager.EnumerateEbx())
        //            {
        //                task.Update("Checking: " + refEntry.Name, (idx++ / (double)totalCount) * 100.0d);
        //                EbxAsset refAsset = App.AssetManager.GetEbx(refEntry);
        //                List<string> AlreadyDone = new List<string>();
        //                foreach (dynamic obj in refAsset.Objects)
        //                {
        //                    if (HasProperty(obj, "StringHash"))
        //                    {
        //                        string TempString = obj.StringHash.ToString("X").ToLower();
        //                        if (StringInfo.ContainsKey(TempString) & !AlreadyDone.Contains(TempString))
        //                        {
        //                            AlreadyDone.Add(TempString);
        //                            StringInfo[TempString] = StringInfo[TempString] + "\n           -" + refEntry.Name;
        //                        }
        //                    }
        //                    foreach (PropertyInfo pi in obj.GetType().GetProperties())
        //                    {
        //                        if (pi.PropertyType == typeof(CString))
        //                        {
        //                            string TempString = HashStringId(pi.GetValue(obj)).ToString("X").ToLower();
        //                            if (StringInfo.ContainsKey(TempString) & !AlreadyDone.Contains(TempString))
        //                            {
        //                                AlreadyDone.Add(TempString);
        //                                StringInfo[TempString] = StringInfo[TempString] + "\n          -" + refEntry.Name;
        //                            }
        //                        }
        //                        else if (pi.PropertyType == typeof(List<CString>))
        //                        {
        //                            foreach (CString cst in pi.GetValue(obj))
        //                            {
        //                                string TempString = HashStringId(cst).ToString("X").ToLower();
        //                                if (StringInfo.ContainsKey(TempString) & !AlreadyDone.Contains(TempString))
        //                                {
        //                                    AlreadyDone.Add(TempString);
        //                                    StringInfo[TempString] = StringInfo[TempString] + "\n          -" + refEntry.Name;
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }

        //            using (StreamWriter writer = new StreamWriter(sfd.FileName))
        //            {
        //                foreach (string StringData in StringInfo.Values)
        //                {
        //                    writer.WriteLine(StringData);
        //                }
        //            }
        //        });

        //        App.Logger.Log("Localized strings usage saved to {0}", sfd.FileName);
        //    }
        //}

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            FrostyTaskWindow.Show("Loading Strings", "", (task) =>
            {
                stringIds = db.EnumerateStrings().Distinct().ToList();
                stringIds.Sort();
            });

            if (stringIds.Count == 0)
            {
                return;
            }

            FillStringIDs(stringIds);
            RemakeList();
        }

        private void Filter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CheckFilterStrings();
            }
        }

        private void Filter_LostFocus(object sender, RoutedEventArgs e)
        {
            CheckFilterStrings();
        }

        private void StringIdList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Button_UpdateCurrentString.IsEnabled = false;
            if (ListBox_StringIdList.SelectedItem != null)
            {
                Button_CopyCurrentString.IsEnabled = true;
                Button_PasteCurrentString.IsEnabled = true;
                ListBoxSelectedString = ((string)ListBox_StringIdList.SelectedItem);
                uint stringID = stringIds[stringIDListUnfiltered.IndexOf(ListBoxSelectedString)];
                if (db.isStringEdited(stringID))
                {
                    Button_RevertCurrentString.IsEnabled = true;
                }
                else
                {
                    Button_RevertCurrentString.IsEnabled = false;
                }
                PopulateLocalizedString(stringID.ToString("X8"));
            }
            else
            {
                TextBox_LocalizedString.Text = "";
                TextBox_LocalizedStringHash.Text = "";
                Button_CopyCurrentString.IsEnabled = false;
                Button_PasteCurrentString.IsEnabled = false;
                Button_RevertCurrentString.IsEnabled = false;
            }
        }

        //private void LocalizedString_KeyDown(object sender, KeyEventArgs e)
        //{
        //    if (e.Key == Key.Enter)
        //    {
        //        int pos = TextBox_LocalizedString.CaretIndex;
        //        tbLocalizedString.Text = tbLocalizedString.Text.Substring(0, pos) + "\n" + tbLocalizedString.Text.Substring(pos, tbLocalizedString.Text.Length - pos);
        //        try
        //        {
        //            tbLocalizedString.CaretIndex = pos + 1;
        //        }
        //        catch
        //        {
        //            tbLocalizedString.CaretIndex = pos;
        //        }
        //    }
        //}

        private void LocalizedString_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ListBox_StringIdList.SelectedItem != null)
            {
                if (TextBox_LocalizedString.Text != db.GetString(stringIds[stringIDListUnfiltered.IndexOf(ListBoxSelectedString)]))
                {
                    Button_UpdateCurrentString.IsEnabled = true;
                }
                else
                {
                    Button_UpdateCurrentString.IsEnabled = false;
                }
            }
        }

        private void LocalizedString_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note: May have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                StringBuilder sb = new StringBuilder();
                foreach (string file in files)
                {
                    sb.Append(File.ReadAllText(file));
                }
                TextBox_LocalizedString.Text = sb.ToString();
            }
        }

        private void UpdateCurrentStringButton_Click(object sender, RoutedEventArgs e)
        {
            Button_UpdateCurrentString.IsEnabled = false;

            if (TextBox_LocalizedString.Text != null)
            {
                int Unfilteredidx = stringIDListUnfiltered.IndexOf((string)ListBox_StringIdList.SelectedItem);
                int selected = ListBox_StringIdList.SelectedIndex;
                uint stringId = stringIds[Unfilteredidx];
                db.SetString(stringId, TextBox_LocalizedString.Text);
                stringIDListUnfiltered[Unfilteredidx] = stringId.ToString("X8") + " - " + db.GetString(stringId);
                ListBox_StringIdList.Items[selected] = stringId.ToString("X8") + " - " + db.GetString(stringId);
                if (ComboBox_FilterType.SelectedIndex == 2)
                {
                    ListBox_StringIdList.Items.RemoveAt(selected);
                    ListBox_StringIdList.SelectedItem = -1;
                }
                else if (!TextBox_LocalizedString.Text.Contains(CurrentFilterText))
                {
                    ListBox_StringIdList.SelectedItem = -1;
                    FilterStrings();
                }

            }

        }

        private void CopyCurrentStringButton_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_LocalizedString.Text != null)
            {
                Clipboard.SetText(TextBox_LocalizedString.Text);
            }
        }

        private void PasteCurrentStringButton_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                TextBox_LocalizedString.Text = Clipboard.GetText();
                LocalizedString_TextChanged(sender, new TextChangedEventArgs(e.RoutedEvent, new UndoAction(), null));
            }
        }

        private void RevertCurrentStringButton_Click(object sender, RoutedEventArgs e)
        {
            int Unfilteredidx = stringIDListUnfiltered.IndexOf((string)ListBox_StringIdList.SelectedItem);
            int selected = ListBox_StringIdList.SelectedIndex;
            uint stringId = stringIds[Unfilteredidx];
            db.RevertString(stringId);
            stringIDListUnfiltered[Unfilteredidx] = stringId.ToString("X8") + " - " + db.GetString(stringId);
            ListBox_StringIdList.Items[selected] = stringId.ToString("X8") + " - " + db.GetString(stringId);
            if (ComboBox_FilterType.SelectedIndex == 1)
            {
                ListBox_StringIdList.Items.RemoveAt(selected);
                ListBox_StringIdList.SelectedItem = -1;
            }
            Button_RevertCurrentString.IsEnabled = false;
        }

        private void RemakeList()
        {
            Button_CopyCurrentString.IsEnabled = false;
            Button_PasteCurrentString.IsEnabled = false;
            Button_UpdateCurrentString.IsEnabled = false;
            CurrentFilterHash = "";
            CurrentFilterText = "";
            stringIds.Clear();
            if (ComboBox_FilterType.SelectedIndex == 0)
            {
                stringIds = db.EnumerateStrings().Distinct().ToList();
            }
            else if (ComboBox_FilterType.SelectedIndex == 1)
            {
                stringIds = db.EnumerateModifiedStrings().Distinct().ToList();
            }
            else if (ComboBox_FilterType.SelectedIndex == 2)
            {
                stringIds = db.EnumerateStrings().Distinct().Except(db.EnumerateModifiedStrings().Distinct().ToList()).ToList();
            }
            stringIds.Sort();
            FillStringIDs(stringIds);

            CheckFilterStrings();

            if (ListBoxSelectedString != null)
            {
                if (ListBox_StringIdList.Items.Contains(ListBoxSelectedString))
                {
                    ListBox_StringIdList.SelectedIndex = ListBox_StringIdList.Items.IndexOf(ListBoxSelectedString);
                }
            }
        }

        private void FillStringIDs(List<uint> stringIDs)
        {
            ListBox_StringIdList.Items.Clear();
            stringIDListUnfiltered.Clear();
            foreach (uint stringId in stringIds)
            {
                ListBox_StringIdList.Items.Add(stringId.ToString("X8") + " - " + db.GetString(stringId));
                stringIDListUnfiltered.Add(stringId.ToString("X8") + " - " + db.GetString(stringId));
            }
        }


        private void PopulateLocalizedString(string stringText)
        {
            stringText = stringText.ToLower();

            if (stringText.StartsWith("id_"))
            {
                TextBox_LocalizedString.Text = db.GetString(stringText);
                TextBox_LocalizedStringHash.Text = stringText;
                return;
            }

            if (!uint.TryParse(stringText, System.Globalization.NumberStyles.HexNumber, null, out uint value))
            {
                TextBox_LocalizedString.Text = "";
                TextBox_LocalizedStringHash.Text = "";
                return;
            }
            TextBox_LocalizedStringHash.Text = value.ToString("X8");
            TextBox_LocalizedString.Text = db.GetString(value);
        }

        private uint HashStringId(string stringId)
        {
            uint result = 0xFFFFFFFF;
            for (int i = 0; i < stringId.Length; i++)
                result = stringId[i] + 33 * result;
            return result;
        }


        private void CheckFilterStrings()
        {
            if (CurrentFilterHash != TextBox_FilterHash.Text || CurrentFilterText != TextBox_FilterText.Text)
            {
                FilterStrings();
            }
        }

        private void FilterStrings()
        {
            ListBox_StringIdList.Items.Filter = new Predicate<object>((object a) => ((((string)a).Substring(0, 8).ToLower().Contains(TextBox_FilterHash.Text.ToLower())) & (((string)a).Substring(10).ToLower().Contains(TextBox_FilterText.Text.ToLower()))));
            CurrentFilterHash = TextBox_FilterHash.Text;
            CurrentFilterText = TextBox_FilterText.Text;
        }

    }
}
