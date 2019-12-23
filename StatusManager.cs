using System;
using System.Text;
using System.Windows;
using System.Threading;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace MSB_SERVER
{
    public class StatusManager
    {
		private static StatusManager INSTANCE = new StatusManager();

		private readonly App serverApplication;

		private Thread pingThread;
		private Thread uptimeThread;
		private Thread environmentThread;

		private bool MODULE_STOP_FLAG;

		private StatusManager()
		{
			serverApplication = (App) Application.Current;
		}

		public static StatusManager GetInstance()
		{
			if (INSTANCE == null) INSTANCE = new StatusManager();
			return INSTANCE;
		}

		public void StartModules()
		{
			MODULE_STOP_FLAG = false;
			StartPing();
			StartUptime();
			StartEnvironment();
		}

		public void StopModules()
		{
			MODULE_STOP_FLAG = true;
		}

		private void StartPing()
		{
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", "PING 스레드 시작");
			if (pingThread == null || !pingThread.IsAlive)
			{
                pingThread = new Thread(DoPing)
                {
                    Priority = ThreadPriority.Lowest
                };
            } else if (pingThread.IsAlive)
			{
				return;
			}
			pingThread.Start();
		}
		
		private void DoPing()
		{
			Ping pingSender = new Ping();
            PingOptions pingOptions = new PingOptions
            {
                DontFragment = true
            };
            PingReply pingReply;
			string pingString = "12345678901234567890123456789012";
			byte[] pingBuffer = Encoding.ASCII.GetBytes(pingString);
			int pingTimeOut = 1000;
			string resultString;
			int pingCount = 0;

			while (true)
			{
				if (MODULE_STOP_FLAG)
				{
					break;
				}
				try
				{
					pingCount--;
					if (pingCount <= 0)
					{
						pingCount = 60;
						pingReply = pingSender.Send("google.co.kr", pingTimeOut, pingBuffer, pingOptions);
						if (pingReply != null && pingReply.Status == IPStatus.Success)
						{
							resultString = pingReply.RoundtripTime.ToString() + "ms";
						}
						else
						{
							resultString = Properties.Resources.ResourceManager.GetString("STATUS_OFF");
						}
						serverApplication.graphicalManager.OnPingStatusChanged(true, true, resultString);
					}
					Thread.Sleep(1000);
				}
				catch (Exception e)
				{
					try
					{
						serverApplication.graphicalManager.OnPingStatusChanged(true, false, null);
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "PING 스레드 에러");
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", e.ToString());
					} catch { }
					return;
				}
				
			}

			try
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", "PING 스레드 종료");
				serverApplication.graphicalManager.OnPingStatusChanged(false, false, null);
			} catch { }
		}

		private void StartUptime()
		{
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", "UPTIME 스레드 시작");
			if (uptimeThread == null || !uptimeThread.IsAlive)
			{
                uptimeThread = new Thread(DoUptime)
                {
                    Priority = ThreadPriority.Lowest
                };
            }
			else if (uptimeThread.IsAlive)
			{
				return;
			}
			uptimeThread.Start();
		}

		private void DoUptime()
		{
			TimeSpan runTime;

			while (true)
			{
				if (MODULE_STOP_FLAG)
				{
					break;
				}
				try
				{
					runTime = DateTime.Now - serverApplication.networkManager.serverStartTime;
					serverApplication.graphicalManager.OnUptimeStatusChanged(true, true, runTime.ToString(@"dd\:hh\:mm\:ss"));
					Thread.Sleep(1000);
				}
				catch (Exception e)
				{
					serverApplication.graphicalManager.OnUptimeStatusChanged(true, false, null);
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "UPTIME 스레드 에러");
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", e.ToString());
					return;
				}
			}

			try
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", "UPTIME 스레드 종료");
				serverApplication.graphicalManager.OnUptimeStatusChanged(false, false, null);
			}
			catch { }
		}

		private void StartEnvironment()
		{
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", "ENV 스레드 시작");
			if (environmentThread == null || !environmentThread.IsAlive)
			{
				environmentThread = new Thread(DoEnvironment);
			}
			else if (environmentThread.IsAlive)
			{
				return;
			}
			environmentThread.Start();
		}

		private void DoEnvironment()
		{
			bool envCpuDanger;
			bool envRamDanger;
			string envCpuString;
			string envRamString;
			PerformanceCounter envCpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
			float envCpuUsage;
			float envRamUsage;

			while (true)
			{
				if (MODULE_STOP_FLAG)
				{
					break;
				}
				try
				{
					envCpuUsage = envCpuCounter.NextValue();
					// ReSharper disable once PossibleLossOfFraction
					envRamUsage = GC.GetTotalMemory(true) / 1024;
					envCpuDanger = envCpuUsage > 100;
					envRamDanger = envRamUsage > 100000;
					envCpuString = string.Format("{0:0.00}%", envCpuUsage);
					envRamString = string.Format("{0:0}KB", envRamUsage);
					serverApplication.graphicalManager.OnCpuStatusChanged(true, true, envCpuDanger, envCpuString);
					serverApplication.graphicalManager.OnRamStatusChanged(true, true, envRamDanger, envRamString);
					Thread.Sleep(1000);
				}
				catch (Exception e)
				{
					try
					{
						serverApplication.graphicalManager.OnCpuStatusChanged(true, false, false, null);
						serverApplication.graphicalManager.OnRamStatusChanged(true, false, false, null);
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", "ENV 스레드 에러");
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", e.ToString());
						return;
					} catch { }
				}
			}

			try
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", "ENV 스레드 종료");
				serverApplication.graphicalManager.OnCpuStatusChanged(false, false, false, null);
				serverApplication.graphicalManager.OnRamStatusChanged(false, false, false, null);
			}
			catch { }
		}

	}
}
