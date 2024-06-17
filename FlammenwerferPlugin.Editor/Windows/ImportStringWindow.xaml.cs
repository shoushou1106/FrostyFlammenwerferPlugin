using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Sdk.AnthemDemo;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;
using Sylvan.Data.Csv;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

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

            Dispatcher.UnhandledException += UnhandledException;

            // Manually set to prevent exception
            FileTypeComboBox.SelectionChanged += OptionsChanged;
        }

        private void UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            FrostyExceptionBox.Show(e.Exception, "Flammenwerfer Editor (Import Strings)");
            DialogResult = false;
            Close();
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

        private void OptionsChanged(object sender, RoutedEventArgs e)
        {
            LoadFile();
        }

        private void OptionsChanged(object sender, SelectionChangedEventArgs e)
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

        private async void ReportProgress(ILogger logger, int current, int total, int currentPart = 1, int totalParts = 1, int detail = 1, int totalDetails = 1)
        {
            if (total > 0)
            {
                // totalParts = tp
                // currentPart = p
                // total = t
                // current = c
                // totalDetails = td
                // detail = d
                // ((((p - 1) * t + (c - 1)) * td + d) / (tp * t * td)) * 100%
                await Task.Run(() =>
                {
                    logger.Log("progress:" + (((((double)currentPart - 1) * (double)total + ((double)current - 1)) * (double)totalDetails + (double)detail) / ((double)totalDetails * (double)total * (double)totalParts)) * 100.0d);
                });
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
                    // Create the header
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

                    // Create the cell
                    DataTemplate cell = new DataTemplate();
                    var cellTextBlock = new FrameworkElementFactory(typeof(TextBlock));
                    cellTextBlock.SetBinding(TextBlock.TextProperty, new Binding(column.ColumnName)); // Binding the value
                    cellTextBlock.SetValue(TextBlock.ForegroundProperty, FindResource("FontColor"));
                    cellTextBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                    cellTextBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
                    cellTextBlock.SetValue(TextBlock.PaddingProperty, new Thickness(2));
                    cell.VisualTree = cellTextBlock;

                    // Create the column
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
                // If there's really an exception occur, use the DataGrid to display it.

                // Allow user to force import
                //ConfirmButton.IsEnabled = false; 
                CsvPreviewDataGrid.ItemsSource = null;
                CsvPreviewDataGrid.Columns.Clear();

                // Create the header
                TextBox textBox = new TextBox();
                textBox.IsReadOnly = true;
                textBox.Width = ActualWidth - 30;
                textBox.BorderThickness = new Thickness(1);
                textBox.TextWrapping = TextWrapping.WrapWithOverflow;
                textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                // Create exception message
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

                // Create column
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

            using (CsvDataReader reader = CsvDataReader.Create(FilePath, new CsvDataReaderOptions()
            {
                HasHeaders = CsvHasHeaderCheckBox.IsChecked.GetValueOrDefault(false)
            }))
            {
                for (int i = 0; i < 5; i++)
                {
                    if (!reader.Read())
                        break;
                    DataRow dr = dt.NewRow();
                    for (int j = 0; j < reader.FieldCount; j++)
                    {
                        // Add column if not exist, column name is index (i)
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
            bool result = true;
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            FrostyTaskWindow.Show(this, "Importing CSV", "Loading", (task) =>
            {
                try
                {
                    const int totalParts = 6;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    ILocalizedStringDatabase db = LocalizedStringDatabase.Current;
                    DataTable dt = new DataTable();
                    Dictionary<int, Func<string, uint, uint>> funcs = new Dictionary<int, Func<string, uint, uint>>();
                    Dictionary<string, List<Tuple<uint, string>>> stringsToAdd = new Dictionary<string, List<Tuple<uint, string>>>();

                    // Step 1: Loading Languages
                    task.TaskLogger.Log("[1/6] Loading Languages");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 1, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);
                    int languagesCount = Controls.LocalizedStringEditor.GetLocalizedLanguages().Count;
                    int index = 0;

                    foreach (string lang in Controls.LocalizedStringEditor.GetLocalizedLanguages())
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        // Initialize dictionary with languages
                        stringsToAdd.Add(lang, new List<Tuple<uint, string>>());
                        ReportProgress(task.TaskLogger, ++index, languagesCount, currentPart: 1, totalParts);
                    }

                    // Step 2: Remove duplicated BOM
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 2, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    long positionStart = 0;
                    bool isRemoveDuplicatedBom = false;
                    Dispatcher.Invoke(() =>
                    {
                        isRemoveDuplicatedBom = CsvRemoveDuplicatedBomCheckBox.IsChecked.GetValueOrDefault(false);
                    });
                    if (isRemoveDuplicatedBom)
                    {
                        task.TaskLogger.Log("[2/6] Removing duplicated BOM");

                        positionStart = CsvRemoveDuplicatedBOM(0);
                        cancelToken.Token.ThrowIfCancellationRequested();
                        GC.Collect();
                    }

                    // Step 3: Read CSV file
                    task.TaskLogger.Log("[3/6] Reading CSV file");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 3, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    bool hasHeader = false;
                    Dispatcher.Invoke(() =>
                    {
                        hasHeader = CsvHasHeaderCheckBox.IsChecked.GetValueOrDefault(false);
                    });
                    using (StreamReader reader = new StreamReader(File.OpenRead(FilePath)))
                    {
                        reader.BaseStream.Position = positionStart;
                        using (CsvDataReader csvReader = CsvDataReader.Create(reader, new CsvDataReaderOptions()
                        {
                            HasHeaders = hasHeader,
                        }))
                        {
                            while (csvReader.Read())
                            {
                                cancelToken.Token.ThrowIfCancellationRequested();
                                DataRow dr = dt.NewRow();
                                for (int i = 0; i < csvReader.FieldCount; i++)
                                {
                                    // Add column if not exist, column name is index (i)
                                    if (!dt.Columns.Contains(i.ToString()))
                                        dt.Columns.Add(i.ToString(), typeof(string));
                                    dr[i] = csvReader.GetString(i);
                                    cancelToken.Token.ThrowIfCancellationRequested();
                                }
                                dt.Rows.Add(dr);
                            }
                        }
                    }

                    GC.Collect();

                    // Step 4: Process column header to actions
                    task.TaskLogger.Log("[4/6] Processing columns");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 4, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    int keyIndex = -1;

                    int previewDataGridColumnsCount = 0;
                    Dispatcher.Invoke(() =>
                    {
                        previewDataGridColumnsCount = CsvPreviewDataGrid.Columns.Count;
                    });

                    // Iterate through the selected usages for each column
                    for (int i = 0; i < previewDataGridColumnsCount; i++)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        // Get usage from header ComboBox
                        string name = null;
                        Dispatcher.Invoke(() =>
                        {
                            name = ((CsvPreviewDataGrid.Columns[i].Header as ComboBox).SelectedItem as ComboBoxItem).Name;
                        });
                        switch (name)
                        {
                            case null:
                            case "":
                            case "None":
                                // Do nothing
                                Func<string, uint, uint> noneAction = (_, inKey) =>
                                {
                                    return inKey;
                                };
                                funcs.Add(i, noneAction);
                                break;

                            case "Key":
                                // Key column
                                if (keyIndex == -1)
                                    keyIndex = i;
                                else
                                {
                                    // Do not accept multiple key columns
                                    FrostyMessageBox.Show("Please only select one key column", "Flammenwerfer Editor (Import String Window)");
                                    result = false;
                                    return;
                                }

                                Func<string, uint, uint> keyAction = (inValue, _) =>
                                {
                                    // Return the parsed key
                                    return uint.Parse(inValue, System.Globalization.NumberStyles.HexNumber);
                                };
                                funcs.Add(i, keyAction);
                                break;

                            default:
                                // String column
                                Func<string, uint, uint> stringAction = (inValue, inKey) =>
                                {
                                    // Add string and key to stringsToAdd list
                                    stringsToAdd[name].Add(new Tuple<uint, string>(inKey, inValue));
                                    return inKey;
                                };
                                funcs.Add(i, stringAction);
                                break;
                        }
                        ReportProgress(task.TaskLogger, i + 1, previewDataGridColumnsCount, currentPart: 4, totalParts);
                    }

                    if (keyIndex == -1)
                    {
                        // Check if key column exists
                        FrostyMessageBox.Show("Please select one key column", "Flammenwerfer Editor (Import String Window)");
                        result = false;
                        return;
                    }

                    // Step 5: Run actions
                    task.TaskLogger.Log("[5/6] Preparing strings");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 5, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    index = 0;

                    // Run actions that processed at step 4 for each row
                    foreach (DataRow row in dt.Rows)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        // Get the key first from key column
                        uint key = funcs[keyIndex](row[keyIndex].ToString(), 0);
                        // Iterate every value in the row
                        for (int i = 0; i < row.ItemArray.Length; i++)
                        {
                            // Will no nothing if this value is in                          None column
                            // Will get the same key if this value is in                    Key column
                            // Will add the string to stringsToAdd if this value is in      String column
                            funcs[i](row[i].ToString(), key);
                        }
                        ReportProgress(task.TaskLogger, ++index, dt.Rows.Count, currentPart: 5, totalParts);
                    }

                    // Step 6: Import Strings
                    task.TaskLogger.Log("[6/6] Importing strings");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 6, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    index = 0;
                    int totalAdded = 0;
                    int totalModified = 0;
                    int totalIgnored = 0;
                    int totalSame = 0;
                    int totalLanguage = 0;

                    // Calculate total languages
                    foreach (KeyValuePair<string, List<Tuple<uint, string>>> pair in stringsToAdd)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        if (pair.Value.Count != 0)
                            totalLanguage++;
                    }

                    // Back up the language currently selected by the user
                    string currentLanguage = Config.Get("Language", "English", ConfigScope.Game);
                    // Iterate every languages and the strings for the language
                    // pair.Key is the language, pair.Value is the string list for the pair.Key
                    foreach (KeyValuePair<string, List<Tuple<uint, string>>> pair in stringsToAdd)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        if (pair.Value.Count == 0)
                            continue;
                        index++;

                        // Switch to language
                        Config.Add("Language", pair.Key, ConfigScope.Game);
                        Config.Save();
                        db.Initialize();
                        var currentStringIds = db.EnumerateStrings().Distinct().AsQueryable();
                        int totalSet = 0;

                        // Add strings
                        //Parallel.ForEach(pair.Value, new ParallelOptions() { CancellationToken = cancelToken.Token, MaxDegreeOfParallelism = 4 }, (tuple) =>
                        foreach (Tuple<uint, string> tuple in pair.Value)
                        {
                            cancelToken.Token.ThrowIfCancellationRequested();

                            if (db.GetString(tuple.Item1) == tuple.Item2)
                                totalSame++;
                            else if (currentStringIds.Contains(tuple.Item1))
                                totalModified++;
                            else
                                totalAdded++;

                            db.SetString(tuple.Item1, tuple.Item2);
                            totalSet++;

                            // Make users feel fast
                            task.TaskLogger.Log($"[6/6] Importing strings ({tuple.Item1.ToString("X")})");
                            ReportProgress(task.TaskLogger, index, totalLanguage, 6, totalParts, totalSet, pair.Value.Count);
                        }
                        totalIgnored += currentStringIds.Count() - totalSet;

                        ReportProgress(task.TaskLogger, index, totalLanguage, currentPart: 6, totalParts);
                    }
                    App.Logger.Log($"{totalModified} strings modified, {totalAdded} strings added, {totalSame} strings same and {totalIgnored} strings ignored. In {totalLanguage} languages.");

                    cancelToken.Token.ThrowIfCancellationRequested();
                    task.TaskLogger.Log("Finishing");
                    ReportProgress(task.TaskLogger, 1, 1, 1, 1);
                    Thread.Sleep(1);

                    // Switch back to the previously backed-up language
                    Config.Add("Language", currentLanguage, ConfigScope.Game);
                    Config.Save();
                    db.Initialize();
                }
                catch (OperationCanceledException)
                {
                    // User canceled
                    result = false;
                    App.Logger.Log("CSV import canceled");
                }
            }, true, (task) => cancelToken.Cancel());

            return result;
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

    }
}
