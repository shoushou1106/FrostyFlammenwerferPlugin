using System;
using System.Windows;
using FlammenwerferPlugin.Resources;
using Frosty.Controls;
using Frosty.Core;

namespace FlammenwerferPlugin.Windows
{
    public partial class AddStringWindow : FrostyDockableWindow
    {
        public string ProfileName { get; set; }

        public FsLocalizationStringDatabase db = LocalizedStringDatabase.Current as FsLocalizationStringDatabase;

        public AddStringWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void GenerateHashButton_Click(object sender, RoutedEventArgs e)
        {
            varHashTextBox.Text = "0x" + ((uint)rand.Next(1 << 30)).ToString("x").PadLeft(8, '0');
        }
        private static Random rand = new Random();
        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                uint hashId;
                string input = varIdTextBox.Text;
                
                // Determine if input is a hash or string ID
                if (string.IsNullOrEmpty(input))
                {
                    // Use hash from hash text box
                    string hashText = Remove0X(varHashTextBox.Text);
                    hashId = Convert.ToUInt32(hashText, 16);
                }
                else
                {
                    // Hash the string ID
                    hashId = LocalizationHelper.HashStringId(input);
                }

                // Set the string value
                db.SetString(hashId, varValueTextBox.Text);

                DialogResult = true;
                Close();
            }
            catch (FormatException)
            {
                App.Logger.Log($"Invalid hash format: {varHashTextBox.Text}");
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Error adding string: {ex.Message}");
            }
        }

        private bool _isUpdating = false;

        private void varHashTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdating)
                return;

            _isUpdating = true;
            try
            {
                // Clear the ID textbox when hash is manually edited
                varIdTextBox.Text = "";

                // Try to display the current value for this hash
                string hashText = Remove0X(varHashTextBox.Text);
                if (uint.TryParse(hashText, System.Globalization.NumberStyles.HexNumber, null, out uint hashId))
                {
                    string currentValue = db.GetString(hashId);
                    varCurrentValueTextBox.Text = currentValue.StartsWith("Invalid StringId:")
                        ? "No matching hash found in localisation database."
                        : currentValue;
                }
                else
                {
                    varCurrentValueTextBox.Text = "Invalid hash input.";
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void varIdTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdating)
                return;

            _isUpdating = true;
            try
            {
                // Update hash textbox when ID is typed
                if (!string.IsNullOrEmpty(varIdTextBox.Text))
                {
                    uint hash = LocalizationHelper.HashStringId(varIdTextBox.Text);
                    varHashTextBox.Text = $"0x{hash:x}";
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private string Remove0X(string hash)
        {
            return hash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) 
                ? hash.Substring(2) 
                : hash;
        }
    }
}
