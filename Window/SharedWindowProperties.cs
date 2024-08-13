using Newtonsoft.Json;
using System.Data;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

namespace PalworldRandomizer
{
    public class ConfigData
    {
        public float AssetVersion = 0;
        public bool AutoReplaceOldFiles = true;
        public bool AutoRestoreTemplate = true;
        public bool OutputLog = false;
        public bool AutoSaveRestoreBackups = true;
        public bool AutoSaveGenerationData = true;
    }

    internal static partial class SharedWindow
    {
        public const string GLOBAL_GONFIG_FILENAME = @"Config\GlobalConfig.json";

        public static ConfigData GetConfig()
        {
            string configFilePath = UAssetData.AppDataPath(GLOBAL_GONFIG_FILENAME);
            if (File.Exists(configFilePath))
            {
                ConfigData? config = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(configFilePath));
                if (config != null)
                {
                    return config;
                }
            }
            return new();
        }

        public static void SaveConfig(ConfigData config)
        {
            try
            {
                Directory.CreateDirectory(UAssetData.AppDataPath("Config"));
                File.WriteAllText(UAssetData.AppDataPath(GLOBAL_GONFIG_FILENAME), JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            catch
            {
            }
        }

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, [In] int[] attrValue, int attrSize);
        [LibraryImport("user32.dll")]
        private static partial int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static void EnableDarkMode(Window window)
        {
            IntPtr windowHandle = new WindowInteropHelper(window).Handle;
            if (DwmSetWindowAttribute(windowHandle, 19, [1], 4) != 0)
            {
                DwmSetWindowAttribute(windowHandle, 20, [1], 4);
            }
        }

        public static void TextBox_PreviewTextInput(Regex regex, object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = (TextBox) sender;
            if (e.Text == "\r")
            {
                Keyboard.ClearFocus();
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
            e.Handled = !regex.IsMatch(textBox.Text[..textBox.SelectionStart] + e.Text + textBox.Text[(textBox.SelectionStart + textBox.SelectionLength)..]);
        }

        public static void TextBox_Pasting(Regex regex, object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }
            string paste = (string) e.DataObject.GetData(typeof(string));
            TextBox textBox = (TextBox) sender;
            if (!regex.IsMatch(textBox.Text[..textBox.SelectionStart] + paste + textBox.Text[(textBox.SelectionStart + textBox.SelectionLength)..]))
                e.CancelCommand();
        }

        public static void PreNotifyInput(object sender, NotifyInputEventArgs e)
        {
            if (e.StagingItem.Input is KeyEventArgs keyEventArgs && keyEventArgs.Key == Key.Space && keyEventArgs.RoutedEvent.Name == "KeyDown"
                && Keyboard.FocusedElement is TextBox textBox)
            {
                TextCompositionEventArgs eventArgs = new(keyEventArgs.Device, new(e.InputManager, textBox, " "))
                {
                    RoutedEvent = TextCompositionManager.PreviewTextInputEvent
                };
                textBox.RaiseEvent(eventArgs);
                keyEventArgs.Handled = eventArgs.Handled;
            }
        }

        public static void PostNotifyInput(object sender, NotifyInputEventArgs e)
        {
            if (e.StagingItem.Input is KeyEventArgs keyEventArgs && keyEventArgs.RoutedEvent.Name == "KeyDown" && Keyboard.FocusedElement is TextBox textBox)
            {
                TextCompositionEventArgs eventArgs = new(keyEventArgs.Device, new(e.InputManager, textBox, ""))
                {
                    RoutedEvent = TextCompositionManager.PreviewTextInputEvent
                };
                textBox.RaiseEvent(eventArgs);
                if (eventArgs.Handled)
                {
                    textBox.Text = "";
                }
            }
        }

        [GeneratedRegex("^(?!0)[0-9]{0,2}$")]
        private static partial Regex positiveIntSize2Regex();
        public static void PositiveIntSize2_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(positiveIntSize2Regex(), sender, e);
        public static void PositiveIntSize2_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(positiveIntSize2Regex(), sender, e);

        [GeneratedRegex("^(?!0)[0-9]{0,3}$")]
        private static partial Regex positiveIntSize3Regex();
        public static void PositiveIntSize3_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(positiveIntSize3Regex(), sender, e);
        public static void PositiveIntSize3_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(positiveIntSize3Regex(), sender, e);

        [GeneratedRegex("^(?!0)[0-9]{0,4}$")]
        private static partial Regex positiveIntSize4Regex();
        public static void PositiveIntSize4_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(positiveIntSize4Regex(), sender, e);
        public static void PositiveIntSize4_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(positiveIntSize4Regex(), sender, e);

        [GeneratedRegex("^(?!0)[0-9]{0,9}$")]
        private static partial Regex positiveIntSize9Regex();
        public static void PositiveIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(positiveIntSize9Regex(), sender, e);
        public static void PositiveIntSize9_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(positiveIntSize3Regex(), sender, e);

        [GeneratedRegex("^(?:0|(?!0)[0-9]{0,2})$")]
        private static partial Regex nonNegIntSize2Regex();
        public static void NonNegIntSize2_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(nonNegIntSize2Regex(), sender, e);
        public static void NonNegIntSize2_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(nonNegIntSize2Regex(), sender, e);

        [GeneratedRegex("^(?:0|(?!0)[0-9]{0,3})$")]
        private static partial Regex nonNegIntSize3Regex();
        public static void NonNegIntSize3_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(nonNegIntSize3Regex(), sender, e);
        public static void NonNegIntSize3_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(nonNegIntSize3Regex(), sender, e);

        [GeneratedRegex("^(?:0|(?!0)[0-9]{0,4})$")]
        private static partial Regex nonNegIntSize4Regex();
        public static void NonNegIntSize4_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(nonNegIntSize4Regex(), sender, e);
        public static void NonNegIntSize4_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(nonNegIntSize4Regex(), sender, e);

        [GeneratedRegex("^(?:0|(?!0)[0-9]{0,5})$")]
        private static partial Regex nonNegIntSize5Regex();
        public static void NonNegIntSize5_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(nonNegIntSize5Regex(), sender, e);
        public static void NonNegIntSize5_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(nonNegIntSize5Regex(), sender, e);

        [GeneratedRegex("^(?:0|(?!0)[0-9]{0,9})$")]
        private static partial Regex nonNegIntSize9Regex();
        public static void NonNegIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(nonNegIntSize9Regex(), sender, e);
        public static void NonNegIntSize9_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(nonNegIntSize9Regex(), sender, e);

        [GeneratedRegex(@"^(0|(?!0)\d{0,5}|(?=.{0,5}[^.]?$)(0\.\d*|(?![0.])\d*\.\d*))$", RegexOptions.ExplicitCapture)]
        private static partial Regex NonNegDecSize5Regex();
        public static void NonNegDecSize5_PreviewTextInput(object sender, TextCompositionEventArgs e) => TextBox_PreviewTextInput(NonNegDecSize5Regex(), sender, e);
        public static void NonNegDecSize5_Pasting(object sender, DataObjectPastingEventArgs e) => TextBox_Pasting(NonNegDecSize5Regex(), sender, e);
    }

    public class MathConverter : IMultiValueConverter, IValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            static bool IsNumber(object value) => value is sbyte || value is byte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal;
            string stringToParse = (string) parameter;
            for (int i = 0; i < values.Length; ++i)
            {
                string stringValue;
                if (IsNumber(values[i]))
                {
                    stringValue = $"{values[i]}";
                }
                else if (values[i] == null || values[i] == DependencyProperty.UnsetValue)
                {
                    stringValue = "0";
                }
                else
                {
                    stringValue = "1";
                }
                stringToParse = new Regex($"(?:\\[{i}\\]|\\{{{i}\\}})").Replace(stringToParse, stringValue);
            }
            return new DataTable().Compute(stringToParse, null);
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Convert([value], targetType, parameter, culture);
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
