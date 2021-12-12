using BarRaider.SdTools;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace streamdeck_timer
{
    internal class PluginSettings
    {
        public const string DEFAULT_TIMER_INTERVAL = "00:00:05";

        public const string DEFAULT_SOUND = "bounce.wav";

        public const string NO_DEFAULT_SOUND = "<use sound file>";

        public static PluginSettings CreateDefaultSettings()
        {
            PluginSettings instance = new PluginSettings
            {
                TimerInterval = DEFAULT_TIMER_INTERVAL,
                AlertColor = "#FF0000",
                PlaybackDevice = string.Empty,
                PlaybackDevices = null,
                PlaySoundOnEndFile = DEFAULT_SOUND,
            };

            return instance;
        }

        [JsonProperty(PropertyName = "timerInterval")]
        public string TimerInterval { get; set; }

        [JsonProperty(PropertyName = "alertColor")]
        public string AlertColor { get; set; }

        [JsonProperty(PropertyName = "playbackDevices")]
        public List<PlaybackDevice> PlaybackDevices { get; set; }

        [JsonProperty(PropertyName = "playbackDevice")]
        public string PlaybackDevice { get; set; }

        [FilenameProperty]
        [JsonProperty(PropertyName = "playSoundOnEndFile")]
        public string PlaySoundOnEndFile { get; set; }

        [JsonProperty(PropertyName = "defaultSounds")]
        public List<DefaultSound> DefaultSounds { get; set; }

        [JsonProperty(PropertyName = "defaultSound")]
        public string DefaultSound { get; set; }
    }
}
