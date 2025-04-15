using DiscordRPC;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using WindowsMediaController;

namespace Music_RPC
{
    internal class Program
    {
        private readonly DiscordRpcClient client = new DiscordRpcClient("1215567146176741376");
        private readonly MediaManager mediaManager = new MediaManager();
        private string Title;
        private string Artist;
        private int Position;
        private int Duration;
        private bool IsPaused;
        private string ImageUrl = "music-icon";
        private string TrackUrl;
        private const string ReplaceSymbol = "#";

        // Основной код
        private void Run()
        {
            client.Initialize();

            mediaManager.OnAnyMediaPropertyChanged += async (sender, args) =>
            {
                string tempTitle = args.Title;
                string tempArtist = args.Artist;

                if (Title != tempTitle || Artist != tempArtist)
                {
                    Title = tempTitle;
                    Artist = tempArtist;
                    IsPaused = false;

                    Console.WriteLine($"Now listening to {Artist} - {Title}"); 
                    ImageUrl = GetTrackImageAndUrl(Title, Artist).Result;
                    UpdateStatus();
                }
            };

            mediaManager.OnAnyTimelinePropertyChanged += (sender, args) =>
            {
                int tempPosition = Convert.ToInt32(args.Position.TotalSeconds);
                int tempDuration = Convert.ToInt32(args.EndTime.TotalSeconds);

                if (Position != tempPosition || Duration != tempDuration)
                {
                    Position = tempPosition;
                    Duration = tempDuration;
                    Console.WriteLine($"{(int)args.Position.Minutes:D2}:{(int)args.Position.Seconds:D2} passed || Pause = {IsPaused}");
                    UpdateStatus();
                }
            };

            mediaManager.OnAnyPlaybackStateChanged += (sender, args) =>
            {
                IsPaused = args.PlaybackRate == 0;
                UpdateStatus();
            };

            client.OnReady += (sender, e) => Console.WriteLine($"Received Ready from user {e.User.Username}");
            client.OnPresenceUpdate += (sender, e) => Console.WriteLine("Received Update!");

            mediaManager.Start();
            Console.ReadLine();

            mediaManager.Dispose();
            client.Dispose();
        }

        // Обрезание текста
        private string Truncate(string input) => input.Length > 64 ? input.Substring(0, 61) + "..." : input;

        // Обновление статуса в Discord
        private void UpdateStatus()
        {
            var presence = new RichPresence
            {
                Details = IsPaused ? "[Pause] " + Truncate(Title) : Truncate(Title),
                State = Truncate(Artist),
                Assets = new Assets
                {
                    LargeImageKey = ImageUrl,
                    LargeImageText = IsPaused ? "Music on pause" : null,
                    SmallImageKey = IsPaused ? "pause" : null,
                    SmallImageText = IsPaused ? "Pause" : null
                },
                Timestamps = IsPaused ? new Timestamps(DateTime.UtcNow.AddSeconds(-Position), DateTime.UtcNow) : new Timestamps(DateTime.UtcNow.AddSeconds(-Position), DateTime.UtcNow.AddSeconds(Duration - Position)),
                Type = ActivityType.Listening,
                Buttons = new[]
                {
                    new Button { Label = "Track", Url = TrackUrl ?? $"https://google.com/search?q={Title}+{Artist}" },
                    new Button { Label = "App", Url = "https://github.com/KOTOKOPOLb/Music-RPC" }
                }
            };

            client.SetPresence(presence);
            Console.WriteLine("Update sent!");
        }

        // Получения ссылки на трек и его картинку
        private async Task<string> GetTrackImageAndUrl(string track, string artist)
        {
            string query = $"{track.Replace(ReplaceSymbol, "")} - {artist.Replace(ReplaceSymbol, "")}";
            string type = "track";
            int page = 0;
            bool nocorrect = false;

            string url = $"https://api.music.yandex.net/search?type={type}&text={query}&page={page}&nocorrect={nocorrect}";

            try
            {
                var request = WebRequest.Create(url);
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    JObject parsedJson = JObject.Parse(await reader.ReadToEndAsync());
                    JToken coverUriToken = parsedJson["result"]["tracks"]["results"][0]["coverUri"];
                    string coverUri = coverUriToken?.ToString().Replace("%%", "400x400");

                    JToken trackIdToken = parsedJson["result"]["tracks"]["results"][0]["id"];
                    JToken albumIdToken = parsedJson["result"]["tracks"]["results"][0]["albums"][0]["id"];
                    TrackUrl = $"https://music.yandex.ru/album/{albumIdToken}/track/{trackIdToken}";

                    return coverUri != null ? "https://" + coverUri : "music-icon";
                }
            }
            catch (WebException ex)
            {
                string errorMessage;
                if (ex.Response is HttpWebResponse errorResponse)
                {
                    int statusCode = (int)errorResponse.StatusCode;
                    errorMessage = $"HTTP Error {statusCode}: {errorResponse.StatusDescription}";
                }
                else
                    errorMessage = ex.Message;

                Console.WriteLine(errorMessage);
                return "music-icon";
            }
        }

        static void Main()
        {
            Console.Title = "Music RPC";
            Console.Write("Music Rpc ", Console.ForegroundColor = ConsoleColor.Red);
            Console.WriteLine("By KOTOKOPOLb", Console.ForegroundColor = ConsoleColor.Cyan);
            Console.WriteLine("", Console.ForegroundColor = ConsoleColor.White);

            Program program = new Program();
            program.Run();
        }
    }
}
