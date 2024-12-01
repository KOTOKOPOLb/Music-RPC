using DiscordRPC;
using Newtonsoft.Json.Linq;
using System.Net;
using WindowsMediaController;
using Button = DiscordRPC.Button;
using Timer = System.Windows.Forms.Timer;

namespace Music_RPC_GUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            mediaManager.OnAnyMediaPropertyChanged += MediaManager_OnAnyMediaPropertyChanged;
            mediaManager.OnAnyTimelinePropertyChanged += MediaManager_OnAnyTimelinePropertyChanged;
            mediaManager.OnAnyPlaybackStateChanged += MediaManager_OnAnyPlaybackStateChanged;
            trackTimer = new Timer { Interval = 100 };
            trackTimer.Tick += TrackTimer_Tick;
        }

        private readonly DiscordRpcClient client = new DiscordRpcClient("1215567146176741376");
        private readonly MediaManager mediaManager = new MediaManager();
        //private MediaManager.MediaSession currentSession;
        private string Title;
        private string Artist;
        private int Position;
        private int Duration;
        private bool IsPaused;
        private string ImageUrl = "music-icon";
        private string TrackUrl;
        private  Timer trackTimer;
        private DateTime playbackStartTime;
        private bool isTimerRunning;

        private void MediaManager_OnAnyMediaPropertyChanged(MediaManager.MediaSession mediaSession, Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties args)
        {
            //if (mediaSession.Id != currentSession.Id) return;

            string tempTitle = args.Title;
            string tempArtist = args.Artist;

            if (Title != tempTitle || Artist != tempArtist)
            {
                Title = tempTitle;
                Artist = tempArtist;
                IsPaused = false;

                ImageUrl = GetTrackImageAndUrl(Title, Artist).Result;
                UpdateStatus();
            }
        }

        private void MediaManager_OnAnyTimelinePropertyChanged(MediaManager.MediaSession mediaSession, Windows.Media.Control.GlobalSystemMediaTransportControlsSessionTimelineProperties args)
        {
            //if (mediaSession.Id != currentSession.Id) return;

            int tempPosition = Convert.ToInt32(args.Position.TotalSeconds);
            int tempDuration = Convert.ToInt32(args.EndTime.TotalSeconds);

            if (Position != tempPosition || Duration != tempDuration)
            {
                Position = tempPosition;
                Duration = tempDuration;
                playbackStartTime = DateTime.UtcNow.AddSeconds(-Position);
            }
        }

        private void MediaManager_OnAnyPlaybackStateChanged(MediaManager.MediaSession mediaSession, Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackInfo args)
        {
            //if (mediaSession.Id != currentSession.Id) return;

            IsPaused = args.PlaybackRate == 0;
            if (IsPaused)
            {
                trackTimer.Stop();
                isTimerRunning = false;
            }
            else
            {
                playbackStartTime = DateTime.UtcNow.AddSeconds(-Position);
                isTimerRunning = true;
                trackTimer.Start();
            }
            UpdateStatus();
        }

        private void UpdateActiveApplicationsList()
        {
            if (mediaManager.CurrentMediaSessions.Count > 0)
            {
                var activeApps = mediaManager.CurrentMediaSessions
                    .Select(session => session.Value.ControlSession.SourceAppUserModelId)
                    .ToList();

                listBox1.Items.Clear();
                foreach (var app in activeApps)
                    listBox1.Items.Add(app);

                if (listBox1.SelectedItem == null && listBox1.Items.Count > 0)
                    listBox1.SelectedIndex = 0;
            }
            else
            {
                listBox1.Items.Clear();
                listBox1.Items.Add("No active media sessions found!");
            }
        }

        private string Truncate(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Length > 50 ? input.Substring(0, 50) + "..." : input;
        }

        private void UpdateStatus()
        {
            toolStripStatusLabel1.Text = $"Last Update: {DateTime.Now.Hour}:{DateTime.Now.Minute:D2}:{DateTime.Now.Second:D2}";
            UpdateActiveApplicationsList();
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
                    new Button { Label = "App [NOT THIS BETA]", Url = "https://github.com/KOTOKOPOLb/Music-RPC" }
                }
            };

            client.SetPresence(presence);

            label1.Text = Title;
            label2.Text = Artist;

            if (!string.IsNullOrEmpty(ImageUrl) && ImageUrl != "music-icon")
            {
                using (var httpClient = new HttpClient())
                {
                    var imageBytes = httpClient.GetByteArrayAsync(ImageUrl).Result;
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        pictureBox1.BackgroundImage = Image.FromStream(ms);
                    }
                }
            }

            if (!IsPaused)
            {
                playbackStartTime = DateTime.UtcNow.AddSeconds(-Position);
                isTimerRunning = true;
                trackTimer.Start();
            }
            else
            {
                isTimerRunning = false;
                trackTimer.Stop();
            }
        }

        private void TrackTimer_Tick(object sender, EventArgs e)
        {
            if (isTimerRunning)
            {
                int currentSeconds = (int)(DateTime.UtcNow - playbackStartTime).TotalSeconds;
                if (currentSeconds > Duration)
                {
                    trackTimer.Stop();
                    isTimerRunning = false;
                }
                else
                {
                    Position = currentSeconds;
                    label3.Text = $"{TimeSpan.FromSeconds(Position):mm\\:ss} / {TimeSpan.FromSeconds(Duration):mm\\:ss}";
                }
            }
        }

        private async Task<string> GetTrackImageAndUrl(string track, string artist)
        {
            string query = $"{track} - {artist}";
            string type = "track";
            int page = 0;
            bool nocorrect = false;

            string url = $"https://api.music.yandex.net/search?type={type}&text={query}&page={page}&nocorrect={nocorrect}";

            var request = WebRequest.Create(url);
            using var response = await request.GetResponseAsync().ConfigureAwait(false);
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            JObject parsedJson = JObject.Parse(await reader.ReadToEndAsync());
            JToken coverUriToken = parsedJson["result"]["tracks"]["results"][0]["coverUri"];
            string coverUri = coverUriToken?.ToString().Replace("%%", "400x400");

            //Get Track URL
            JToken trackIdToken = parsedJson["result"]["tracks"]["results"][0]["id"];
            JToken albumIdToken = parsedJson["result"]["tracks"]["results"][0]["albums"][0]["id"];
            TrackUrl = $"https://music.yandex.ru/album/{albumIdToken}/track/{trackIdToken}";

            return coverUri != null ? "https://" + coverUri : "music-icon";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            mediaManager.Start();
            //client.Initialize();
            InitializeTrayIcon();
            notifyIcon1.Visible = true;
            UpdateActiveApplicationsList();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            mediaManager.Dispose();
            //client.Dispose();
        }

        private void Form1_Deactivate(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                ShowInTaskbar = false;
                notifyIcon1.Visible = true;
            }
        }

        private ContextMenuStrip trayContextMenu;
        private void InitializeTrayIcon()
        {
            if (trayContextMenu == null)
            {
                trayContextMenu = new ContextMenuStrip();
                trayContextMenu.Items.Add("Показать", null, ShowApplication);
                trayContextMenu.Items.Add("Обновить", null, UpdateApplication);
                trayContextMenu.Items.Add("Закрыть", null, ExitApplication);
            }

            notifyIcon1.Icon = SystemIcons.Application;
            notifyIcon1.ContextMenuStrip = trayContextMenu;
            notifyIcon1.Visible = true;
        }

        private void ShowApplication(object? sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void UpdateApplication(object? sender, EventArgs e)
        {
            mediaManager.ForceUpdate();
            UpdateActiveApplicationsList();
            UpdateStatus();
        }

        private void ExitApplication(object? sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Application.Exit();
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) ShowApplication(sender, e);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //string selectedApp = listBox1.SelectedItem?.ToString();
            //if (string.IsNullOrEmpty(selectedApp) || selectedApp == "No active media sessions found!") return;

            //currentSession = mediaManager.CurrentMediaSessions.Values
            //    .FirstOrDefault(session => session.ControlSession.SourceAppUserModelId == selectedApp);

            //if (currentSession != null)
            //{
            //    //currentSession.ControlSession.TryUpdateTimelineProperties();
            //    //currentSession.ControlSession.TryUpdateMediaProperties();
            //}
        }
    }
}
