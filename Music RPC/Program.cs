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
        private bool IsPaused;
        private string ImageUrl = "music-icon";
        private string TrackUrl;

        // Основной код
        public void Run()
        {
            client.Initialize();

            mediaManager.OnAnyMediaPropertyChanged += (sender, args) =>
            {
                Title = args.Title.Length > 64 ? args.Title.Substring(0, 61) + "..." : args.Title;
                Artist = args.Artist.Length > 64 ? args.Artist.Substring(0, 61) + "..." : args.Artist;
                IsPaused = false;
                Console.WriteLine($"Now listening to {Artist} - {Title}");
                ImageUrl = GetTrackImage(Title, Artist).Result;
                UpdateStatus();
            };

            mediaManager.OnAnyTimelinePropertyChanged += (sender, args) =>
            {
                Position = Convert.ToInt32(args.Position.TotalSeconds);
                Console.WriteLine($"{(int)args.Position.Minutes:D2}:{(int)args.Position.Seconds:D2} passed || Pause = {IsPaused}");
                UpdateStatus();
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

        // Обновление статуса в Discord
        private void UpdateStatus()
        {
            var presence = new RichPresence
            {
                Details = IsPaused ? "[Pause] " + Title : Title,
                State = Artist,
                Assets = new Assets
                {
                    LargeImageKey = ImageUrl,
                    LargeImageText = IsPaused ? "Music on pause" : "Listening to music",
                    SmallImageKey = IsPaused ? "pause" : null,
                    SmallImageText = IsPaused ? "Pause" : null
                },
                Timestamps = IsPaused ? new Timestamps(DateTime.UtcNow.AddSeconds(-Position), DateTime.UtcNow) : new Timestamps(DateTime.UtcNow.AddSeconds(-Position)),

                Buttons = new[]
                {
                    new Button { Label = "Track", Url = TrackUrl },
                    //new Button { Label = "App", Url = "https://github.com/KOTOKOPOLb/Music-RPC" }
                }
            };

            client.SetPresence(presence);
        }

        // Получения ссылки на картинку трека
        private async Task<string> GetTrackImage(string track, string artist)
        {
            string query = $"{track} - {artist}";
            string type = "track";
            int page = 0;
            bool nocorrect = false;

            string url = $"https://api.music.yandex.net/search?type={type}&text={query}&page={page}&nocorrect={nocorrect}";

            var request = WebRequest.Create(url);
            using (var response = await request.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                JObject parsedJson = JObject.Parse(await reader.ReadToEndAsync());
                JToken coverUriToken = parsedJson["result"]["tracks"]["results"][0]["coverUri"];
                string coverUri = coverUriToken?.ToString().Replace("%%", "400x400");

                //Get Track URL
                JToken trackIdToken = parsedJson["result"]["tracks"]["results"][0]["id"];
                JToken albumIdToken = parsedJson["result"]["tracks"]["results"][0]["albums"][0]["id"];
                TrackUrl = $"https://music.yandex.ru/album/{albumIdToken}/track/{trackIdToken}";

                return coverUri != null ? "https://" + coverUri : "music-icon";
            }
        }

        static void Main()
        {
            Console.Title = "Music Rpc";
            Console.Write("Music Rpc ", Console.ForegroundColor = ConsoleColor.Red);
            Console.WriteLine("By KOTOKOPOLb", Console.ForegroundColor = ConsoleColor.Cyan);
            Console.WriteLine("", Console.ForegroundColor = ConsoleColor.White);

            Program program = new Program();
            program.Run();
        }
    }
}
