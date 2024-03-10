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
        private int TimeLeft;
        private bool IsPaused;
        private string ImageUrl = "music-icon";

        public void Run()
        {
            client.Initialize();

            mediaManager.OnAnyMediaPropertyChanged += (sender, args) =>
            {
                Title = args.Title;
                Artist = args.Artist;
                Console.WriteLine($"Now listening to {Artist} - {Title}");
                ImageUrl = GetTrackImage(Title, Artist).Result;
                UpdateStatus();
            };

            mediaManager.OnAnyTimelinePropertyChanged += (sender, args) =>
            {
                TimeLeft = Convert.ToInt32((args.EndTime - args.Position).TotalSeconds);
                Console.WriteLine($"{TimeLeft} seconds left || Pause = {IsPaused}");
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

        private void UpdateStatus()
        {
            RichPresence presence;

            if (IsPaused)
            {
                presence = new RichPresence
                {
                    Details = "[Pause] " + Title,
                    State = Artist,
                    Assets = new Assets
                    {
                        LargeImageKey = ImageUrl,
                        LargeImageText = "Music on pause",
                        SmallImageKey = "pause",
                        SmallImageText = "Pause"
                    }
                };
            }
            else
            {
                presence = new RichPresence
                {
                    Details = Title,
                    State = Artist,
                    Timestamps = Timestamps.FromTimeSpan(TimeLeft),
                    Assets = new Assets
                    {
                        LargeImageKey = ImageUrl,
                        LargeImageText = "Listening to music"
                    }
                };
            }

            client.SetPresence(presence);
        }

        private async Task<string> GetTrackImage(string track, string artist)
        {
            string query = $"{track} - {artist}";
            string type = "all";
            int page = 0;
            bool nocorrect = false;

            string url = $"https://api.music.yandex.net/search?type={type}&text={query}&page={page}&nocorrect={nocorrect}";

            var request = WebRequest.Create(url);

            using (var response = await request.GetResponseAsync())
            {
                using (var stream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        JObject parsedJson = JObject.Parse(await reader.ReadToEndAsync());

                        JToken coverUriToken = parsedJson["result"]["best"]["result"]["coverUri"];
                        string coverUri = coverUriToken?.ToString().Replace("%%", "400x400");

                        if (coverUri == null)
                            return "music-icon";

                        return "https://" + coverUri;
                    }
                }
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
