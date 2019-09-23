using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;

namespace MSB_SERVER
{
    public class DatabaseManager
    {
		private static DatabaseManager INSTANCE;

		private readonly MSB_SERVER.App serverApplication;

		private Thread databaseThread;

		private LinkedList<NetworkData.UserData> userDatabase;

		private bool MODULE_STOP_FLAG = false;

		private DatabaseManager()
		{
			serverApplication = (MSB_SERVER.App) Application.Current;
			userDatabase = new LinkedList<NetworkData.UserData>();
			userDatabase.AddLast(new NetworkData.UserData(1, "LimeCake", "LimeCake", "TK"));
			userDatabase.AddLast(new NetworkData.UserData(2, "Qon", "Qon", "Qon"));
			userDatabase.AddLast(new NetworkData.UserData(3, "MJ", "MJ", "CodeMJ"));
			userDatabase.AddLast(new NetworkData.UserData(4, "Plug", "Plug", "Plug"));
			userDatabase.AddLast(new NetworkData.UserData(5, "Choco", "Choco", "Choco"));
			userDatabase.AddLast(new NetworkData.UserData(6, "TEST01", "TEST01", "TEST01"));
			userDatabase.AddLast(new NetworkData.UserData(7, "TEST02", "TEST02", "TEST02"));
		}

		public static DatabaseManager GetInstance()
		{
			if (INSTANCE == null) INSTANCE = new DatabaseManager();
			return INSTANCE;
		}

		public void StartDatabase()
		{
			MODULE_STOP_FLAG = false;
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 시작");
			if (databaseThread == null || !databaseThread.IsAlive)
			{
				databaseThread = new Thread(new ThreadStart(DoDatabase));
                databaseThread.Priority = ThreadPriority.Lowest;
			}
			else if (databaseThread.IsAlive)
			{
				return;
			}
			databaseThread.Start();
		}

		public void StopDatabase()
		{
			MODULE_STOP_FLAG = true;
		}

		private void DoDatabase()
		{
			serverApplication.Dispatcher.Invoke(new Action(() => {
				serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, true);
			}));
			while (true)
			{
				if (MODULE_STOP_FLAG)
				{
					break;
				}
				try
				{
					Thread.Sleep(1000);
				} catch (Exception e)
				{
					serverApplication.Dispatcher.Invoke(new Action(() => {
						serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, false);
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 에러");
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", e.ToString());
					}));
					return;
				}
			}
			
			try
			{
				serverApplication.Dispatcher.Invoke(new Action(() => {
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 종료");
					serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(false, false);
				}));
			}
			catch { }
		}

		public int GetTotalUserCount()
		{
			return userDatabase.Count;
		}

		public bool RequestUserLogin(string _id, string _pw, out NetworkData.UserData _userData, ref string message)
		{
			foreach (NetworkData.UserData user in userDatabase)
			{
				if (user.userID.Equals(_id))
				{
					if (user.userPW.Equals(_pw))
					{
						_userData = user;
						message = user.userNick + "님 환영합니다";
						return true;
					} else
					{
						_userData = null;
						message = "비밀번호 불일치";
						return false;
					}
				}
			}
			_userData = null;
			message = "일치하는 계정이 없습니다";
			return false;
		}

		public bool RequestUserRegister(string _id, string _pw, string _name, ref string message)
		{
			foreach (NetworkData.UserData user in userDatabase)
			{
				if (user.userID.Equals(_id))
				{
					message = "존재하는 ID입니다";
					return false;
				}
			}
			userDatabase.AddLast(new NetworkData.UserData(userDatabase.Count + 1, _id, _pw, _name));
			message = "가입되었습니다";
			return true;
		}

		public bool RequestUserStatus(string _id, out NetworkData.UserData _userData, ref string message)
		{
			foreach (NetworkData.UserData user in userDatabase)
			{
				if (user.userID.Equals(_id))
				{
					_userData = user;
					message = "";
					return true;
				}
			}
			_userData = null;
			message = "일치하는 계정이 없습니다";
			return false;
		}
	}
}
