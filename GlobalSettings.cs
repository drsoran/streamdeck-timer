using Newtonsoft.Json;
using System.Collections.Generic;

namespace streamdeck_timer
{
    public class GlobalSettings
    {
        [JsonProperty(PropertyName = "timers")]
        public Dictionary<string, TimerStatus> DicTimers { get; set; }
    }
}
