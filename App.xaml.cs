using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MSB_SERVER
{
	// TODO 스레드 메서드 STATIC 화
	public partial class App : Application
	{
		public NetworkManager networkManager;
		public DatabaseManager databaseManager;
		public LogManager logManager;
		public StatusManager statusManager;
		public GraphicalManager graphicalManager;
		public ServerManager serverManager;
		public CommandManager commandManager;

		void OnStartup(object sender, StartupEventArgs eventArgs)
		{
			networkManager = NetworkManager.GetInstance();
			databaseManager = DatabaseManager.GetInstance();
			logManager = LogManager.GetInstance();
			statusManager = StatusManager.GetInstance();
			graphicalManager = GraphicalManager.GetInstance();
			serverManager = ServerManager.GetInstance();
			commandManager = CommandManager.GetInstance();

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                MSBUnhandledException((Exception) e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            DispatcherUnhandledException += (s, e) =>
                MSBUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
                MSBUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");

            graphicalManager.ShowGraphicalUserInterface();
		}

        public void MSBUnhandledException(Exception e, string sender)
        {
            logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "***CRITICAL ERROR***", sender);
            logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "***ERROR MESSAGE***", e.Message);
            logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "***STACK TRACE***", e.StackTrace);
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MSBUnhandledException(e.Exception, sender.ToString());
        }
    }
}
