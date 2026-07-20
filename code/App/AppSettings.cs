using System;
using System.IO;
using System.Text.Json;

namespace Barabulka2
{
    /// <summary>
    /// Целевой FPS обновления рыб. Значение = количество кадров в секунду,
    /// Unlimited = 0 => без ограничения (как было раньше, привязано к рендеру композитора).
    /// </summary>
    public enum FishFpsMode
    {
        Unlimited = 0,
        Fps30 = 30,
        Fps60 = 60,
        Fps144 = 144
    }

    /// <summary>
    /// Настройки приложения. Хранятся в %AppData%\Barabulka2\settings.json
    /// </summary>
    public class AppSettings
    {
        /// <summary>Прозрачность рыб, 0.1 (почти невидимы) .. 1.0 (непрозрачные)</summary>
        public double FishOpacity { get; set; } = 1.0;

        /// <summary>true = окно кликов не перехватывает, всё уходит "сквозь" на рабочий стол</summary>
        public bool ClickThrough { get; set; } = true;

        /// <summary>Целевой FPS движения рыб. По умолчанию - без ограничения (как раньше).</summary>
        public FishFpsMode FpsMode { get; set; } = FishFpsMode.Unlimited;

        /// <summary>Количество рыб на экране.</summary>
        public int FishCount { get; set; } = 50;

        /// <summary>Множитель скорости рыб. 1.0 = как сейчас по умолчанию. Случайный разброс скорости у каждой рыбы сохраняется всегда.</summary>
        public double FishSpeedMultiplier { get; set; } = 1.0;

        private static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "Barabulka2", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch
            {
                // Файл настроек битый/недоступен - просто едем на дефолтах, не роняем приложение
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath)!;
                Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // Не критично: просто настройки не переживут перезапуск
            }
        }
    }
}
