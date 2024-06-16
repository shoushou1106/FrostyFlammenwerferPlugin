using ExcelDataReader;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FlammenwerferPlugin.Editor.Windows
{
    public partial class ImportStringWindow : FrostyDockableWindow
    {
        private string FilePath { get; set; }

        public ImportStringWindow(Window owner)
        {
            Owner = owner;

            InitializeComponent();

            Left = (Owner.Left + (Owner.Width / 2.0)) - (ActualWidth / 2.0);
            Top = (Owner.Top + (Owner.Height / 2.0)) - (ActualHeight / 2.0);

            CsvGrid.Visibility = Visibility.Collapsed;
            JsonGrid.Visibility = Visibility.Collapsed;
            ExcelGrid.Visibility = Visibility.Collapsed;
            FileTypeComboBox.SelectionChanged += PropertyChanged_Selection;
            CsvFallbackEncodingComboBox.SelectionChanged += PropertyChanged_Selection;
            //CsvRemoveDuplicatedBomCheckBox.Click += PropertyChanged_Routed;

            List<Encoding> encodings = Encoding.GetEncodings().Select(e => e.GetEncoding()).ToList();
            CsvFallbackEncodingComboBox.ItemsSource = encodings;
            CsvFallbackEncodingComboBox.SelectedItem = new ExcelReaderConfiguration().FallbackEncoding;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note: May have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Count() > 1)
                    App.Logger.LogWarning("Multiple files dropped. Only the first file will be opened."); // TODO: Allow import multiple files

                if (files.Count() >= 1)
                {
                    FilePath = files[0];
                    LoadFile();
                }
            }
        }

        private void FileBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            FrostyOpenFileDialog openFileDialog = new FrostyOpenFileDialog("Import Localized Strings",
                "All supported file (*.csv;*.json;*.xlsx;*.xlsb;*.xls)|*.csv;*.json;*.xlsx;*.xlsb;*.xls|CSV file (*.csv)|*.csv|Json file (*.json)|*.json|Excel file (*.xlsx;*.xlsb;*.xls)|*.xlsx;*.xlsb;*.xls|All file (*.*)|*.*",
                "LocalizedStrings")
            {
                Multiselect = false // TODO: Allow import multiple files
            };
            if (openFileDialog.ShowDialog())
            {
                FilePath = openFileDialog.FileName;
                LoadFile();
            }
        }

        private void PropertyChanged_Routed(object sender, RoutedEventArgs e)
        {
            LoadFile();
        }

        private void PropertyChanged_Selection(object sender, SelectionChangedEventArgs e)
        {
            LoadFile();
        }

        private void LoadFile()
        {
            CsvGrid.Visibility = Visibility.Collapsed;
            JsonGrid.Visibility = Visibility.Collapsed;
            ExcelGrid.Visibility = Visibility.Collapsed;
            ConfirmButton.IsEnabled = false;

            if (!File.Exists(FilePath))
            {
                FileNameLabel.Content = "or drag a file here";
                FileNameLabel.ToolTip = null;
                FilePath = null;
                return;
            }

            FileInfo fileInfo = new FileInfo(FilePath);
            FileNameLabel.Content = fileInfo.Name;
            FileNameLabel.ToolTip = FilePath;

            switch ((FileTypeComboBox.SelectedItem as ComboBoxItem).Name)
            {
                case "Auto":
                    switch (fileInfo.Extension.ToLower())
                    {
                        case ".csv":
                            ShowPreviewCsv();
                            break;

                        case ".json":
                            //ReadCsv(path);
                            break;

                        case ".xlsx":
                        case ".xlsb":
                        case ".xls":
                            //ReadCsv(path);
                            break;
                        default:
                            CsvGrid.Visibility = Visibility.Collapsed;
                            JsonGrid.Visibility = Visibility.Collapsed;
                            ExcelGrid.Visibility = Visibility.Collapsed;
                            FrostyMessageBox.Show($"Unsupported file type: {fileInfo.Extension.ToLower()}{Environment.NewLine}Manually choose the File Type to force reading", "Flammenwerfer Editor (Import String Window)");
                            break;
                    }
                    break;

                case "Csv":
                    ShowPreviewCsv();
                    break;

                case "Json":
                    //ReadCsv(path);
                    break;

                case "Excel":
                    //ReadCsv(path);
                    break;

                default:
                    throw new FileFormatException("The selected File Type cannot be recognized");
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(FilePath))
            {
                CsvGrid.Visibility = Visibility.Collapsed;
                JsonGrid.Visibility = Visibility.Collapsed;
                ExcelGrid.Visibility = Visibility.Collapsed;
                FileNameLabel.Content = "or drag a file here";
                FileNameLabel.ToolTip = null;
                FilePath = null;
                FrostyMessageBox.Show("File not found", "Flammenwerfer Editor (Import String Window)");
                return;
            }

            FileInfo fileInfo = new FileInfo(FilePath);
            FileNameLabel.Content = fileInfo.Name;
            FileNameLabel.ToolTip = FilePath;

            bool isSuccess = false;

            switch ((FileTypeComboBox.SelectedItem as ComboBoxItem).Name)
            {
                case "Auto":
                    switch (fileInfo.Extension.ToLower())
                    {
                        case ".csv":
                            isSuccess = ImportCsv();
                            break;

                        case ".json":
                            //ReadCsv(path);
                            break;

                        case ".xlsx":
                        case ".xlsb":
                        case ".xls":
                            //ReadCsv(path);
                            break;
                        default:
                            CsvGrid.Visibility = Visibility.Collapsed;
                            JsonGrid.Visibility = Visibility.Collapsed;
                            ExcelGrid.Visibility = Visibility.Collapsed;
                            FrostyMessageBox.Show($"Unsupported file type: {fileInfo.Extension.ToLower()}{Environment.NewLine}Manually choose the File Type to force reading", "Flammenwerfer Editor (Import String Window)");
                            return;
                    }
                    break;

                case "Csv":
                    ShowPreviewCsv();
                    break;

                case "Json":
                    //ReadCsv(path);
                    break;

                case "Excel":
                    //ReadCsv(path);
                    break;

                default:
                    throw new FileFormatException("The selected File Type cannot be recognized");
            }

            if (isSuccess)
            {
                DialogResult = true;
                Close();
            }
        }

        #region - CSV -

        private void ShowPreviewCsv()
        {
            CsvGrid.Visibility = Visibility.Visible;
            ConfirmButton.IsEnabled = true;
            CsvPreviewDataGrid.ItemsSource = null;
            CsvPreviewDataGrid.Columns.Clear();
            try
            {
                DataTable dataTable = ReadPreviewCsv();
                CsvPreviewDataGrid.ItemsSource = dataTable.DefaultView;

                foreach (DataColumn column in dataTable.Columns)
                {
                    ComboBox header = new ComboBox();
                    header.Items.Add(new ComboBoxItem()
                    {
                        Name = "None",
                        Content = "None",
                    });
                    header.Items.Add(new ComboBoxItem()
                    {
                        Name = "Key",
                        Content = "Key",
                    });
                    header.SelectedIndex = 0;
                    Controls.LocalizedStringEditor.GetLocalizedLanguages()
                        .ForEach(x => header.Items.Add(new ComboBoxItem()
                        {
                            Name = x,
                            Content = "String (" + x + ")",
                        }));
                    header.BorderThickness = new Thickness(1);

                    DataTemplate cell = new DataTemplate();
                    var cellTextBlock = new FrameworkElementFactory(typeof(TextBlock));
                    cellTextBlock.SetBinding(TextBlock.TextProperty, new Binding(column.ColumnName));
                    cellTextBlock.SetValue(TextBlock.ForegroundProperty, FindResource("FontColor"));
                    cellTextBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                    cellTextBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
                    cellTextBlock.SetValue(TextBlock.PaddingProperty, new Thickness(2));
                    cell.VisualTree = cellTextBlock;

                    DataGridTemplateColumn templateColumn = new DataGridTemplateColumn
                    {
                        Header = header,
                        CellTemplate = cell,
                        CellStyle = (Style)FindResource("DataGridCellStyle"),
                        HeaderStyle = (Style)FindResource("DataGridHeaderStyle")
                    };
                    CsvPreviewDataGrid.Columns.Add(templateColumn);
                }
            }
            catch (Exception ex)
            {
                // Allow user to force import
                //ConfirmButton.IsEnabled = false; 
                CsvPreviewDataGrid.ItemsSource = null;
                CsvPreviewDataGrid.Columns.Clear();

                TextBox textBox = new TextBox();
                textBox.IsReadOnly = true;
                textBox.Width = ActualWidth - 30;
                textBox.BorderThickness = new Thickness(1);
                textBox.TextWrapping = TextWrapping.WrapWithOverflow;
                textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Could not read the file as CSV, you can still force import");
                sb.AppendLine();
                sb.AppendLine("Exception:");
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

                textBox.Text = sb.ToString();

                DataGridTemplateColumn templateColumn = new DataGridTemplateColumn
                {
                    Header = textBox,
                    CellTemplate = new DataTemplate(),
                    CellStyle = (Style)FindResource("DataGridCellStyle"),
                    HeaderStyle = (Style)FindResource("DataGridHeaderStyle"),
                    Width = ActualWidth - 30
                };
                CsvPreviewDataGrid.Columns.Add(templateColumn);
                CsvPreviewDataGrid.Columns.Add(new DataGridTemplateColumn());
            }
        }

        private DataTable ReadPreviewCsv()
        {
            DataTable dt = new DataTable();

            using (FileStream stream = File.Open(FilePath, FileMode.Open, FileAccess.Read))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration()
            {
                FallbackEncoding = CsvFallbackEncodingComboBox.SelectedItem as Encoding
            }))
            {
                for (int i = 0; i < 5; i++)
                {
                    if (!reader.Read())
                        break;
                    DataRow dr = dt.NewRow();
                    for (int j = 0; j < reader.FieldCount; j++)
                    {
                        if (!dt.Columns.Contains(j.ToString()))
                            dt.Columns.Add(j.ToString(), typeof(string));
                        dr[j] = reader.GetString(j);
                    }
                    dt.Rows.Add(dr);
                }
            }

            return dt;
        }

        private bool ImportCsv()
        {
            ILocalizedStringDatabase db = LocalizedStringDatabase.Current;
            DataTable dt = new DataTable();
            Dictionary<int, Func<string, uint, uint>> funcs = new Dictionary<int, Func<string, uint, uint>>();
            Dictionary<string, List<Tuple<uint, string>>> stringsToAdd = new Dictionary<string, List<Tuple<uint, string>>>();

            foreach (string lang in Controls.LocalizedStringEditor.GetLocalizedLanguages())
            {
                stringsToAdd.Add(lang, new List<Tuple<uint, string>>());
            }

            // Remove duplicated BOM
            long positionStart = 0;
            if (CsvRemoveDuplicatedBomCheckBox.IsChecked == true)
            {
                positionStart = CsvRemoveDuplicatedBOM(0);
                GC.Collect();
            }

            App.Logger.Log(positionStart.ToString());

            using (MemoryStream stream = new MemoryStream(File.ReadAllBytes(FilePath).Skip((int)positionStart).ToArray()))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration()
            {
                FallbackEncoding = CsvFallbackEncodingComboBox.SelectedItem as Encoding
            }))
            {
                while (reader.Read())
                {
                    DataRow dr = dt.NewRow();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (!dt.Columns.Contains(i.ToString()))
                            dt.Columns.Add(i.ToString(), typeof(string));
                        dr[i] = reader.GetString(i);
                    }
                    dt.Rows.Add(dr);
                }
            }

            GC.Collect();

            int keyIndex = -1;

            for (int i = 0; i < CsvPreviewDataGrid.Columns.Count; i++)
            {
                string name = ((CsvPreviewDataGrid.Columns[i].Header as ComboBox).SelectedItem as ComboBoxItem).Name;
                switch (name)
                {
                    case null:
                    case "":
                    case "None":
                        Func<string, uint, uint> noneAction = (_, inKey) => {
                            return inKey;
                        };
                        funcs.Add(i, noneAction);
                        break;

                    case "Key":
                        if (keyIndex == -1)
                            keyIndex = i;
                        else
                        {
                            FrostyMessageBox.Show("Please only select one key column", "Flammenwerfer Editor (Import String Window)");
                            return false;
                        }

                        Func<string, uint, uint> keyAction = (inValue, _) => {
                            return uint.Parse(inValue, System.Globalization.NumberStyles.HexNumber);
                        };
                        funcs.Add(i, keyAction);
                        break;

                    default:
                        Func<string, uint, uint> stringAction = (inValue, inKey) => {
                            stringsToAdd[name].Add(new Tuple<uint, string>(inKey, inValue));
                            return inKey;
                        };
                        funcs.Add(i, stringAction);
                        break;
                }
            }

            if (keyIndex == -1)
            {
                FrostyMessageBox.Show("Please select one key column", "Flammenwerfer Editor (Import String Window)");
                return false;
            }

            foreach (DataRow row in dt.Rows)
            {
                uint key = funcs[keyIndex](row[keyIndex].ToString(), 0);
                for (int i = 0; i < row.ItemArray.Length; i++)
                {
                    funcs[i](row[i].ToString(), key);
                }
            }

            string currentLanguage = Config.Get("Language", "English", ConfigScope.Game);

            foreach (KeyValuePair<string, List<Tuple<uint, string>>> pair in stringsToAdd)
            {
                if (pair.Value.Count == 0)
                    continue;

                Config.Add("Language", pair.Key, ConfigScope.Game);
                Config.Save();
                db.Initialize();

                foreach (Tuple<uint, string> tuple in pair.Value)
                {
                    db.SetString(tuple.Item1, tuple.Item2);
                }
            }

            Config.Add("Language", currentLanguage, ConfigScope.Game);
            Config.Save();
            db.Initialize();
            return true;
        }

        private long CsvRemoveDuplicatedBOM(long currentPosition)
        {
            long outputPosition = currentPosition;

            using (FileStream stream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.Inheritable))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                stream.Position = currentPosition;
                byte[] buffer = new byte[stream.Length];
                int length = reader.Read(buffer, 0, 8);

                if (length >= 6 &&
                    buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF &&
                    buffer[3] == 0xEF && buffer[4] == 0xBB && buffer[5] == 0xBF)
                {
                    // Duplicated UTF-8 BOM
                    stream.Dispose();
                    outputPosition = CsvRemoveDuplicatedBOM(outputPosition + 3);
                }
                else if (length >= 4 &&
                    buffer[0] == 0xFF && buffer[1] == 0xFE &&
                    buffer[2] == 0xFF && buffer[3] == 0xFE)
                {
                    // Duplicated UTF-16 LE BOM
                    stream.Dispose();
                    outputPosition = CsvRemoveDuplicatedBOM(outputPosition + 2);
                }
                else if (length >= 4 &&
                    buffer[0] == 0xFE && buffer[1] == 0xFF &&
                    buffer[2] == 0xFE && buffer[3] == 0xFF)
                {
                    // Duplicated UTF-16 BE BOM
                    stream.Dispose();
                    outputPosition = CsvRemoveDuplicatedBOM(outputPosition + 2);
                }
                else if (length >= 8 &&
                    buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF &&
                    buffer[4] == 0x00 && buffer[5] == 0x00 && buffer[6] == 0xFE && buffer[7] == 0xFF)
                {
                    // Duplicated UTF-32 BE BOM
                    stream.Dispose();
                    outputPosition = CsvRemoveDuplicatedBOM(outputPosition + 4);
                }
                else if (length >= 8 &&
                    buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00 &&
                    buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
                {
                    // Duplicated UTF-32 LE BOM
                    stream.Dispose();
                    outputPosition = CsvRemoveDuplicatedBOM(outputPosition + 4);
                }
                else
                {
                    // No duplicated BOM
                }
            }

            return outputPosition;
        }

        #endregion









        // Excel header example maybe https://github.com/ExcelDataReader/ExcelDataReader/issues/335



    }
}
