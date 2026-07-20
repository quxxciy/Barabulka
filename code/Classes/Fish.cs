using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Point = System.Windows.Point;
namespace Barabulka2
{
    public class Fish
    {
        private double _posX; // центр рыбы по X
        private double _posY; // центр рыбы по Y
        private double _speedX;
        private double _speedY;

        // --- Блуждание курса ---
        private double _targetAngle;
        private double _currentAngle;
        private double _wanderTimer;

        // --- "Дыхание" скорости - имитация цикла взмахов хвоста (глайд-разгон) ---
        private double _tailPhase;
        private readonly double _tailFrequency; // у каждой рыбы своя, для разнообразия

        // --- Редкие рывки вперёд ---
        private double _burstBoost; // текущий множитель ускорения, затухает сам
        private const double BurstChancePerFrame = 0.0025; // ~раз в 6-7 сек в среднем при 60fps
        private const double BurstDecay = 0.93;

        // --- Боковое покачивание (чисто визуальное, не влияет на реальную траекторию) ---
        private double _wagPhase;
        private readonly double _wagFrequency;
        private readonly double _wagAmplitudeDeg;

        // --- Механика "перевернуло и брыкается" (реакция на столкновение) ---
        private bool _isFlipped;
        private double _flipTimer;      // сколько кадров ещё будет брыкаться
        private double _flipCooldown;   // кулдаун перед тем, как может перевернуться снова
        private double _thrashPhase;

        private static readonly Random _rand = new Random();

        public System.Windows.Controls.Image Sprite;
        private readonly ScaleTransform _scale;
        private readonly RotateTransform _rotate;
        private readonly TranslateTransform _translate;

        public double FishSize { get; private set; }
        public bool IsFlipped => _isFlipped;

        /// <summary>Глобальный множитель скорости (настройка). Базовая случайная скорость рыбы (см. конструктор) не трогается - только итоговое перемещение.</summary>
        public double SpeedMultiplier { get; set; } = 1.0;

        public double PosX => _posX;
        public double PosY => _posY;

        public Fish(double startX, double startY, double speedX, double speedY, double size, System.Windows.Controls.Image sprite)
        {
            _posX = startX;
            _posY = startY;
            _speedX = speedX;
            _speedY = speedY;
            FishSize = size;
            Sprite = sprite;

            Sprite.Width = FishSize;
            Sprite.Height = FishSize;

            _scale = new ScaleTransform(1, 1);
            _rotate = new RotateTransform(0);
            _translate = new TranslateTransform();

            var group = new TransformGroup();
            group.Children.Add(_scale);
            group.Children.Add(_rotate);
            group.Children.Add(_translate);

            Sprite.RenderTransform = group;
            Sprite.RenderTransformOrigin = new Point(0.5, 0.5);

            _currentAngle = AngleOf(_speedX, _speedY);
            _targetAngle = _currentAngle;

            // Небольшой случайный разброс по каждой рыбе, чтобы все не двигались синхронно
            _tailFrequency = 0.12 + _rand.NextDouble() * 0.10;
            _wagFrequency = 0.15 + _rand.NextDouble() * 0.10;
            _wagAmplitudeDeg = 4 + _rand.NextDouble() * 5;
            _tailPhase = _rand.NextDouble() * Math.PI * 2;
            _wagPhase = _rand.NextDouble() * Math.PI * 2;
        }

