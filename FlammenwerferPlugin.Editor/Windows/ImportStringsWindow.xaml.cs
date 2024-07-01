using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylvan.Data.Csv;
using Sylvan.Data.Excel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlammenwerferPlugin.Editor.Windows
{
    public partial class ImportStringsWindow : FrostyDockableWindow, INotifyPropertyChanged
    {

        #region - Window -

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string FilePath { get; set; }
        public List<ComboBoxItem> ComboBoxItems { get; set; }

        private int _previewCount = 5;
        public int PreviewCount
        {
            get { return _previewCount; }
            set
            {
                _previewCount = value;
                OnPropertyChanged(nameof(PreviewCount));
                OnPropertyChanged(nameof(IsLoadLessButtonEnabled));
            }
        }

        public bool IsLoadLessButtonEnabled => PreviewCount > 1;


        public ImportStringsWindow(Window owner)
        {
            Owner = owner;

            InitializeComponent();

            Left = (Owner.Left + (Owner.Width / 2.0)) - (ActualWidth / 2.0);
            Top = (Owner.Top + (Owner.Height / 2.0)) - (ActualHeight / 2.0);

            CsvGrid.Visibility = Visibility.Collapsed;
            JsonGrid.Visibility = Visibility.Collapsed;
            ExcelGrid.Visibility = Visibility.Collapsed;

            this.DataContext = this;

            Dispatcher.UnhandledException += UnhandledException;

            ComboBoxItems = new List<ComboBoxItem>
            {
                new ComboBoxItem()
                {
                    Name = "None",
                    Content = "None",
                    IsSelected = true
                },
                new ComboBoxItem()
                {
                    Name = "Key",
                    Content = "Key",
                }
            };
            Controls.LocalizedStringEditor.GetLocalizedLanguages()
                .ForEach(x => ComboBoxItems.Add(new ComboBoxItem()
                {
                    Name = x,
                    Content = "String (" + x + ")",
                }));

            JsonFieldTypes = new List<JsonFieldType>
            {
                new JsonFieldType()
                {
                    Name = "None",
                    DisplayName = "None",
                },
                new JsonFieldType()
                {
                    Name = "Key",
                    DisplayName = "Key",
                }
            };
            Controls.LocalizedStringEditor.GetLocalizedLanguages()
                .ForEach(x => JsonFieldTypes.Add(new JsonFieldType()
                {
                    Name = x,
                    DisplayName = "String (" + x + ")",
                }));

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

        private async void LoadMoreButton_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                PreviewCount++;
            });
        }

        private async void LoadLessButton_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(async () =>
            {
                if (PreviewCount > 1)
                {
                    PreviewCount--;
                    if (PreviewCount == 1)
                    {
                        await Task.Delay(200);
                        Dispatcher.Invoke(() =>
                        {
                            LoadFile();
                        });
                    }
                }
            });
        }

        private void PreviewCountChanged(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                await Task.Delay(200);
                Dispatcher.Invoke(() =>
                {
                    LoadFile();
                });
            });
        }
        private void PreviewCountChanged(object sender, KeyEventArgs e)
        {
            Task.Run(async () =>
            {
                await Task.Delay(200);
                Dispatcher.Invoke(() =>
                {
                    LoadFile();
                });
            });
        }
        private void PreviewCountChanged(object sender, MouseButtonEventArgs e)
        {
            Task.Run(async () =>
            {
                await Task.Delay(200);
                Dispatcher.Invoke(() =>
                {
                    LoadFile();
                });
            });
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
                "All supported file (*.csv;*.json;*.xlsx;*.xlsb;*.xls)|*.csv;*.json;*.xlsx;*.xlsb;*.xls|CSV file (*.csv)|*.csv|JSON file (*.json)|*.json|Excel file (*.xlsx;*.xlsb;*.xls)|*.xlsx;*.xlsb;*.xls|All file (*.*)|*.*",
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

        private void OptionsChanged(object sender, RoutedEventArgs e) => LoadFile();
        private void OptionsChanged(object sender, SelectionChangedEventArgs e) => LoadFile();

        private async void ReportProgress(ILogger logger, double current, double total, double currentPart = 1, double totalParts = 1, double detail = 1, double totalDetails = 1)
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
                await Task.Run(() => logger.Log("progress:" + (((((double)currentPart - 1) * (double)total + ((double)current - 1)) * (double)totalDetails + (double)detail) / ((double)totalDetails * (double)total * (double)totalParts)) * 100.0d));
            }
        }

        #endregion

        #region - File -

        private void LoadFile()
        {
            CsvGrid.Visibility = Visibility.Collapsed;
            JsonGrid.Visibility = Visibility.Collapsed;
            ExcelGrid.Visibility = Visibility.Collapsed;
            ConfirmButton.IsEnabled = false;

            if (!File.Exists(FilePath))
            {
                (FileNameLabel.Content as TextBlock).Text = "or drag a file here";
                (FileNameLabel.ToolTip as TextBlock).Text = null;
                FilePath = null;
                return;
            }

            FileInfo fileInfo = new FileInfo(FilePath);
            (FileNameLabel.Content as TextBlock).Text = fileInfo.Name;
            (FileNameLabel.ToolTip as TextBlock).Text = FilePath;


            switch ((FileTypeComboBox.SelectedItem as ComboBoxItem).Name)
            {
                case "Auto":
                    switch (fileInfo.Extension.ToLower())
                    {
                        case ".csv":
                            ShowPreviewCsv();
                            break;

                        case ".json":
                            ShowPreviewJson();
                            break;

                        case ".xlsx":
                        case ".xlsb":
                        case ".xls":
                            ShowPreviewExcel();
                            break;
                        default:
                            CsvGrid.Visibility = Visibility.Collapsed;
                            JsonGrid.Visibility = Visibility.Collapsed;
                            ExcelGrid.Visibility = Visibility.Collapsed;
                            FrostyMessageBox.Show($"Unsupported file type: {fileInfo.Extension.ToLower()}{Environment.NewLine}Manually choose the File Type to force reading", "Flammenwerfer Editor (Import Strings Window)");
                            break;
                    }
                    break;

                case "Csv":
                    ShowPreviewCsv();
                    break;

                case "Json":
                    ShowPreviewJson();
                    break;

                case "Excel":
                    ShowPreviewExcel();
                    break;

                default:
                    throw new FileFormatException("The selected File Type cannot be recognized");
            }

            GC.Collect();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(FilePath))
            {
                CsvGrid.Visibility = Visibility.Collapsed;
                JsonGrid.Visibility = Visibility.Collapsed;
                ExcelGrid.Visibility = Visibility.Collapsed;
                (FileNameLabel.Content as TextBlock).Text = "or drag a file here";
                (FileNameLabel.ToolTip as TextBlock).Text = null;
                FilePath = null;
                FrostyMessageBox.Show("File not found", "Flammenwerfer Editor (Import String Window)");
                return;
            }
            
            FileInfo fileInfo = new FileInfo(FilePath);
            (FileNameLabel.Content as TextBlock).Text = fileInfo.Name;
            (FileNameLabel.ToolTip as TextBlock).Text = FilePath;

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
                            isSuccess = ImportJson();
                            break;

                        case ".xlsx":
                        case ".xlsb":
                        case ".xls":
                            isSuccess = ImportExcel();
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
                    isSuccess = ImportCsv();
                    break;

                case "Json":
                    isSuccess = ImportJson();
                    break;

                case "Excel":
                    isSuccess = ImportExcel();
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

        #endregion

        #region - CSV -

        private void ShowPreviewCsv()
        {
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            FrostyTaskWindow.Show(this, "Loading CSV", "Loading", (task) =>
            {
                try
                {
                    const int totalParts = 2;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    Dispatcher.Invoke(() =>
                    {
                        CsvGrid.Visibility = Visibility.Visible;
                        ConfirmButton.IsEnabled = true;
                        CsvPreviewDataGrid.ItemsSource = null;
                        CsvPreviewDataGrid.Columns.Clear();
                    });

                    task.TaskLogger.Log("[1/2] Reading CSV file");
                    ReportProgress(task.TaskLogger, 0, 1, 1, totalParts);
                    Thread.Sleep(1);

                    DataTable dataTable = ReadPreviewCsv();
                    Dispatcher.Invoke(() =>
                    {
                        CsvPreviewDataGrid.ItemsSource = dataTable.DefaultView;
                    });
                    cancelToken.Token.ThrowIfCancellationRequested();
                    ReportProgress(task.TaskLogger, 1, 1, 1, totalParts);

                    int index = 0;
                    task.TaskLogger.Log("[2/2] Creating preview");
                    Thread.Sleep(1);
                    ReportProgress(task.TaskLogger, 0, 1, 2, totalParts);
                    foreach (DataColumn column in dataTable.Columns)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // Create the header
                            cancelToken.Token.ThrowIfCancellationRequested();
                            ComboBox header = new ComboBox();
                            header.ItemsSource = ComboBoxItems;
                            header.SelectedIndex = 0;
                            header.BorderThickness = new Thickness(1);

                            // Create the cell
                            cancelToken.Token.ThrowIfCancellationRequested();
                            DataTemplate cell = new DataTemplate();
                            var cellTextBlock = new FrameworkElementFactory(typeof(TextBlock));
                            cellTextBlock.SetBinding(TextBlock.TextProperty, new Binding(column.ColumnName)); // Binding the value
                            cellTextBlock.SetValue(TextBlock.ForegroundProperty, FindResource("FontColor"));
                            cellTextBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                            cellTextBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
                            cellTextBlock.SetValue(TextBlock.PaddingProperty, new Thickness(2));
                            cell.VisualTree = cellTextBlock;

                            // Create the column
                            cancelToken.Token.ThrowIfCancellationRequested();
                            DataGridTemplateColumn templateColumn = new DataGridTemplateColumn
                            {
                                Header = header,
                                CellTemplate = cell,
                                CellStyle = (Style)FindResource("DataGridCellStyle"),
                                HeaderStyle = (Style)FindResource("DataGridHeaderStyle")
                            };
                            CsvPreviewDataGrid.Columns.Add(templateColumn);
                        });

                        ReportProgress(task.TaskLogger, ++index, dataTable.Columns.Count, 2, totalParts);
                    }
                }
                catch (OperationCanceledException)
                {
                    // User canceled
                    Dispatcher.Invoke(() =>
                    {
                        CsvPreviewDataGrid.Visibility = Visibility.Collapsed;
                    });
                }
                catch (Exception ex)
                {
                    // If there's really an exception occur, use the DataGrid to display it.
                    Dispatcher.Invoke(() =>
                    {
                        ConfirmButton.IsEnabled = false; 
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
                        sb.AppendLine("Could not read the file as CSV");
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
                    });
                }
            }, true, (task) => cancelToken.Cancel());
        }

        private DataTable ReadPreviewCsv()
        {
            DataTable dt = new DataTable();

            bool hasHeaders = false;
            Dispatcher.Invoke(() =>
            {
                hasHeaders = CsvHasHeaderCheckBox.IsChecked.GetValueOrDefault(false);
            });

            using (CsvDataReader reader = CsvDataReader.Create(FilePath, new CsvDataReaderOptions()
            {
                HasHeaders = hasHeaders
            }))
            {
                for (int i = 0; i < PreviewCount; i++)
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
                                {
                                    keyIndex = i;
                                }
                                else
                                {
                                    // Do not accept multiple key columns
                                    FrostyMessageBox.Show("Please select only one key column", "Flammenwerfer Editor (Import String Window)");
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
                        FrostyMessageBox.Show("Please at least select one key column", "Flammenwerfer Editor (Import String Window)");
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

            GC.Collect();
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

        #region - JSON -

        /// <summary>
        /// Items for JsonFieldsListBox
        /// </summary>
        public ObservableCollection<JsonFieldItem> JsonFieldItems { get; set; } = new ObservableCollection<JsonFieldItem>();

        /// <summary>
        /// Same as <see cref="ComboBoxItems"/>, but with JsonFieldType
        /// </summary>
        public List<JsonFieldType> JsonFieldTypes { get; set; } = new List<JsonFieldType>();

        /// <summary>
        /// Node class for JsonPreviewTreeView
        /// </summary>
        public class JsonTreeNode
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public ObservableCollection<JsonTreeNode> Children { get; set; }

            public JsonTreeNode()
            {
                Children = new ObservableCollection<JsonTreeNode>();
            }
        }

        /// <summary>
        /// Item class for JsonFieldsListBox
        /// </summary>
        public class JsonFieldItem
        {
            public JsonFieldItem(ListBox parent = null, JsonFieldType type = null)
            {
                if (parent != null)
                    Parent = parent;
                if (type != null)
                    Type = type;
            }

            private ListBox Parent;

            private JsonFieldType _type;
            public JsonFieldType Type
            {
                get { return _type; }
                set
                {
                    _type = value;
                    OnPropertyChanged(nameof(Type));
                }
            }
            
            private string _assignTo;
            public string AssignTo
            {
                get { return _assignTo; }
                set
                {
                    _assignTo = value;
                    OnPropertyChanged(nameof(AssignTo));
                }
            }

            public ObservableCollection<string> PopupPreviewContent { get; set; }

            private bool _isPopupOpen;
            public bool IsPopupOpen
            {
                get { return _isPopupOpen; }
                set
                {
                    if (_isPopupOpen != value)
                    {
                        _isPopupOpen = value;
                        OnPropertyChanged(nameof(IsPopupOpen));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                if (Parent != null)
                    Parent.Items.Refresh();
            }
        }

        /// <summary>
        /// About the same as WPF <see cref="ComboBoxItem"/>, use for JsonFieldTypes
        /// </summary>
        public class JsonFieldType
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
        }

        public bool IsJsonAssignButtonEnabled => JsonPreviewTreeView.SelectedItem != null && (JsonPreviewTreeView.SelectedItem as JsonTreeNode).Path != null;
        private void JsonPreviewTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) => OnPropertyChanged(nameof(IsJsonAssignButtonEnabled));

        public bool IsJsonRemoveFieldButtonEnabled => JsonFieldsListBox.SelectedItem != null;
        private void JsonFieldsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => OnPropertyChanged(nameof(IsJsonRemoveFieldButtonEnabled));
        
        private void ShowPreviewJson()
        {
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            FrostyTaskWindow.Show(this, "Loading JSON", "Loading", (task) =>
            {
                try
                {
                    const int totalParts = 3;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    Dispatcher.Invoke(() =>
                    {
                        JsonGrid.Visibility = Visibility.Visible;
                        ConfirmButton.IsEnabled = true;
                        JsonPreviewTreeView.ItemsSource = null;
                        JsonPreviewTreeView.Items.Clear();
                        //JsonFieldItems.Clear();
                    });

                    task.TaskLogger.Log("[1/3] Reading Json file");
                    ReportProgress(task.TaskLogger, 0, 1, 1, totalParts);
                    Thread.Sleep(1);

                    JToken token = ReadJson();

                    task.TaskLogger.Log("[2/3] Removeing unavailable fields");
                    ReportProgress(task.TaskLogger, 0, 1, 2, totalParts);
                    Thread.Sleep(1);

                    int index = 0;
                    foreach (JsonFieldItem item in JsonFieldItems)
                    {
                        if (!token.SelectTokens(item.AssignTo).Any())
                            item.AssignTo = null;
                        ReportProgress(task.TaskLogger, ++index, JsonFieldItems.Count, 2, totalParts);
                    }

                    task.TaskLogger.Log("[3/3] Parsing Json file");
                    ReportProgress(task.TaskLogger, 0, 1, 3, totalParts);
                    Thread.Sleep(1);

                    ObservableCollection<JsonTreeNode> treeNodes = ParseJsonToTree(token);

                    Dispatcher.Invoke(() =>
                    {
                        JsonPreviewTreeView.ItemsSource = treeNodes;
                    });
                }
                catch (OperationCanceledException)
                {
                    // User canceled
                    Dispatcher.Invoke(() =>
                    {
                        JsonGrid.Visibility = Visibility.Collapsed;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        JsonGrid.Visibility = Visibility.Collapsed;
                        FrostyExceptionBox.Show(ex, "Flammenwerfer Editor (Import Strings Window) Could not read the file as JSON");
                    });
                }
            }, true, (task) => cancelToken.Cancel());
        }

        /// <summary>
        /// Read JSON from <see cref="FilePath"/>
        /// </summary>
        private JToken ReadJson()
        {
            // read JSON directly from a file
            using (StreamReader file = new StreamReader(File.OpenRead(FilePath)))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                return JToken.ReadFrom(reader);
            }
        }

        /// <summary>
        /// Parse <see cref="PreviewCount"/> object to TreeView
        /// </summary>
        /// <param name="jToken">JToken to parse</param>
        private ObservableCollection<JsonTreeNode> ParseJsonToTree(JToken jToken)
        {
            ObservableCollection<JsonTreeNode> root = new ObservableCollection<JsonTreeNode>();

            if (jToken == null)
                return root;
            if (jToken is JValue)
            {
                var childItem = new JsonTreeNode { Name = jToken.ToString() };
                root.Add(childItem);
            }
            else if (jToken is JObject obj)
            {
                int count = 0;
                foreach (var property in obj.Properties())
                {
                    if (count >= PreviewCount)
                        break;
                    var childItem = new JsonTreeNode { Name = property.Name, Path = property.Path };
                    root.Add(childItem);
                    ParseJsonTokenToNode(property.Value, childItem);
                }
            }
            else if (jToken is JArray array)
            {
                for (int i = 0; i < array.Count && i < PreviewCount; i++)
                {
                    var childItem = new JsonTreeNode { Name = i.ToString(), Path = array[i].Path };
                    root.Add(childItem);
                    ParseJsonTokenToNode(array[i], childItem);
                }
            }

            return root;
        }

        // https://stackoverflow.com/questions/23812357/how-to-bind-dynamic-json-into-treeview-wpf/28097883
        private void ParseJsonTokenToNode(JToken token, JsonTreeNode inTreeNode)
        {
            if (token == null)
                return;
            if (token is JValue)
            {
                var childItem = new JsonTreeNode { Name = token.ToString(), Path = token.Path };
                inTreeNode.Children.Add(childItem);
            }
            else if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    var childItem = new JsonTreeNode { Name = property.Name, Path = property.Path };
                    inTreeNode.Children.Add(childItem);
                    ParseJsonTokenToNode(property.Value, childItem);
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    var childItem = new JsonTreeNode { Name = i.ToString(), Path = array[i].Path };
                    inTreeNode.Children.Add(childItem);
                    ParseJsonTokenToNode(array[i], childItem);
                }
            }
        }

        private void JsonAddItemButton_Click(object sender, RoutedEventArgs e)
        {
            JsonFieldItems.Add(new JsonFieldItem(JsonFieldsListBox, JsonFieldTypes[0]));
        }

        private void JsonRemoveItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsJsonRemoveFieldButtonEnabled)
            {
                var selectedIndex = JsonFieldsListBox.SelectedIndex;
                JsonFieldItems.Remove(JsonFieldsListBox.SelectedItem as JsonFieldItem);
                JsonFieldsListBox.SelectedIndex = selectedIndex;
            }
        }

        private void JsonAssignButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsJsonAssignButtonEnabled &&
                sender is Button button &&
                button.Tag is JsonFieldItem fieldItem)
            {
                fieldItem.AssignTo = (JsonPreviewTreeView.SelectedItem as JsonTreeNode).Path;
            }
        }

        /// <summary>
        /// Show what will the current JSONPath select
        /// </summary>
        private void JsonJPathTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.Tag is Popup popup &&
                popup.Tag is JsonFieldItem fieldItem &&
                !String.IsNullOrEmpty(fieldItem.AssignTo))
            {
                fieldItem.PopupPreviewContent = new ObservableCollection<string>(ReadJson().SelectTokens(fieldItem.AssignTo).Take(PreviewCount).Select(token => token.ToString()));
                fieldItem.IsPopupOpen = true;
                ((popup.Child) as ListBox).Items.Refresh();
            }
        }

        /// <summary>
        /// Provide instructions to the user and automatically detect JSONPath
        /// </summary>
        private void JsonPathHelpButton_Click(object sender, RoutedEventArgs e)
        {
            string recommendedJsonPath = null;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("In JSON mode, this window will use JSONPath to select content and import");
            sb.AppendLine("Use Assign (right arrow) button to set the path of the currently selected json object");
            sb.AppendLine("Use Test button to preview the items that will selected by the path (displayed count is sync with the preview count)");
            sb.AppendLine();
            if (JsonFieldsListBox.SelectedItem != null && JsonFieldsListBox.SelectedItem is JsonFieldItem fieldItem && !String.IsNullOrEmpty(fieldItem.AssignTo) &&
                fieldItem.AssignTo.Split('.') is string[] paths && paths.Length > 1)
            {
                bool isBracketsFinded = false;
                for (int i = 0; i < paths.Length; i++)
                {
                    string opt = Regex.Replace(paths[i], @"\[\d+\]", "[*]");
                    if (opt != paths[i])
                    {
                        paths[i] = opt;
                        isBracketsFinded = true;
                        break;
                    }
                }

                if (isBracketsFinded)
                {
                    recommendedJsonPath = String.Join(".", paths);
                    sb.Append("Selected field item is assign to: ");
                    sb.AppendLine(fieldItem.AssignTo);
                    sb.Append("Recommended JSONPath: ");
                    sb.AppendLine(recommendedJsonPath);
                    sb.AppendLine();
                    sb.AppendLine("Click Yes to open JSONPath document");
                    sb.Append("Click No will replace current path with recommended one");
                }
                else
                {
                    paths[paths.Length - 1] = "*";
                    recommendedJsonPath = String.Join(".", paths);
                    sb.Append("Selected field item is assign to: ");
                    sb.AppendLine(fieldItem.AssignTo);
                    sb.Append("Recommended JSONPath: ");
                    sb.AppendLine(recommendedJsonPath);
                    sb.AppendLine();
                    sb.AppendLine("Click Yes to open JSONPath document");
                    sb.Append("Click No will replace current path with recommended one");
                }
            }
            else
            {
                sb.AppendLine("Click Yes to open JSONPath document");
                sb.Append("Click No will do nothing, because no field selected");
            }

            MessageBoxResult result = FrostyMessageBox.Show(sb.ToString(),
                "JSONPath Help",
                MessageBoxButton.YesNoCancel); 

            switch (result)
            {
                case MessageBoxResult.Yes:
                    Process.Start(@"https://goessner.net/articles/JsonPath/");
                    break;
                case MessageBoxResult.No:
                    if (recommendedJsonPath != null)
                    {
                        (JsonFieldsListBox.SelectedItem as JsonFieldItem).AssignTo = recommendedJsonPath;
                    }
                    break;
            }
        }

        private bool ImportJson()
        {
            bool result = true;
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            FrostyTaskWindow.Show(this, "Importing JSON", "Loading", (task) =>
            {
                try
                {
                    const int totalParts = 5;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    ILocalizedStringDatabase db = LocalizedStringDatabase.Current;
                    JToken jToken;
                    Dictionary<string, List<string>> languages = new Dictionary<string, List<string>>();

                    // Step 1: Loading Languages
                    task.TaskLogger.Log("[1/5] Loading Languages");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 1, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);
                    List<string> localizedLanguages = Controls.LocalizedStringEditor.GetLocalizedLanguages();
                    int index = 0;

                    foreach (string lang in localizedLanguages)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        // Initialize dictionary with languages
                        languages.Add(lang, new List<string>());
                        ReportProgress(task.TaskLogger, ++index, localizedLanguages.Count, currentPart: 1, totalParts);
                    }

                    // Step 2: Read Json
                    task.TaskLogger.Log("[2/5] Reading JSON");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 2, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    jToken = ReadJson();

                    GC.Collect();

                    // Step 3: Process fields
                    task.TaskLogger.Log("[3/5] Processing fields");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 3, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    string keyPath = null;
                    index = 0;

                    // Process the usage of each field into language list
                    foreach (JsonFieldItem item in JsonFieldItems)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();

                        // Get usage
                        switch (item.Type.Name)
                        {
                            case null:
                            case "":
                            case "None":
                                // Do nothing
                                break;

                            case "Key":
                                // Key column
                                if (String.IsNullOrEmpty(keyPath))
                                {
                                    keyPath = item.AssignTo;
                                }
                                else
                                {
                                    // Do not accept multiple key
                                    FrostyMessageBox.Show("Please provide only one key field", "Flammenwerfer Editor (Import String Window)");
                                    result = false;
                                    return;
                                }
                                break;

                            default:
                                // String column
                                languages[item.Type.Name].Add(item.AssignTo);
                                break;
                        }
                        ReportProgress(task.TaskLogger, ++index, JsonFieldItems.Count, currentPart: 3, totalParts);
                    }

                    if (String.IsNullOrEmpty(keyPath))
                    {
                        // Check if key path exists
                        FrostyMessageBox.Show("Please at least provide one key field", "Flammenwerfer Editor (Import String Window)");
                        result = false;
                        return;
                    }

                    // Step 4: Process keys
                    task.TaskLogger.Log("[4/5] Processing keys");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 4, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    index = 0;
                    List<uint> keys = new List<uint>();
                    int keyCount = jToken.SelectTokens(keyPath).Count();
                    foreach (JToken token in jToken.SelectTokens(keyPath))
                    {
                        keys.Add(uint.Parse(token.ToString(), System.Globalization.NumberStyles.HexNumber));
                        ReportProgress(task.TaskLogger, ++index, keyCount, currentPart: 4, totalParts);
                    }

                    // Step 5: Import Strings
                    task.TaskLogger.Log("[5/5] Importing strings");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 5, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    index = 0;
                    int totalAdded = 0;
                    int totalModified = 0;
                    int totalIgnored = 0;
                    int totalSame = 0;
                    int totalLanguage = 0;

                    // Calculate total languages
                    foreach (KeyValuePair<string, List<string>> pair in languages)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        if (pair.Value.Count != 0)
                            totalLanguage++;
                    }

                    // Function to calculate more details
                    Func<double, double, double, double, double> calculateDetails = (current, total, currentPart, totalPart) =>
                    {
                        double Result = 0;
                        Dispatcher.InvokeAsync(() =>
                        {
                            Result = ((currentPart - 1) * totalPart + current) / (totalPart * total) * 100.0d;
                        }).Wait();
                        return Result;
                    };

                    // Back up the language currently selected by the user
                    string currentLanguage = Config.Get("Language", "English", ConfigScope.Game);
                    // Iterate every languages and the string paths for the language
                    // pair.Key is the language, pair.Value is the string list for the pair.Key
                    foreach (KeyValuePair<string, List<string>> pair in languages)
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
                        int index2 = 0;
                        foreach (string path in pair.Value)
                        {
                            index2++;
                            List<JToken> tokens = jToken.SelectTokens(path).ToList();

                            if (tokens.Count != keys.Count)
                            {
                                App.Logger.LogError($"The amount of data for this path is incorrect: {path}");
                                continue;
                            }
                            for (int i = 0; i < tokens.Count; i++)
                            {
                                cancelToken.Token.ThrowIfCancellationRequested();

                                if (db.GetString(keys[i]) == tokens[i].ToString())
                                    totalSame++;
                                else if (currentStringIds.Contains(keys[i]))
                                    totalModified++;
                                else
                                    totalAdded++;

                                db.SetString(keys[i], tokens[i].ToString());
                                totalSet++;

                                // Make users feel fast
                                task.TaskLogger.Log($"[5/5] Importing strings ({keys[i].ToString("X")})");
                                ReportProgress(task.TaskLogger, index, totalLanguage, 5, totalParts,
                                    calculateDetails(i + 1, tokens.Count, index2, pair.Value.Count),
                                    100.0d);
                            }
                        }
                        totalIgnored += currentStringIds.Count() - totalSet;

                        ReportProgress(task.TaskLogger, index, totalLanguage, currentPart: 5, totalParts);
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
                    App.Logger.Log("JSON import canceled");
                }
            }, true, (task) => cancelToken.Cancel());

            GC.Collect();
            return result;
        }

        #endregion

        #region - Excel -

        private void ShowPreviewExcel()
        {
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            FrostyTaskWindow.Show(this, "Loading Excel file", "Loading", (task) =>
            {
                try
                {
                    const int totalParts = 2;
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Dispatcher.Invoke(() =>
                    {
                        ExcelGrid.Visibility = Visibility.Visible;
                        ConfirmButton.IsEnabled = true;
                        ExcelPreviewTabControl.Items.Clear();
                    });

                    task.TaskLogger.Log("[1/2] Reading Excel file");
                    ReportProgress(task.TaskLogger, 0, 1, 1, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);
                    DataSet dataSet = ReadPreviewExcel();
                    cancelToken.Token.ThrowIfCancellationRequested();
                    ReportProgress(task.TaskLogger, 1, 1, 1, totalParts);

                    int index = 0;
                    int index2 = 0;
                    task.TaskLogger.Log("[2/2] Creating preview");
                    Thread.Sleep(1);
                    ReportProgress(task.TaskLogger, 0, 1, 2, totalParts);
                    foreach (DataTable table in dataSet.Tables)
                    {
                        index++;

                        Dispatcher.Invoke(() =>
                        {
                            // Create a tab
                            TabItem tabItem = new TabItem
                            {
                                Header = table.TableName
                            };

                            DataGrid dataGrid = new DataGrid
                            {
                                Background = (Brush)FindResource("ListBackground"),
                                Foreground = (Brush)FindResource("FontColor"),
                                BorderThickness = new Thickness(0),
                                ColumnHeaderStyle = (Style)FindResource("DataGridHeaderStyle"),
                                CellStyle = (Style)FindResource("DataGridCellStyle"),
                                AutoGenerateColumns = false,
                                HeadersVisibility = DataGridHeadersVisibility.Column,
                                RowHeaderWidth = 0,
                                RowStyle = (Style)FindResource("DataGridRowStyle"),
                                MinColumnWidth = 25,
                                ColumnWidth = 175,
                                GridLinesVisibility = DataGridGridLinesVisibility.None,
                                CanUserAddRows = false,
                                CanUserDeleteRows = false,
                                CanUserReorderColumns = false,
                                CanUserResizeColumns = true,
                                CanUserResizeRows = true,
                                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                            };

                            dataGrid.ItemsSource = table.DefaultView;

                            index2 = 0;
                            foreach (DataColumn column in table.Columns)
                            {
                                // Create the header
                                ComboBox header = new ComboBox();
                                header.ItemsSource = ComboBoxItems;
                                header.SelectedIndex = 0;
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
                                dataGrid.Columns.Add(templateColumn);
                                ReportProgress(task.TaskLogger, index, dataSet.Tables.Count, 2, totalParts, ++index2, table.Columns.Count);
                            }

                            tabItem.Content = dataGrid;
                            ExcelPreviewTabControl.Items.Add(tabItem);
                        });
                        ReportProgress(task.TaskLogger, index, dataSet.Tables.Count, 2, totalParts);
                    }
                    Dispatcher.Invoke(() =>
                    {
                        if (ExcelPreviewTabControl.Items.Count >= 1)
                            ExcelPreviewTabControl.SelectedIndex = 0;
                    });
                }
                catch (OperationCanceledException)
                {
                    // User canceled
                    Dispatcher.Invoke(() =>
                    {
                        ExcelPreviewTabControl.Visibility = Visibility.Collapsed;
                    });
                }
                catch (Exception ex)
                {
                    // If there's really an exception occur, use the TabControl to display it.
                    Dispatcher.Invoke(() =>
                    {
                        ConfirmButton.IsEnabled = false; 
                        ExcelPreviewTabControl.Items.Clear();

                        // Create a tab
                        TabItem tabItem = new TabItem
                        {
                            Header = "Exception"
                        };

                        // Create the content
                        TextBox textBox = new TextBox();
                        textBox.IsReadOnly = true;
                        textBox.Width = ActualWidth - 30;
                        textBox.BorderThickness = new Thickness(1);
                        textBox.TextWrapping = TextWrapping.WrapWithOverflow;
                        textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                        textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                        // Create exception message
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("Could not read the file as Excel file");
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

                        tabItem.Content = textBox;
                        ExcelPreviewTabControl.Items.Add(tabItem);
                        ExcelPreviewTabControl.SelectedItem = tabItem;
                    });
                }
            }, true, (task) => cancelToken.Cancel());
        }

        private DataSet ReadPreviewExcel()
        {
            DataSet ds = new DataSet();

            bool isReadHiddenWorksheets = false;
            Dispatcher.Invoke(() =>
            {
                isReadHiddenWorksheets = ExcelReadHiddenWorksheetsCheckBox.IsChecked.GetValueOrDefault(false);
            });

            using (ExcelDataReader reader = ExcelDataReader.Create(FilePath, new ExcelDataReaderOptions()
            {
                ReadHiddenWorksheets = isReadHiddenWorksheets
            }))
            {
                do
                {
                    DataTable dt = new DataTable();
                    dt.TableName = reader.WorksheetName;

                    // Skip first row (header)
                    Dispatcher.Invoke(() =>
                    {
                        if (ExcelHasHeaderCheckBox.IsChecked.GetValueOrDefault(false))
                            reader.Read();
                    });

                    for (int i = 0; i < PreviewCount; i++)
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
                    ds.Tables.Add(dt);

                } while (reader.NextResult());
            }
            return ds;
        }

        private bool ImportExcel()
        {
            bool result = true;
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            FrostyTaskWindow.Show(this, "Importing Excel file", "Loading", (task) =>
            {
                try
                {
                    const int totalParts = 5;
                    cancelToken.Token.ThrowIfCancellationRequested();

                    ILocalizedStringDatabase db = LocalizedStringDatabase.Current;
                    DataTable dt = new DataTable();
                    Dictionary<int, Func<string, uint, uint>> funcs = new Dictionary<int, Func<string, uint, uint>>();
                    Dictionary<string, List<Tuple<uint, string>>> stringsToAdd = new Dictionary<string, List<Tuple<uint, string>>>();

                    // Step 1: Loading Languages
                    task.TaskLogger.Log("[1/5] Loading Languages");
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

                    // Step 2: Read Excel file
                    task.TaskLogger.Log("[2/5] Reading Excel file");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 2, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    bool isReadHiddenWorksheets = false;
                    Dispatcher.Invoke(() =>
                    {
                        isReadHiddenWorksheets = ExcelReadHiddenWorksheetsCheckBox.IsChecked.GetValueOrDefault(false);
                    });

                    using (ExcelDataReader reader = ExcelDataReader.Create(FilePath, new ExcelDataReaderOptions()
                    {
                        ReadHiddenWorksheets = isReadHiddenWorksheets
                    }))
                    {
                        // Move to selected worksheet
                        Dispatcher.Invoke(() =>
                        {
                            for (int i = 0; i < ExcelPreviewTabControl.SelectedIndex; i++)
                            {
                                reader.NextResult();
                            }
                        });
                        

                        // Skip first row (header)
                        Dispatcher.Invoke(() =>
                        {
                            if (ExcelHasHeaderCheckBox.IsChecked.GetValueOrDefault(false))
                                reader.Read();
                        });

                        while (reader.Read())
                        {
                            cancelToken.Token.ThrowIfCancellationRequested();
                            DataRow dr = dt.NewRow();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                // Add column if not exist, column name is index (i)
                                if (!dt.Columns.Contains(i.ToString()))
                                    dt.Columns.Add(i.ToString(), typeof(string));
                                dr[i] = reader.GetString(i);
                                cancelToken.Token.ThrowIfCancellationRequested();
                            }
                            dt.Rows.Add(dr);
                        }
                    }

                    GC.Collect();

                    // Step 3: Process column header to actions
                    task.TaskLogger.Log("[3/5] Processing columns");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 3, totalParts);
                    cancelToken.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);

                    int keyIndex = -1;

                    int previewDataGridColumnsCount = 0;
                    Dispatcher.Invoke(() =>
                    {
                        previewDataGridColumnsCount = ((ExcelPreviewTabControl.SelectedItem as TabItem).Content as DataGrid).Columns.Count;
                    });

                    // Iterate through the selected usages for each column
                    for (int i = 0; i < previewDataGridColumnsCount; i++)
                    {
                        cancelToken.Token.ThrowIfCancellationRequested();
                        // Get usage from header ComboBox
                        string name = null;
                        Dispatcher.Invoke(() =>
                        {
                            name = ((((ExcelPreviewTabControl.SelectedItem as TabItem).Content as DataGrid).Columns[i].Header as ComboBox).SelectedItem as ComboBoxItem).Name;
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
                                {
                                    keyIndex = i;
                                }
                                else
                                {
                                    // Do not accept multiple key columns
                                    FrostyMessageBox.Show("Please select only one key column", "Flammenwerfer Editor (Import String Window)");
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
                        ReportProgress(task.TaskLogger, i + 1, previewDataGridColumnsCount, currentPart: 3, totalParts);
                    }

                    if (keyIndex == -1)
                    {
                        // Check if key column exists
                        FrostyMessageBox.Show("Please at least select one key column", "Flammenwerfer Editor (Import String Window)");
                        result = false;
                        return;
                    }

                    // Step 4: Run actions
                    task.TaskLogger.Log("[4/5] Preparing strings");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 4, totalParts);
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
                        ReportProgress(task.TaskLogger, ++index, dt.Rows.Count, currentPart: 4, totalParts);
                    }

                    // Step 5: Import Strings
                    task.TaskLogger.Log("[5/5] Importing strings");
                    ReportProgress(task.TaskLogger, 0, 1, currentPart: 5, totalParts);
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
                            task.TaskLogger.Log($"[5/5] Importing strings ({tuple.Item1.ToString("X")})");
                            ReportProgress(task.TaskLogger, index, totalLanguage, 5, totalParts, totalSet, pair.Value.Count);
                        }
                        totalIgnored += currentStringIds.Count() - totalSet;

                        ReportProgress(task.TaskLogger, index, totalLanguage, currentPart: 5, totalParts);
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
                    App.Logger.Log("Excel import canceled");
                }
            }, true, (task) => cancelToken.Cancel());

            GC.Collect();
            return result;
        }

        #endregion

    }
}
