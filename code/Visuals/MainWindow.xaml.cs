using System.Runtime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Brushes = System.Windows.Media.Brushes;
using Image = System.Windows.Controls.Image;
using Rectangle = System.Windows.Shapes.Rectangle;
namespace Barabulka2
{
    public partial class MainWindow : Window
    {
        private List<Fish> _fishes = new List<Fish>();
        string[] fishImages = { "images/var1.png", "images/var2.png" };
        private readonly Random _rand = new Random();

        private const double DespawnMargin = 250;
        private const double SpawnMargin = 150;

        // шанс психануть
        private const double PizdaChancePerFrame = 0.001;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = (HwndSource)PresentationSource.FromVisual(this);
            source?.AddHook(WndProc);
        }

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
            {
                handled = true; // блокируем сворачивание от "Показать рабочий стол" / Win+D
            }
            return IntPtr.Zero;
        }

        public MainWindow()
        {
            InitializeComponent();

            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = true;

            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            _fishes = new List<Fish>();
            for (int i = 0; i < _settings.FishCount; i++)
            {
                CreateFish(i);
            }

            // Точка отсчёта для dtScale - чтобы первый тик не словил огромный dtScale
            _lastUpdateMs = _clock.Elapsed.TotalMilliseconds;
            CompositionTarget.Rendering += Update;

            // Применяем сохранённые настройки (хэндл окна к этому моменту уже создан)
            ApplyOpacity(_settings.FishOpacity);
            ApplyClickThrough(_settings.ClickThrough);


            SetupTrayIcon();
        }

        private AppSettings _settings = AppSettings.Load();
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private SettingsWindow? _settingsWindow;

        // --- Ограничение FPS обновления рыб (не влияет на реальную скорость - см. dtScale в Fish.Move) ---
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private double _lastUpdateMs;

        /// <summary>Иконка в трее - единственный способ открыть настройки, т.к. у окна нет рамки/кнопок.</summary>
        private void SetupTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = GetAppIcon(),
                Visible = true,
                Text = "Barabulka2"
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Настройки", null, (s, e) => OpenSettings());
            menu.Items.Add("Выход", null, (s, e) => System.Windows.Application.Current.Shutdown());
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => OpenSettings();
        }

        /// <summary>
        /// Достаёт иконку из самого exe (та, что прописана в csproj как ApplicationIcon).
        /// Работает и в Debug, и в Release - в отличие от SystemIcons.Application, которая
        /// всегда возвращает системную заглушку и не имеет отношения к иконке проекта.
        /// </summary>
        private static System.Drawing.Icon GetAppIcon()
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                                  ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null) return icon;
            }
            catch
            {
                // не критично, ниже фолбэк на системную иконку
            }
            return System.Drawing.SystemIcons.Application;
        }

        private void OpenSettings()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }
            _settingsWindow = new SettingsWindow(this, _settings);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        /// <summary>Применить прозрачность ко всем рыбам и запомнить в настройках.</summary>
        public void ApplyOpacity(double opacity)
        {
            _settings.FishOpacity = opacity;
            foreach (var fish in _fishes)
                fish.Sprite.Opacity = opacity;
        }

        /// <summary>Включить/выключить прохождение кликов сквозь окно (и, соответственно, сквозь рыб).</summary>
        public void ApplyClickThrough(bool clickThrough)
        {
            _settings.ClickThrough = clickThrough;
            ClickThroughHelper.SetClickThrough(this, clickThrough);
        }

        /// <summary>Сменить лимит FPS обновления рыб. Скорости рыб это не касается - см. dtScale в Fish.Move.</summary>
        public void ApplyFpsMode(FishFpsMode mode)
        {
            _settings.FpsMode = mode;
            // Сбрасываем точку отсчёта, чтобы не словить один "скачок" dtScale сразу после смены режима
            _lastUpdateMs = _clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>Применить множитель скорости ко всем текущим рыбам (и запомнить для новых).</summary>
        public void ApplySpeedMultiplier(double multiplier)
        {
            _settings.FishSpeedMultiplier = multiplier;
            foreach (var fish in _fishes)
                fish.SpeedMultiplier = multiplier;
        }

        /// <summary>Изменить количество рыб на экране "на лету" - добавить или убрать лишних.</summary>
        public void ApplyFishCount(int count)
        {
            count = Math.Max(1, count);
            _settings.FishCount = count;

            if (count > _fishes.Count)
            {
                int startIndex = _fishes.Count;
                for (int i = startIndex; i < count; i++)
                    CreateFish(i);
            }
            else if (count < _fishes.Count)
            {
                int toRemove = _fishes.Count - count;
                for (int i = 0; i < toRemove; i++)
                {
                    var last = _fishes[_fishes.Count - 1];
                    MyCanvas.Children.Remove(last.Sprite);
                    _fishes.RemoveAt(_fishes.Count - 1);
                }
            }
        }

        public void SaveCurrentSettings() => _settings.Save();

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            _settings.Save();
            base.OnClosed(e);
        }

        private void CreateFish(int index)
        {
            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fishImages[index % fishImages.Length]);

            var image = new Image
            {
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(fullPath, UriKind.Absolute))
            };
            RenderOptions.SetCachingHint(image, CachingHint.Cache);

            int idx = index;
            image.ImageFailed += (s, ex) =>
                System.Diagnostics.Debug.WriteLine($"Image {idx} FAILED: {ex.ErrorException?.Message}");

            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);

            double fishSize = _rand.Next(200, 300);
            double startX = _rand.Next(0, Math.Max(1, (int)ActualWidth));
            double startY = _rand.Next(0, Math.Max(1, (int)ActualHeight));
            double angle = _rand.NextDouble() * Math.PI * 2;
            double speed = 1.5 + _rand.NextDouble() * 2.5;

            var fish = new Fish(
                startX: startX,
                startY: startY,
                speedX: Math.Cos(angle) * speed,
                speedY: Math.Sin(angle) * speed,
                size: fishSize,
                sprite: image
            );

            image.Opacity = _settings.FishOpacity;
            fish.SpeedMultiplier = _settings.FishSpeedMultiplier;

            _fishes.Add(fish);
            MyCanvas.Children.Add(image);
        }

        private (double x, double y, double vx, double vy) GetRespawnState()
        {
            double w = ActualWidth;
            double h = ActualHeight;

            int side = _rand.Next(4);
            double x, y;

            switch (side)
            {
                case 0: // сверху
                    x = _rand.NextDouble() * w;
                    y = -SpawnMargin;
                    break;
                case 1: // справа
                    x = w + SpawnMargin;
                    y = _rand.NextDouble() * h;
                    break;
                case 2: // снизу
                    x = _rand.NextDouble() * w;
                    y = h + SpawnMargin;
                    break;
                default: // слева
                    x = -SpawnMargin;
                    y = _rand.NextDouble() * h;
                    break;
            }

            // случайная точка внутри экрана, к которой рыба дэшнется
            double targetX = _rand.NextDouble() * w;
            double targetY = _rand.NextDouble() * h;
            double dx = targetX - x;
            double dy = targetY - y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) len = 1;

            double speed = 1.5 + _rand.NextDouble() * 2.5;
            double vx = dx / len * speed;
            double vy = dy / len * speed;

            return (x, y, vx, vy);
        }

        private void Update(object? sender, EventArgs e)
        {
            double nowMs = _clock.Elapsed.TotalMilliseconds;
            double elapsedMs = nowMs - _lastUpdateMs;

            int fpsCap = (int)_settings.FpsMode;
            if (fpsCap > 0)
            {
                double targetIntervalMs = 1000.0 / fpsCap;
                if (elapsedMs < targetIntervalMs) return; // ждём следующий тик рендера
            }

            _lastUpdateMs = nowMs;

            // Все константы движения тюнились из расчёта ~60 обновлений в секунду.
            // dtScale переводит реально прошедшее время в "эталонные" 60fps-такты,
            // чтобы скорость рыб не зависела от выбранного лимита FPS.
            double dtScale = elapsedMs / (1000.0 / 60.0);
            dtScale = Math.Clamp(dtScale, 0.0, 3.0); // защита от рывка после сворачивания/паузы окна

            double w = ActualWidth;
            double h = ActualHeight;

            foreach (var fish in _fishes)
            {
                fish.Move(dtScale);

                if (fish.IsOutOfBounds(0, 0, w, h, DespawnMargin))
                {
                    var (x, y, vx, vy) = GetRespawnState();
                    fish.Respawn(x, y, vx, vy);
                }
            }

            CheckCollisions(dtScale);
        }
        private void CheckCollisions(double dtScale)
        {
            double chance = PizdaChancePerFrame * dtScale;

            for (int i = 0; i < _fishes.Count; i++)
            {
                var a = _fishes[i];
                if (a.IsFlipped) continue;

                for (int j = i + 1; j < _fishes.Count; j++)
                {
                    var b = _fishes[j];
                    if (b.IsFlipped) continue;

                    double dx = a.PosX - b.PosX;
                    double dy = a.PosY - b.PosY;
                    double touchDist = (a.FishSize + b.FishSize) * 0.35;

                    // Быстрый предварительный отсев без sqrt
                    double distSq = dx * dx + dy * dy;
                    if (distSq > touchDist * touchDist) continue;

                    a.TryPizda(chance);
                    b.TryPizda(chance);
                }
            }
        }
    }
}