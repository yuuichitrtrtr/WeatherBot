using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json.Serialization;
using System.Linq;
using System.Net;

namespace WeatherBot
{
    public class UserSubscribtion
    {
        public long UserId { get; set; }
        public long ChatId { get; set; }
        public string City { get; set; }
        public string Time { get; set; }
    }
    public class SubscriptionManager
    {
        private const string FilePath = "subscriptions.json";
        private List<UserSubscribtion> _subscriptions;

        public SubscriptionManager()
        {
            Load();
        }

        private void Load()
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                _subscriptions = JsonSerializer.Deserialize<List<UserSubscribtion>>(json) ?? new List<UserSubscribtion>();
                Console.WriteLine($"📋 Загружено {_subscriptions.Count} подписок");
            }
            else
            {
                _subscriptions = new List<UserSubscribtion>();
            }
        }
        private void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_subscriptions, options);
            File.WriteAllText(FilePath, json);
        }
        public void AddSubscription(long userId, long chatId, string city, string time)
        {
            _subscriptions.RemoveAll(s => s.UserId == userId);
            _subscriptions.Add(new UserSubscribtion
            {
                UserId = userId,
                ChatId = chatId,
                City = city,
                Time = time
            });
            Save();
            Console.WriteLine($"✅ Пользователь {userId} подписан на {city} в {time}");
        }
        public void RemoveSubscription(long userId)
        {
            _subscriptions.RemoveAll(s => s.UserId == userId);
            Save();
            Console.WriteLine($"❌ Пользователь {userId} отписан");
        }
        public bool IsSubscribed(long userId)
        {
            return _subscriptions.Any(s => s.UserId == userId);
        }
        public UserSubscribtion GetSubscription(long userId)
        {
            return _subscriptions.FirstOrDefault(s => s.UserId == userId);
        }
        public List<UserSubscribtion> GetAllSubscriptions()
        {
            return _subscriptions.ToList();
        }
    }
        class Bot
        {
        private static void StartSimpleWebServer()
        {
            Task.Run(() =>
            {
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add("http://*:10000/");
                    listener.Start();
                    Console.WriteLine("✅ Веб-сервер запущен на порту 10000");

                    while (true)
                    {
                        var context = listener.GetContext();
                        string responseString = "Weather Bot is running!";
                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

                        context.Response.ContentType = "text/plain";
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка веб-сервера: {ex.Message}");
                }
            });
        }
        private static ReplyKeyboardMarkup _mainKeyboard;
            private static WeatherService _weatherService;
            private static SubscriptionManager _subscriptionManager;
            private static ITelegramBotClient _botClient;
            private static Timer _notificationTimer;
            static async Task Main(string[] args)
            {
                if (File.Exists(".env"))
                {
                    foreach (var line in File.ReadAllLines(".env"))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                            continue;

                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();
                            Environment.SetEnvironmentVariable(key, value);

                        }
                    }
                }

                string botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
                string apikey = Environment.GetEnvironmentVariable("OPENWEATHER_API_KEY");

                if (string.IsNullOrEmpty(botToken))
                {
                    Console.WriteLine("❌ Ошибка: TELEGRAM_BOT_TOKEN не найден в .env файле");
                    Console.WriteLine("Проверьте, что файл .env существует и содержит: TELEGRAM_BOT_TOKEN=ваш_токен");
                    return;
                }

                if (string.IsNullOrEmpty(apikey))
                {
                    Console.WriteLine("❌ Ошибка: OPENWEATHER_API_KEY не найден в .env файле");
                    return;
                }


                _botClient = new TelegramBotClient(botToken);
                _weatherService = new WeatherService(apikey);
                _subscriptionManager = new SubscriptionManager();
                _mainKeyboard = CreateMainKeyboard();
                StartNotificationTimer();

                _botClient.StartReceiving(handlerUpdate, errorHandler);

                Console.WriteLine("Бот запущен...");

            StartSimpleWebServer();

            await Task.Delay(-1);


        }
            private static ReplyKeyboardMarkup CreateMainKeyboard()  
            {
                var locationButton = new KeyboardButton("📍 Отправить геолокацию")
                {
                    RequestLocation = true
                };

                return new ReplyKeyboardMarkup(new[]  
                {
            new KeyboardButton[] { new KeyboardButton("/start"), new KeyboardButton("/help") },
            new KeyboardButton[] { locationButton, new KeyboardButton("/subscribe") },
            new KeyboardButton[] { new KeyboardButton("/mysubscription"), new KeyboardButton("/unsubscribe") },
            new KeyboardButton[] { new KeyboardButton("/info") }
        })
                {
                    ResizeKeyboard = true  
                };
            }
            private static void StartNotificationTimer()
            {
                _notificationTimer = new Timer(CheckAndSendNotifications, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
                Console.WriteLine("⏰ Таймер уведомлений запущен");
            }
            private static async void CheckAndSendNotifications(object state)
            {
            Console.WriteLine($"🔔 [ТАЙМЕР] Сработал в {DateTime.Now:HH:mm:ss}");
            var now = DateTime.Now;
            var moscowTime = now.AddHours(3);
            string currentTimeMoscow = moscowTime.ToString("HH:mm");

            Console.WriteLine($"🔔 [ТАЙМЕР] Текущее время: {currentTimeMoscow}");
            if (_botClient == null)
                {
                    Console.WriteLine("❌ КРИТИЧЕСКАЯ ОШИБКА: _botClient = null после создания!");
                    return;
                }
                foreach (var sub in _subscriptionManager.GetAllSubscriptions())
                {
                Console.WriteLine($"🔔 [ТАЙМЕР] Подписка: {sub.City} в {sub.Time}, пользователь {sub.UserId}");
                if (sub.Time == currentTimeMoscow)
                    {
                        try
                        {
                            Console.WriteLine($"📤 Отправка уведомления для {sub.City} в {currentTimeMoscow}");

                            string weather = await _weatherService.GetWeatherAsync(sub.City);
                            await _botClient.SendMessage(sub.ChatId,
                                $"🌅 Доброе утро!\n\n{weather}\n\n📅 {now:dd.MM.yyyy}");

                            Console.WriteLine($"✅ Уведомление отправлено пользователю {sub.UserId}");
                        }

                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Ошибка отправки: {ex.Message}");
                        Console.WriteLine($"🔔 [ТАЙМЕР] Проверка завершена в {DateTime.Now:HH:mm:ss}");
                    }
                    }
                }
            }
            
            static async Task handlerUpdate(ITelegramBotClient botClient, Update update, CancellationToken token)
            {
                var message = update.Message;
                if (message == null) return;

                if (message.Text == "/start" || message.Text == "📋/start")
                {
                    await botClient.SendMessage(message.Chat.Id, "🌟 *ДОБРО ПОЖАЛОВАТЬ!* 🌟\n\n" +
        "━━━━━━━━━━━━━━━━━━━\n" +
        "☀️ *КАК ПОЛУЧИТЬ ПОГОДУ?*\n" +
        "━━━━━━━━━━━━━━━━━━━\n\n" +
        "📝 *Просто напиши город*\n" +
        "   Например: `Москва`\n\n" +
        "📍 *Или отправь геолокацию*\n" +
        "   (кнопка снизу *геолокация может не отправится если у Телеграма нет полного доступа!*)\n\n" +
        "━━━━━━━━━━━━━━━━━━━\n" +
        "🔔 *ЕЖЕДНЕВНАЯ РАССЫЛКА*\n" +
        "Работает по Московскому времени!\n"+
        "━━━━━━━━━━━━━━━━━━━\n\n" +
        "📅 `/subscribe Москва 09:00`\n" +
        "   Погода будет приходить каждое утро!\n\n" +
        "━━━━━━━━━━━━━━━━━━━\n\n" +
        "💬 `/help` — все команды\n\n" +
        "✨ *Хорошего дня!* ✨");
                }
                else if (message.Text == "/help" || message.Text == "🆘/help")
                {
                string helpText = ("🤖 Доступные команды: \n" +
                      "📋 /start - Запуск бота/возврат\n" +
                      "🆘 /help - Доступные действия\n" +
                      "📍 Отправить геолокацию - Погода по вашему месту\n" +
                      "  /mysubscription - проверить свою подписку на уведомления\n" +
                      "  /subscribe - подписаться на ежедневные уведомления\n" +
                      "  /unsubscribe - отписаться от ежедневной рассылки");

                    var locationButton = new KeyboardButton("📍Отправить геолокацию")
                    {
                        RequestLocation = true
                    };
                    var keyboardWithLocation = new ReplyKeyboardMarkup(new[]
    {
                    new KeyboardButton[] { new KeyboardButton("📋/start"), new KeyboardButton("🆘/help") },
                    new KeyboardButton[] { locationButton, new KeyboardButton("/subscribe") },
                    new KeyboardButton[] {new KeyboardButton("/mysubscription"), new KeyboardButton("/unsubscribe")},
                    new KeyboardButton[] { new KeyboardButton("/info")}
                })
                    {
                        ResizeKeyboard = true
                    };

                    await botClient.SendMessage(message.Chat.Id, helpText, replyMarkup: keyboardWithLocation);
                }
                else if (message.Location != null)
                {
                    double latitude = message.Location.Latitude;
                    double longitude = message.Location.Longitude;

                    string latStr = latitude.ToString().Replace(',', '.');
                    string lonStr = longitude.ToString().Replace(',', '.');

                    await botClient.SendMessage(message.Chat.Id, "📍 Определяю погоду по вашему местоположению...");

                    string weatherInfo = await _weatherService.GetWeatherByCoordsAsync(latStr, lonStr);
                    await botClient.SendMessage(message.Chat.Id, weatherInfo);
                }
                else if (message.Text.StartsWith("/Weather"))
                {
                    string city = message.Text.Replace("/Weather", "").Trim();
                    if (string.IsNullOrEmpty(city))
                    {
                        await botClient.SendMessage(message.Chat.Id, "🌤️ Укажите город. Пример: /Weather Москва");
                        return;
                    }
                    string weatherInfo = await _weatherService.GetWeatherAsync(city);

                    await botClient.SendMessage(message.Chat.Id, weatherInfo);
                }
                else if (!message.Text.StartsWith("/"))
                {
                    string input = message.Text.Trim();

                    if (input.Length < 2)
                    {
                        await botClient.SendMessage(message.Chat.Id, "❌ Название города слишком короткое.\n\n" +
                "Напишите полное название или отправьте геолокацию 📍");
                        return;
                    }
                    if (input.Length > 50)
                    {
                        await botClient.SendMessage(message.Chat.Id, "❌ Слишком длинное название. Попробуйте короче или отправьте геолокацию .");
                        return;
                    }

                    await botClient.SendMessage(message.Chat.Id, $"🔍 Ищу погоду в городе '{input}'...");

                    string weatherinfo = await _weatherService.GetWeatherAsync(input);

                    if (weatherinfo.Contains("не найден"))
                    {
                        await botClient.SendMessage(message.Chat.Id,
                        $"❌ Город '{input}' не найден.\n\n" +
                        "💡 Советы:\n" +
                        "• Проверьте раскладку клавиатуры (русская/английская)\n" +
                        "• Напишите город правильно, например: Москва\n" +
                        "• Или отправьте геолокацию 📍");
                    }
                    else
                    {
                        await botClient.SendMessage(message.Chat.Id, weatherinfo);
                    }
                }
                else if (message.Text.StartsWith("/subscribe"))
                {
                    var parts = message.Text.Split(' ');


                    if (parts.Length < 3)
                    {
                        await botClient.SendMessage(message.Chat.Id,
                            "📅 Формат подписки:\n/subscribe Город Время\n\n" +
                            "Пример: /subscribe Москва 09:00\n\n" +
                            "Время указывайте в 24-часовом формате");
                        return;
                    }

                    string city = parts[1];
                    string time = parts[2];


                    if (!System.Text.RegularExpressions.Regex.IsMatch(time, @"^([0-1][0-9]|2[0-3]):[0-5][0-9]$"))
                    {
                        await botClient.SendMessage(message.Chat.Id,
                            "❌ Неверный формат времени. Используйте ЧЧ:ММ (например, 09:00 или 18:30)");
                        return;
                    }

                    _subscriptionManager.AddSubscription(message.From.Id, message.Chat.Id, city, time);
                    await botClient.SendMessage(message.Chat.Id,
                        $"✅ Вы подписались на уведомления!\n\n" +
                        $"🏙️ Город: {city}\n" +
                        $"⏰ Время: {time}\n\n" +
                        $"Для отписки используйте /unsubscribe");
                }
                else if (message.Text == "/unsubscribe")
                {
                    if (_subscriptionManager.IsSubscribed(message.From.Id))
                    {
                        _subscriptionManager.RemoveSubscription(message.From.Id);
                        await botClient.SendMessage(message.Chat.Id,
                            "✅ Вы отписались от уведомлений о погоде.\n\n" +
                            "Чтобы подписаться снова: /subscribe Москва 09:00");
                    }
                    else
                    {
                        await botClient.SendMessage(message.Chat.Id,
                            "❌ Вы не подписаны на уведомления.\n\n" +
                            "Подписаться: /subscribe Москва 09:00");
                    }
                }
                else if (message.Text == "/mysubscription")
                {
                    var sub = _subscriptionManager.GetSubscription(message.From.Id);
                    if (sub != null)
                    {
                        await botClient.SendMessage(message.Chat.Id,
                            $"📋 Ваша подписка:\n" +
                            $"🏙️ Город: {sub.City}\n" +
                            $"⏰ Время: {sub.Time}\n\n" +
                            $"Изменить: /subscribe {sub.City} НОВОЕ_ВРЕМЯ\n" +
                            $"Отписаться: /unsubscribe");
                    }
                    else
                    {
                        await botClient.SendMessage(message.Chat.Id,
                            "❌ У вас нет активных подписок.\n\n" +
                            "Подписаться: /subscribe Москва 09:00");
                    }
                }
                else if (message.Text == "/info" || message.Text == "/info")
                {
                    await botClient.SendMessage(message.Chat.Id, "Я - бот, присылающий тебе прогноз погоды на сегодня в твоем регионе!");
                }
                else
                {
                    await botClient.SendMessage(message.Chat.Id, "Не неси хуйни");
                }
            }
            static Task errorHandler(ITelegramBotClient botClient, Exception error, CancellationToken token)
            {
                Console.WriteLine($"Ошибка: {error.Message}");
                return Task.CompletedTask;
            }
        }
        public class WeatherService
        {
            private readonly HttpClient _httpClient;
            private readonly string _apiKey;
            public WeatherService(string apiKey)
            {
                _httpClient = new HttpClient();
                _apiKey = apiKey;

            }
            public async Task<string> GetWeatherByCoordsAsync(string latitude, string longitude)
            {
                try
                {
                    string url = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={_apiKey}&units=metric&lang=ru";

                    using HttpClient client = new HttpClient();
                    HttpResponseMessage response = await client.GetAsync(url);
                    string json = await response.Content.ReadAsStringAsync();

                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement root = doc.RootElement;

                    string cityName = root.GetProperty("name").GetString();
                    double temp = root.GetProperty("main").GetProperty("temp").GetDouble();
                    int humidity = root.GetProperty("main").GetProperty("humidity").GetInt32();
                    double feelsLike = root.GetProperty("main").GetProperty("feels_like").GetDouble();
                    string description = root.GetProperty("weather")[0].GetProperty("description").GetString();

                    return $"🌍 Город: {cityName}\n" +
                           $"🌡️ Температура: {Math.Round(temp)}°C\n" +
                           $"🤔 Ощущается как: {Math.Round(feelsLike)}°C\n" +
                           $"💧 Влажность: {humidity}%\n" +
                           $"☁️ {description}";
                }
                catch (Exception ex)
                {
                    return $"❌ Ошибка: {ex.Message}";
                }
            }
            public async Task<string> GetWeatherAsync(string city)
            {
                try
                {
                    string url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={_apiKey}&units=metric&lang=ru";
                    HttpResponseMessage response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorJson = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Ошибка API: {errorJson}");
                        return $"❌ Город '{city}' не найден. Код ошибки: {response.StatusCode}";
                    }

                    string json = await response.Content.ReadAsStringAsync();

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true  
                    };
                    var WeatherData = JsonSerializer.Deserialize<WeatherResponse>(json, options);
                    Console.WriteLine($"weatherData = {(WeatherData == null ? "null" : "не null")}");
                    Console.WriteLine($"weatherData.Main = {(WeatherData?.main == null ? "null" : "не null")}");
                    Console.WriteLine($"weatherData.Name = {(WeatherData?.name == null ? "null" : WeatherData.name)}");
                    return FormatWeatherMessage(WeatherData);
                }
                catch (Exception ex)
                {
                    return $"❌ Ошибка получения погоды: {ex.Message}";
                }
            }
            private string FormatWeatherMessage(WeatherResponse data)
            {
                if (data?.main == null)
                {
                    return "❌ Данные о погоде недоступны";
                }
                string description = (data.weather != null && data.weather.Length > 0)
            ? data.weather[0].description
            : "нет данных";

                return $"🌍 Город: {data.name}\n" +
                       $"🌡️ Температура: {Math.Round(data.main.temp)}°C\n" +
                       $"🤔 Ощущается как: {Math.Round(data.main.feels_like)}°C\n" +
                       $"💧 Влажность: {data.main.humidity}%\n" +
                       $"☁️ {data.weather[0].description}\n";
            }
        }
        public class WeatherResponse
        {
            public MainInfo main { get; set; }
            public WeatherInfo[] weather { get; set; }
            public string name { get; set; }
        }
        public class MainInfo
        {
            public double temp { get; set; }
            public int humidity { get; set; }

            public double feels_like { get; set; }

        }
        public class WeatherInfo
        {
            public string description { get; set; }
        }
    }


