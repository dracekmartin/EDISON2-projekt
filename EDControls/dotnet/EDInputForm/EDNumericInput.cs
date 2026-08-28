using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EDControls.EDInputForm
{
    /// <summary>
    /// Attached property omezující vstup TextBoxu na numerické znaky.
    /// <c>Mode="Integer"</c> = jen číslice, <c>"Decimal"</c> = číslice + jeden desetinný oddělovač.
    /// </summary>
    public static class EDNumericInput
    {
        public static readonly DependencyProperty ModeProperty =
            DependencyProperty.RegisterAttached(
                "Mode", typeof(string), typeof(EDNumericInput),
                new PropertyMetadata(null, OnModeChanged));

        public static string GetMode(DependencyObject obj) => (string)obj.GetValue(ModeProperty);
        public static void SetMode(DependencyObject obj, string value) => obj.SetValue(ModeProperty, value);

        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox tb) return;
            tb.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(tb, OnPaste);
            if (e.NewValue is string)
            {
                tb.PreviewTextInput += OnPreviewTextInput;
                DataObject.AddPastingHandler(tb, OnPaste);
            }
        }

        private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb) return;
            string mode = GetMode(tb);
            foreach (char c in e.Text)
            {
                if (char.IsDigit(c)) continue;
                if (c == '-' && tb.CaretIndex == 0 && !tb.Text.Contains('-')) continue;
                if (mode == "Decimal" && (c == '.' || c == ',') && !tb.Text.Contains('.') && !tb.Text.Contains(',')) continue;
                e.Handled = true;
                return;
            }
        }

        private static void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string))!;
                if (sender is TextBox tb && !IsValid(text, GetMode(tb)))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        internal static bool IsValid(string text, string mode)
        {
            bool hasSep = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsDigit(c)) continue;
                if (c == '-' && i == 0) continue;
                if (mode == "Decimal" && (c == '.' || c == ',') && !hasSep) { hasSep = true; continue; }
                return false;
            }
            return true;
        }
    }
}
