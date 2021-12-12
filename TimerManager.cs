using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Timers;

namespace streamdeck_timer
{
    internal class TimerManager
    {
        #region Private members
        private static TimerManager instance = null;
        private static readonly object objLock = new object();

        private readonly Timer tmrTimerCounter;
        private Dictionary<string, TimerStatus> dicTimers = new Dictionary<string, TimerStatus>();
        private GlobalSettings global;

        #endregion

        #region Constructors

        public static TimerManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new TimerManager();
                    }
                    return instance;
                }
            }
        }

        public bool IsInitialized { get; private set; }

        private TimerManager()
        {
            IsInitialized = false;
            tmrTimerCounter = new Timer
            {
                Interval = 1000
            };

            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += Instance_OnReceivedGlobalSettings;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
        }

        private void Instance_OnReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload payload)
        {
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                global = payload.Settings.ToObject<GlobalSettings>();
                dicTimers = global.DicTimers;
            }

            if (!tmrTimerCounter.Enabled)
            {
                tmrTimerCounter.Start();
            }
            HandleElapsedTimers();

            IsInitialized = true;
        }

        #endregion

        #region Public Methods

        public void StartTimer(TimerSettings timerSettings)
        {
            if (!dicTimers.ContainsKey(timerSettings.TimerId))
            {
                dicTimers[timerSettings.TimerId] = new TimerStatus
                {
                    EndTime = DateTime.Now + timerSettings.CounterLength,
                };
            }
            else // We were paused, modify time left based on current time
            {
                dicTimers[timerSettings.TimerId].EndTime = DateTime.Now.AddSeconds(dicTimers[timerSettings.TimerId].PausedTimeLeft);
            }

            if (SecondsLeft(timerSettings.TimerId) <= 0)
            {
                ResetTimer(timerSettings);
            }
            dicTimers[timerSettings.TimerId].IsEnabled = true;
            SaveTimers();
        }

        public void StopTimer(string timerId)
        {
            if (dicTimers.ContainsKey(timerId))
            {
                dicTimers[timerId].IsEnabled = false;
                dicTimers[timerId].PausedTimeLeft = Math.Max(SecondsLeft(timerId),0);
                SaveTimers();
            }
        }

        public void ResetTimer(TimerSettings timerSettings)
        {
            if (!dicTimers.ContainsKey(timerSettings.TimerId))
            {
                dicTimers[timerSettings.TimerId] = new TimerStatus();
            }
            dicTimers[timerSettings.TimerId].EndTime = DateTime.Now + timerSettings.CounterLength;
            dicTimers[timerSettings.TimerId].PausedTimeLeft = 0;
            SaveTimers();
        }

        public long GetTimerTime(string timerId)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return 0;
            }

            if (IsTimerEnabled(timerId))
            {
                return SecondsLeft(timerId);
            }
            else
            {
                return dicTimers[timerId].PausedTimeLeft;
            }
        }

        public bool IsTimerEnabled(string timerId)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return false;
            }
            return dicTimers[timerId].IsEnabled;
        }

        public bool IncrementTimer(string timerId, TimeSpan increment)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return false;
            }
            dicTimers[timerId].EndTime += increment;
            SaveTimers();
            return true;
        }

        public DateTime GetTimerEndTime(string timerId)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return DateTime.MinValue;
            }
            return dicTimers[timerId].EndTime;
        }

        #endregion

        #region Private Methods

        private int SecondsLeft(string counterKey)
        {
            if (!dicTimers.ContainsKey(counterKey))
            {
                return -1;
            }

            return (int)(dicTimers[counterKey].EndTime - DateTime.Now).TotalSeconds;
        }

        private void SaveTimers()
        {
            if (global == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"SaveTimers - global is null, creating new object");
                global = new GlobalSettings();
            }
            global.DicTimers = dicTimers;
            GlobalSettingsManager.Instance.SetGlobalSettings(JObject.FromObject(global));
        }

        private void HandleElapsedTimers()
        {
            foreach (string key in dicTimers.Keys)
            {
                if (dicTimers[key].IsEnabled)
                {
                    int secondsLeft = SecondsLeft(key);
                    if (secondsLeft < 0)
                    {
                        dicTimers[key].IsEnabled = false;
                        dicTimers[key].PausedTimeLeft = 0;
                    }
                }
            }
        }

        #endregion
    }
}
