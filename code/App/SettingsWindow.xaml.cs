using System.Windows;
using System.Windows.Controls;

namespace Barabulka2
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _main;
        private bool _initialized;

        public SettingsWindow(MainWindow main, AppSettings current)
        {
            InitializeComponent();
            _main = main;

            OpacitySlider.Value = current.FishOpacity;
            ClickThroughCheckBox.IsChecked = current.ClickThrough;
            OpacityLabel.Text = $"{current.FishOpacity * 100:F0}%";

            FpsComboBox.SelectedIndex = current.FpsMode switch
            {
                FishFpsMode.Fps30 => 0,
                FishFpsMode.Fps60 => 1,
                FishFpsMode.Fps144 => 2,
                _ => 3 // Unlimited
            };

            FishCountSlider.Value = current.FishCount;
            FishCountLabel.Text = current.FishCount.ToString();

            SpeedSlider.Value = current.FishSpeedMultiplier;
            SpeedLabel.Text = $"{current.FishSpeedMultiplier * 100:F0}%";

            _initialized = true; // чтобы обработчики ниже не сработали раньше времени при инициализации контролов
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_initialized) return;
            OpacityLabel.Text = $"{e.NewValue * 100:F0}%";
            _main.ApplyOpacity(e.NewValue);
        }

        private void FpsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            var mode = FpsComboBox.SelectedIndex switch
            {
                0 => FishFpsMode.Fps30,
                1 => FishFpsMode.Fps60,
                2 => FishFpsMode.Fps144,
                _ => FishFpsMode.Unlimited
            };
            _main.ApplyFpsMode(mode);
        }

        private void FishCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_initialized) return;
            int count = (int)e.NewValue;
            FishCountLabel.Text = count.ToString();
            _main.ApplyFishCount(count);
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_initialized) return;
            SpeedLabel.Text = $"{e.NewValue * 100:F0}%";
            _main.ApplySpeedMultiplier(e.NewValue);
        }

        private void ClickThroughCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            _main.ApplyClickThrough(ClickThroughCheckBox.IsChecked == true);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _main.SaveCurrentSettings();
            Close();
        }
    }
}
