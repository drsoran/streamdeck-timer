using System;

namespace streamdeck_timer
{
    public class TimerStatus
    {
        public DateTime EndTime { get; set; }

        public bool IsEnabled { get; set; }

        public int PausedTimeLeft { get; set; }

        public TimerStatus()
        {
            EndTime = DateTime.Now;
            IsEnabled = false;
            PausedTimeLeft = 0;
        }
    }
}
