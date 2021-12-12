using Newtonsoft.Json;

namespace streamdeck_timer
{
    internal class PlaybackDevice
    {
        [JsonProperty(PropertyName = "name")]
        public string ProductName { get; set; }
    }
}
