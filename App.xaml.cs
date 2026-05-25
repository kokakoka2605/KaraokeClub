using System.Windows;

namespace KaraokeClub
{
    public partial class App : Application
    {
        private void OnlyLetters_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => char.IsLetter(c) || char.IsWhiteSpace(c));
        }

        private void OnlyNumbers_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => char.IsDigit(c));
        }

        private void OnlyDecimal_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var tb = sender as System.Windows.Controls.TextBox;
            var current = tb?.Text ?? "";
            // Allow digits and one dot/comma
            if (e.Text == "." || e.Text == ",")
            {
                e.Handled = current.Contains('.') || current.Contains(',');
                return;
            }
            e.Handled = !e.Text.All(c => char.IsDigit(c));
        }

        private void Phone_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var tb = (System.Windows.Controls.TextBox)sender;
            var current = tb.Text;

            if (e.Text == "+")
            {
                e.Handled = current.Contains('+') || tb.CaretIndex != 0;
                return;
            }

            e.Handled = !e.Text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c));
        }

        // Block dates within last 16 years (worker must be >= 16)
        private void BirthDatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.DatePicker dp)
            {
                var maxDate = DateTime.Today.AddYears(-16);
                dp.DisplayDateEnd = maxDate;
                dp.BlackoutDates.Clear();
                dp.BlackoutDates.Add(new System.Windows.Controls.CalendarDateRange(
                    maxDate.AddDays(1), DateTime.MaxValue));
            }
        }
    }
}
