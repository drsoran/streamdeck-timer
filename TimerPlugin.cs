using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace streamdeck_timer
{
    [PluginActionId("com.drsoran.timerplugin")]
    public partial class TimerPlugin : PluginBase
    {
        private const int RESET_COUNTER_KEYPRESS_LENGTH_MS = 600;

        private const int TOTAL_ALERT_STAGES = 2;

        private PluginSettings Settings { get; }

        private string TimerId { get; }

        private TimeSpan TimerInterval { get; set; }

        private Timer TmrAlert { get; } = new Timer();

        private long HighestTimerSeconds { get; set; }

        private int AlertStage { get; set; }

        private bool IsAlerting { get; set; }

        private bool _KeyPressed { get; set; }

        private DateTime KeyPressStart { get; set; }

        public TimerPlugin(ISDConnection connection, InitialPayload payload) 
            : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                Settings = PluginSettings.CreateDefaultSettings();
                Connection.SetSettingsAsync(JObject.FromObject(Settings));
            }
            else
            {
                Settings = payload.Settings.ToObject<PluginSettings>();
            }

            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            TimerId = Connection.ContextId;
            TmrAlert.Interval = 200;
            TmrAlert.Elapsed += TmrAlert_Elapsed;

            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            TmrAlert.Elapsed -= TmrAlert_Elapsed;
            TmrAlert.Stop();
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            // Used for long press
            KeyPressStart = DateTime.Now;
            _KeyPressed = true;

            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            if (TimerManager.Instance.IsTimerEnabled(TimerId))
            {
                await ResetAlert();
            }
            else
            {
                ResumeTimer();
            }
        }

        public override void KeyReleased(KeyPayload payload)
        {
            _KeyPressed = false;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Released");
        }

        public async override void OnTick()
        {
            long total;

            // Stream Deck calls this function every second,
            // so this is the best place to determine if we need to reset (versus the internal timer which may be paused)
            CheckIfResetNeeded();

            if (IsAlerting)
            {
                await ResetAlert();
                ResumeTimer();
                return;
            }

            // Handle alerting
            total = TimerManager.Instance.GetTimerTime(TimerId);
            if (total <= 0 && !TimerManager.Instance.IsTimerEnabled(TimerId)) // Time passed before 
            {
                total = (int)TimerInterval.TotalSeconds;
            }
            else if (total <= 0 && !TmrAlert.Enabled) // Timer running, need to alert
            {
                total = 0;
                IsAlerting = true;
                TmrAlert.Start();
                PlaySoundOnEnd();
            }

            await ShowTimeOnKey(total);
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);

            InitializeSettings();
            SaveSettings();
        }

        private void Connection_OnPropertyInspectorDidAppear(object sender, SDEventReceivedEventArgs<PropertyInspectorDidAppear> e)
        {
            PropagatePlaybackDevices();
            PropagateResourceSounds();
        }

        private void Connection_OnSendToPlugin(object sender, SDEventReceivedEventArgs<SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            Logger.Instance.LogMessage(TracingLevel.INFO, "OnSendToPlugin called");
            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "loadsavepicker":
                        string propertyName = (string)payload["property_name"];
                        string pickerTitle = (string)payload["picker_title"];
                        string pickerFilter = (string)payload["picker_filter"];
                        string fileName = PickersUtil.Pickers.SaveFilePicker(pickerTitle, null, pickerFilter);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            if (!PickersUtil.Pickers.SetJsonPropertyValue(Settings, propertyName, fileName))
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, "Failed to save picker value to settings");
                            }
                            SaveSettings();
                        }
                        break;
                }
            }
        }

        private void TmrAlert_Elapsed(object sender, ElapsedEventArgs e)
        {
            Bitmap img = Tools.GenerateGenericKeyImage(out Graphics graphics);
            int height = img.Height;
            int width = img.Width;

            // Background
            var bgBrush = new SolidBrush(GenerateStageColor(Settings.AlertColor, AlertStage, TOTAL_ALERT_STAGES));
            graphics.FillRectangle(bgBrush, 0, 0, width, height);
            Connection.SetImageAsync(img);

            AlertStage = (AlertStage + 1) % TOTAL_ALERT_STAGES;
            graphics.Dispose();
        }

        private void InitializeSettings()
        {
            Task.Run(() =>
            {
                int retries = 60;
                while (!TimerManager.Instance.IsInitialized && retries > 0)
                {
                    retries--;
                    System.Threading.Thread.Sleep(1000);
                }

                SetTimerInterval();
            });
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        private async Task ShowTimeOnKey(long total)
        {
            long minutes, seconds, hours;
            string delimiter = ":";
            minutes = total / 60;
            seconds = total % 60;
            hours = minutes / 60;
            minutes %= 60;

            string hoursStr = (hours > 0) ? $"{hours:0}{delimiter}" : "";
            string secondsDelimiter = delimiter;
            if (!string.IsNullOrEmpty(hoursStr))
            {
                secondsDelimiter = "\n";
            }

            await Connection.SetTitleAsync($"{hoursStr}{minutes:00}{secondsDelimiter}{seconds:00}");
        }

        private void SetTimerInterval()
        {
            TimerInterval = TimeSpan.Zero;

            if (!string.IsNullOrEmpty(Settings.TimerInterval))
            {
                if (!TimeSpan.TryParse(Settings.TimerInterval, out var timerInterval))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Repeat Amount: {Settings.TimerInterval}");
                    Settings.TimerInterval = PluginSettings.DEFAULT_TIMER_INTERVAL;
                    SaveSettings();
                }
                else
                {
                    TimerInterval = timerInterval;
                    HighestTimerSeconds = (long)TimerInterval.TotalSeconds;

                    if (!TimerManager.Instance.IsTimerEnabled(TimerId))
                    {
                        ResetTimer();
                    }
                }
            }
        }

        private void ResetTimer()
        {
            TimerManager.Instance.ResetTimer(new TimerSettings()
            {
                TimerId = TimerId,
                CounterLength = TimerInterval,
            });

            HighestTimerSeconds = (long) TimerInterval.TotalSeconds;
        }

        private void ResumeTimer()
        {
            TimerManager.Instance.StartTimer(new TimerSettings()
            {
                TimerId = TimerId,
                CounterLength = TimerInterval
            });
        }

        private void PauseTimer()
        {
            TimerManager.Instance.StopTimer(TimerId);
        }

        private void CheckIfResetNeeded()
        {
            if (!_KeyPressed)
            {
                return;
            }

            if ((DateTime.Now - KeyPressStart).TotalMilliseconds >= RESET_COUNTER_KEYPRESS_LENGTH_MS)
            {
                PauseTimer();
                SetTimerInterval();
            }
        }

        private void PropagatePlaybackDevices()
        {
            try
            {
                Settings.PlaybackDevices = AudioUtils.Common.GetAllPlaybackDevices(true)
                    .Select(d => new PlaybackDevice() { ProductName = d })
                    .OrderBy(p => p.ProductName).ToList();

                SaveSettings();
            }
            catch (Exception ex)
            {
                Settings.PlaybackDevices = new List<PlaybackDevice>();
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error propagating playback devices {ex}");
            }
        }

        private void PropagateResourceSounds()
        {
            Settings.DefaultSounds = GetType().Assembly
               .GetManifestResourceNames()
               .Where(n => n.EndsWith("wav"))
               .Select(s => new DefaultSound { Name = s.Split("Sounds.").Last() })
               .ToList();

            Settings.DefaultSounds.Insert(0, new DefaultSound { Name = PluginSettings.NO_DEFAULT_SOUND });

            SaveSettings();
        }

        private void PlaySoundOnEnd()
        {
            Task.Run(async () =>
            {
                if (string.IsNullOrEmpty(Settings.PlaybackDevice))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"PlaySoundOnEnd called but File or Playback device are empty. File: {Settings.PlaySoundOnEndFile} Device: {Settings.PlaybackDevice}");
                    return;
                }

                if (!string.IsNullOrEmpty(Settings.DefaultSound) && Settings.DefaultSound != PluginSettings.NO_DEFAULT_SOUND)
                {
                    await PlayResourceSound(Settings.DefaultSound);
                    return;
                }

                if (!string.IsNullOrEmpty(Settings.PlaySoundOnEndFile))
                {
                    if (!File.Exists(Settings.PlaySoundOnEndFile))
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"PlaySoundOnEnd called but file does not exist: {Settings.PlaySoundOnEndFile}");
                        await PlayResourceSound(PluginSettings.DEFAULT_SOUND);
                        return;
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"PlaySoundOnEnd called. Playing {Settings.PlaySoundOnEndFile} on device: {Settings.PlaybackDevice}");
                        await AudioUtils.Common.PlaySound(Settings.PlaySoundOnEndFile, Settings.PlaybackDevice);
                    }
                }
            });
        }

        private async Task PlayResourceSound(string soundName)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"PlayResourceSound called. Playing {soundName} on device: {Settings.PlaybackDevice}");

            string soundRes = $"streamdeck_timer.Sounds.{soundName}";
            using var stream = GetType().Assembly.GetManifestResourceStream(soundRes);
            if (stream != null)
            {
                var memStream = new MemoryStream();
                stream.CopyTo(memStream);

                await AudioUtils.Common.PlayStream(memStream, Settings.PlaybackDevice);
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"No resource found {soundRes}");
            }
        }

        private Color GenerateStageColor(string initialColor, int stage, int totalAmountOfStages)
        {
            Color color = ColorTranslator.FromHtml(initialColor);
            int a = color.A;
            double r = color.R;
            double g = color.G;
            double b = color.B;

            // Try and increase the color in the last stage;
            if (stage == totalAmountOfStages - 1)
            {
                stage = 1;
            }

            for (int idx = 0; idx < stage; idx++)
            {
                r /= 2;
                g /= 2;
                b /= 2;
            }

            return Color.FromArgb(a, (int)r, (int)g, (int)b);
        }

        private async Task ResetAlert()
        {
            IsAlerting = false;
            TmrAlert.Stop();
            ResetTimer();
            await Connection.SetImageAsync((string) null);
        }
    }
}
