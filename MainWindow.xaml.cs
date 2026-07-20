using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Barabulka2
{
    public partial class MainWindow : Window
    {
        private Rectangle _whiteSquare = null!;
        private List<Fish> _fishes = new List<Fish>();
        string[] fishImages = { "images/var1.png", "images/var2.png" };
        private readonly Random _rand = new Random();

        private const double DespawnMargin = 250;
        private const double SpawnMargin = 150;

        // шанс психануть
        private const double PizdaChancePerFrame = 0.001;

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
            for (int i = 0; i < 50; i++)
            {
                CreateFish(i);
            }
            CompositionTarget.Rendering += Update;
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
            double w = ActualWidth;
            double h = ActualHeight;

            foreach (var fish in _fishes)
            {
                fish.Move();

                if (fish.IsOutOfBounds(0, 0, w, h, DespawnMargin))
                {
                    var (x, y, vx, vy) = GetRespawnState();
                    fish.Respawn(x, y, vx, vy);
                }
            }

            CheckCollisions();
        }
        private void CheckCollisions()
        {
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

                    a.TryPizda(PizdaChancePerFrame);
                    b.TryPizda(PizdaChancePerFrame);
                }
            }
        }
    }
}