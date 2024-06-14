using ExcelDataReader;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FlammenwerferPlugin.Editor.Windows
{
    public partial class ImportStringWindow : FrostyDockableWindow
    {
        private bool IsCenterWindow { get; set; } = true;
        private string FilePath { get; set; }

        public ImportStringWindow(Window owner)
        {
            Owner = owner;

            InitializeComponent();

            Left = (Owner.Left + (Owner.Width / 2.0)) - (ActualWidth / 2.0);
            Top = (Owner.Top + (Owner.Height / 2.0)) - (ActualHeight / 2.0) / 0.8;

            CsvGrid.Visibility = Visibility.Collapsed;
            JsonGrid.Visibility = Visibility.Collapsed;
            ExcelGrid.Visibility = Visibility.Collapsed;
            FileTypeComboBox.SelectionChanged += FileTypeComboBox_SelectionChanged;
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

        private void FileTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadFile();
        }

        private void LoadFile()
        {
            CsvGrid.Visibility = Visibility.Collapsed;
            JsonGrid.Visibility = Visibility.Collapsed;
            ExcelGrid.Visibility = Visibility.Collapsed;

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
                            JsonGrid.Visibility = Visibility.Visible;
                            //ReadCsv(path);
                            break;

                        case ".xlsx":
                        case ".xlsb":
                        case ".xls":
                            Excel.Visibility = Visibility.Visible;
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
                    CsvGrid.Visibility = Visibility.Visible;
                    ReadPreviewCsv();
                    break;

                case "Json":
                    JsonGrid.Visibility = Visibility.Visible;
                    //ReadCsv(path);
                    break;

                case "Excel":
                    ExcelGrid.Visibility = Visibility.Visible;
                    //ReadCsv(path);
                    break;

                default:
                    throw new FileFormatException("The selected File Type cannot be recognized");
            }
        }

        private void ShowPreviewCsv()
        {
            CsvGrid.Visibility = Visibility.Visible;
            CsvPreviewDataGrid.ItemsSource = null;
            CsvPreviewDataGrid.Columns.Clear();
            DataTable dataTable = ReadPreviewCsv();
            CsvPreviewDataGrid.ItemsSource = dataTable.DefaultView;

            foreach (DataColumn column in dataTable.Columns)
            {
                DockPanel header = new DockPanel();
                header.LastChildFill = true;
                header.HorizontalAlignment = HorizontalAlignment.Stretch;
                ComboBox headerComboBox = new ComboBox();
                headerComboBox.Items.Add(new ComboBoxItem()
                {
                    Name = "None",
                    Content = "None",
                });
                headerComboBox.Items.Add(new ComboBoxItem()
                {
                    Name = "Key",
                    Content = "Key",
                });
                headerComboBox.SelectedIndex = 0;
                Controls.LocalizedStringEditor.GetLocalizedLanguages()
                    .ForEach(x => headerComboBox.Items.Add(new ComboBoxItem()
                    {
                        Name = x,
                        Content = "String (" + x + ")",
                    }));
                headerComboBox.BorderThickness = new Thickness(1);
                header.HorizontalAlignment = HorizontalAlignment.Stretch;
                header.Children.Add(headerComboBox);

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

        private DataTable ReadPreviewCsv()
        {
            DataTable dt = new DataTable();

            using (FileStream stream = File.Open(FilePath, FileMode.Open, FileAccess.Read))
            {
                using (IExcelDataReader reader = ExcelReaderFactory.CreateCsvReader(stream))
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
            }

            return dt;
        }





        // Excel header https://github.com/ExcelDataReader/ExcelDataReader/issues/335



    }
}