        /// <param name="dtScale">
        /// Масштаб прошедшего времени относительно эталонных 60 fps (1.0 = один "нормальный" тик).
        /// Все константы движения изначально тюнились из расчёта ~60 вызовов Move() в секунду,
        /// поэтому при ограничении FPS (когда Update() вызывается реже) масштаб растёт,
        /// чтобы итоговая скорость рыб в реальном времени не менялась.
        /// </param>
        public void Move(double dtScale = 1.0)
        {
            if (_isFlipped)
            {
                MoveFlipped(dtScale);
                return;
            }

            if (_flipCooldown > 0) _flipCooldown -= dtScale;

            // Смена курса раз в случайный интервал
            _wanderTimer -= dtScale;
            if (_wanderTimer <= 0)
            {
                double baseAngle = AngleOf(_speedX, _speedY);
                _targetAngle = baseAngle + (_rand.NextDouble() * 70 - 35);
                _wanderTimer = _rand.Next(60, 180);
            }

            double diff = NormalizeAngle(_targetAngle - _currentAngle);
            _currentAngle = NormalizeAngle(_currentAngle + diff * 0.02 * dtScale);

            // Базовая скорость + "дыхание" (взмахи хвоста) + редкие рывки
            double baseSpeed = Math.Sqrt(_speedX * _speedX + _speedY * _speedY);
            _tailPhase += _tailFrequency * dtScale;
            double tailPulse = 1.0 + Math.Sin(_tailPhase) * 0.18; // ±18% от базовой скорости

            if (_burstBoost > 0.02)
                _burstBoost *= Math.Pow(BurstDecay, dtScale);
            else if (_rand.NextDouble() < BurstChancePerFrame * dtScale)
                _burstBoost = 1.2 + _rand.NextDouble() * 1.3; // рывок х2.2-2.5 в моменте

            double speed = baseSpeed * tailPulse * (1.0 + _burstBoost) * SpeedMultiplier * dtScale;

            double rad = _currentAngle * Math.PI / 180.0;
            _speedX = Math.Cos(rad) * baseSpeed; // курс/база хранится без пульсации, чтобы не "накапливалась"
            _speedY = Math.Sin(rad) * baseSpeed;

            _posX += Math.Cos(rad) * speed;
            _posY += Math.Sin(rad) * speed;

            ApplyVisualTransform(rad, dtScale);

            _translate.X = _posX - FishSize / 2;
            _translate.Y = _posY - FishSize / 2;
        }

        private void MoveFlipped(double dtScale)
        {
            _flipTimer -= dtScale;
            _thrashPhase += 0.9 * dtScale;

            // резко дёргается на месте почти без поступательного движения - как будто и правда брыкается
            double jitterX = Math.Sin(_thrashPhase * 1.7) * 1.5;
            double jitterY = Math.Cos(_thrashPhase * 1.3) * 1.5;
            _posX += (jitterX + _speedX * 0.15) * dtScale;
            _posY += (jitterY + _speedY * 0.15) * dtScale;

            bool facingLeft = _speedX < 0;
            _scale.ScaleX = facingLeft ? -1 : 1;
            _scale.ScaleY = -1; // кверху брюхом

            double baseAngle = facingLeft ? -_currentAngle + 180 : _currentAngle;
            double thrash = Math.Sin(_thrashPhase) * 20; // дрожание/брыкание
            _rotate.Angle = NormalizeAngle(baseAngle + thrash);

            _translate.X = _posX - FishSize / 2;
            _translate.Y = _posY - FishSize / 2;

            if (_flipTimer <= 0)
            {
                _isFlipped = false;
                _scale.ScaleY = 1;
                _flipCooldown = 180; // ~3 сек нельзя переворачиваться снова
                // задаём новый спокойный курс, чтобы рыба не продолжала брыкливую траекторию
                _currentAngle = AngleOf(_speedX, _speedY);
                _targetAngle = _currentAngle;
                _wanderTimer = 30;
            }
        }

        private void ApplyVisualTransform(double rad, double dtScale)
        {
            bool facingLeft = _speedX < 0;
            _scale.ScaleX = facingLeft ? -1 : 1;
            _scale.ScaleY = 1;

            double headingDeg = _currentAngle;
            double visualAngle = facingLeft ? -headingDeg + 180 : headingDeg;

            _wagPhase += _wagFrequency * dtScale;
            double wag = Math.Sin(_wagPhase) * _wagAmplitudeDeg * (facingLeft ? -1 : 1);

            _rotate.Angle = NormalizeAngle(visualAngle + wag);
        }

        private static double AngleOf(double x, double y) => Math.Atan2(y, x) * 180.0 / Math.PI;

        private static double NormalizeAngle(double angle)
        {
            while (angle > 180) angle -= 360;
            while (angle < -180) angle += 360;
            return angle;
        }

        public bool IsOutOfBounds(double minX, double minY, double maxX, double maxY, double margin)
        {
            return _posX < minX - margin || _posX > maxX + margin ||
                   _posY < minY - margin || _posY > maxY + margin;
        }

        public void Respawn(double startX, double startY, double speedX, double speedY)
        {
            _posX = startX;
            _posY = startY;
            _speedX = speedX;
            _speedY = speedY;
            _currentAngle = AngleOf(_speedX, _speedY);
            _targetAngle = _currentAngle;
            _wanderTimer = 0;
            _isFlipped = false;
            _flipCooldown = 0;
            _burstBoost = 0;
            _scale.ScaleY = 1;
        }
        public void TryPizda(double chance)
        {
            if (_isFlipped || _flipCooldown > 0) return;
            if (_rand.NextDouble() < chance)
            {
                _isFlipped = true;
                _flipTimer = _rand.Next(45, 100); // ~0.75-1.7 сек при 60fps
            }
        }
    }
}