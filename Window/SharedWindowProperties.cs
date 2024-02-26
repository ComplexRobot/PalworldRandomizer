using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace PalworldRandomizer
{
    internal static partial class SharedWindow
    {
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

        [GeneratedRegex("^(?!0)[0-9]{0,2}$")]
        private static partial Regex positiveIntSize2Regex();
        public static void PositiveIntSize2_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox_PreviewTextInput(positiveIntSize2Regex(), sender, e);
        }

        public static void PositiveIntSize2_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            TextBox_Pasting(positiveIntSize2Regex(), sender, e);
        }

        [GeneratedRegex("^(?!0)[0-9]{0,3}$")]
        private static partial Regex positiveIntSize3Regex();
        public static void PositiveIntSize3_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox_PreviewTextInput(positiveIntSize3Regex(), sender, e);
        }

        public static void PositiveIntSize3_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            TextBox_Pasting(positiveIntSize3Regex(), sender, e);
        }

        [GeneratedRegex("^(?:0|(?!0)[0-9]{0,4})$")]
        private static partial Regex nonNegIntSize4Regex();
        public static void NonNegIntSize4_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox_PreviewTextInput(nonNegIntSize4Regex(), sender, e);
        }

        public static void NonNegIntSize4_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            TextBox_Pasting(nonNegIntSize4Regex(), sender, e);
        }

        [GeneratedRegex("^(?:0|(?!0)[0-9]{0,9})$")]
        private static partial Regex nonNegIntSize9Regex();
        public static void NonNegIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox_PreviewTextInput(nonNegIntSize9Regex(), sender, e);
        }

        public static void NonNegIntSize9_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            TextBox_Pasting(nonNegIntSize9Regex(), sender, e);
        }

        [GeneratedRegex("^(?!0)[0-9]{0,9}$")]
        private static partial Regex positiveIntSize9Regex();
        public static void PositiveIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox_PreviewTextInput(positiveIntSize9Regex(), sender, e);
        }

        public static void PositiveIntSize9_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            TextBox_Pasting(positiveIntSize3Regex(), sender, e);
        }
    }
}
