using Newtonsoft.Json;

namespace streamdeck_timer
{
    internal class DefaultSound
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }
}
