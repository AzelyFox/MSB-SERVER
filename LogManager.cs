using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MSB_SERVER
{
    public class LogManager
    {
		private static LogManager INSTANCE;

		private App serverApplication;

		private TextBox mainSystemLogBox;
		private TextBox mainSystemErrorLogBox;
		private TextBox mainNetworkLogBox;
		private TextBox mainNetworkErrorLogBox;
		private TextBox mainUserLogBox;
		private TextBox mainRoomLogBox;

        public bool SAVE_DEBUG_LOGS = false;

		public enum LOG_LEVEL
		{
			LOG_DEBUG, LOG_NORMAL, LOG_CRITICAL
		}

		public enum LOG_TARGET
		{
			LOG_SYSTEM, LOG_NETWORK
		}

		public struct LogObject
		{
			public LOG_LEVEL logLevel;
			public LOG_TARGET logTarget;
			public string prefix;
			public string message;
			public string datetime;
		}

		private LinkedList<LogObject> logList;

        public bool SCROLL_TO_END = true;

		private LogManager()
		{
			serverApplication = (App) Application.Current;
			if (logList == null)
			{
				logList = new LinkedList<LogObject>();
			}
		}

		public static LogManager GetInstance()
		{
			if (INSTANCE == null) INSTANCE = new LogManager();
			return INSTANCE;
		}

		public void SetSystemLogger(TextBox logBox, TextBox errorBox)
		{
			mainSystemLogBox = logBox;
			mainSystemErrorLogBox = errorBox;
		}

		public void SetNetworkLogger(TextBox logBox, TextBox errorBox)
		{
			mainNetworkLogBox = logBox;
			mainNetworkErrorLogBox = errorBox;
		}

		public void SetUserLogger(TextBox textBox)
		{
			mainUserLogBox = textBox;
		}

		public void SetRoomLogger(TextBox textBox)
		{
			mainRoomLogBox = textBox;
		}

		public void StartLogger()
		{
			serverApplication.graphicalManager.OnLogModuleStatusChanged(true, true);
		}

		public void StopLogger()
		{
			serverApplication.graphicalManager.OnLogModuleStatusChanged(false, false);
		}

		public void ClearLog(int clearMode = 0)
		{
			if (logList == null)
			{
				return;
			}
            if (clearMode == 0)
            {
                logList.Clear();
                mainSystemLogBox.Clear();
                mainNetworkLogBox.Clear();
            }

            int index = 0;

            if (clearMode == 1)
            {
                while (index < logList.Count())
                {
                    if (logList.ElementAt(index).logTarget == LOG_TARGET.LOG_SYSTEM)
                    {
                        logList.Remove(logList.ElementAt(index));
                    } else
                    {
                        index++;
                    }
                }
                mainSystemLogBox.Clear();
            }

            if (clearMode == 2)
            {
                while (index < logList.Count())
                {
                    if (logList.ElementAt(index).logTarget == LOG_TARGET.LOG_NETWORK)
                    {
                        logList.Remove(logList.ElementAt(index));
                    }
                    else
                    {
                        index++;
                    }
                }
                mainNetworkLogBox.Clear();
            }
        }

		public void NewLog(LOG_LEVEL logLevel, LOG_TARGET logTarget, string prefix, string message)
		{
            if (!SAVE_DEBUG_LOGS && logLevel == LOG_LEVEL.LOG_DEBUG)
            {
                return;
            }
			if (mainSystemLogBox == null || mainNetworkLogBox == null || mainUserLogBox == null || mainRoomLogBox == null)
			{
				return;
			}
			LogObject logObject = new LogObject();
			logObject.logLevel = logLevel;
			logObject.logTarget = logTarget;
			logObject.prefix = prefix;
			logObject.message = message;
			logObject.datetime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff");
			logList.AddLast(logObject);
            serverApplication.Dispatcher?.Invoke(() => {
	            SyncLogBox();
            });
		}

		public void SyncLogBox()
		{
			if (mainSystemLogBox == null || mainNetworkLogBox == null || mainUserLogBox == null || mainRoomLogBox == null)
			{
				return;
			}
			if (logList == null || logList.Count == 0)
			{
				return;
			}
			string printString = string.Empty;

			LogObject log = logList.Last();

			switch (log.logLevel)
			{
				case LOG_LEVEL.LOG_DEBUG:
					printString += "[D]";
					break;
				case LOG_LEVEL.LOG_NORMAL:
					printString += "[N]";
					break;
				case LOG_LEVEL.LOG_CRITICAL:
					printString += "[C]";
					break;
				default:
					printString += "[?]";
					break;
			}

			printString += " " + log.prefix + " : ";
			printString += log.datetime;
			printString += "\n";
			printString += log.message;
			printString += "\n";

			switch (log.logTarget)
			{
				case LOG_TARGET.LOG_SYSTEM:
					mainSystemLogBox.AppendText(printString);
					if (SCROLL_TO_END) mainSystemLogBox.ScrollToEnd();
					if (log.logLevel == LOG_LEVEL.LOG_CRITICAL)
					{
						mainSystemErrorLogBox.AppendText(printString);
						if (SCROLL_TO_END) mainSystemErrorLogBox.ScrollToEnd();
					}
					break;
				case LOG_TARGET.LOG_NETWORK:
					mainNetworkLogBox.AppendText(printString);
                    if (SCROLL_TO_END) mainNetworkLogBox.ScrollToEnd();
					if (log.logLevel == LOG_LEVEL.LOG_CRITICAL)
					{
						mainNetworkErrorLogBox.AppendText(printString);
						if (SCROLL_TO_END) mainSystemErrorLogBox.ScrollToEnd();
					}
					break;
				default:
					mainSystemLogBox.AppendText(printString);
                    if (SCROLL_TO_END) mainSystemLogBox.ScrollToEnd();
					mainNetworkLogBox.AppendText(printString);
                    if (SCROLL_TO_END) mainNetworkLogBox.ScrollToEnd();
					if (log.logLevel == LOG_LEVEL.LOG_CRITICAL)
					{
						mainSystemErrorLogBox.AppendText(printString);
						if (SCROLL_TO_END) mainSystemErrorLogBox.ScrollToEnd();
						mainNetworkErrorLogBox.AppendText(printString);
						if (SCROLL_TO_END) mainSystemErrorLogBox.ScrollToEnd();
					}
					break;
			}
			
		}
    }
}
