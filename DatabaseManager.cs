using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Crypto.Generators;

namespace MSB_SERVER
{
    public class DatabaseManager
    {
		private static DatabaseManager INSTANCE;
		
		private readonly MSB_SERVER.App serverApplication;

		private Thread databaseThread;

		private MySqlConnection dbConnection;

		private bool MODULE_STOP_FLAG = false;

		private int totalUser = 0;

		private DatabaseManager()
		{
			serverApplication = (MSB_SERVER.App) Application.Current;
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
			try
			{
				if (dbConnection == null)
				{
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;");
				}

				dbConnection.Open();
				MySqlCommand userSearchCommand = new MySqlCommand("SELECT COUNT(`user_index`) FROM `users`", dbConnection);
				MySqlDataReader userSearchReader = userSearchCommand.ExecuteReader();
				while (userSearchReader.Read())
				{
					totalUser = Int32.Parse(userSearchReader[0].ToString());
				}
				userSearchReader.Close();
			}
			catch (Exception e)
			{
				dbConnection.Close();
				dbConnection = null;
				serverApplication.Dispatcher.Invoke(() => {
					serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, false);
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 연결 끊김");
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", e.Message);
				});
				return;
			}
			if (databaseThread == null || !databaseThread.IsAlive)
			{
				databaseThread = new Thread(DoDatabase);
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
			if (dbConnection != null)
			{
				dbConnection.Close();
				dbConnection = null;
			}
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

				if (dbConnection == null)
				{
					serverApplication.Dispatcher.Invoke(new Action(() => {
						serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, false);
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 연결 끊김");
					}));
					return;
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
			return totalUser;
		}

		public bool RequestUserLogin(string _id, string _pw, out NetworkData.UserData _userData, ref string message)
		{
			try
			{
				MySqlCommand userSearchCommand = new MySqlCommand($"SELECT * FROM `users` WHERE `user_id` = '{_id}'", dbConnection);
				MySqlDataReader userSearchReader = userSearchCommand.ExecuteReader();
				if (userSearchReader.Read())
				{
					int user_index = Int32.Parse(userSearchReader[0].ToString());
					string user_id = userSearchReader[1].ToString();
					string user_pw = userSearchReader[2].ToString();
					string user_nick = userSearchReader[3].ToString();
					int user_cash = Int32.Parse(userSearchReader[4].ToString());
					int user_money = Int32.Parse(userSearchReader[5].ToString());
					userSearchReader.Close();
					NetworkData.UserData user = new NetworkData.UserData(user_index, user_id, user_nick);
					user.userCash = user_cash;
					user.userMoney = user_money;
					if (BCrypt.Net.BCrypt.Verify(_pw, user_pw))
					{
						_userData = user;
						message = user_nick + "님 환영합니다!";
						return true;
					}
					else
					{
						_userData = null;
						message = "비밀번호 불일치";
						return false;
					}
				}
				else
				{
					userSearchReader.Close();
					if (RequestUserRegister(_id, _pw, null, ref message))
					{
						return RequestUserLogin(_id, _pw, out _userData, ref message);
					}
					else
					{
						_userData = null;
						message = "가입에 실패하였습니다";
						return false;
					}
				}
			}
			catch (Exception e)
			{
				_userData = null;
				message = "DB 문제가 발생하였습니다 : " + e.Message;
				return false;
			}
		}

		public bool RequestUserRegister(string _id, string _pw, string _name, ref string message)
		{
			try
			{
				MySqlCommand userSearchCommand = new MySqlCommand($"SELECT * FROM `users` WHERE `user_id` = '{_id}'", dbConnection);
				MySqlDataReader userSearchReader = userSearchCommand.ExecuteReader();
				if (userSearchReader.Read())
				{
					message = "존재하는 ID입니다";
					userSearchReader.Close();
					return false;
				}
				userSearchReader.Close();
				string passwordHash = BCrypt.Net.BCrypt.HashPassword(_pw);
				MySqlCommand userInsertCommand = new MySqlCommand($"INSERT INTO `users` (`user_id`, `user_pw`) VALUES ('{_id}', '{passwordHash}')", dbConnection);
				int inserted = userInsertCommand.ExecuteNonQuery();
				if (inserted != 1)
				{
					message = "가입되지 않았습니다";
					return false;
				}

				totalUser++;
				return true;
			}
			catch (Exception e)
			{
				message = "DB 문제가 발생하였습니다 : " + e.Message;
				return false;
			}
		}

		public bool RequestUserStatus(string _id, out NetworkData.UserData _userData, ref string message)
		{
			try
			{
				MySqlCommand userSearchCommand = new MySqlCommand($"SELECT * FROM `users` WHERE `user_id` = '{_id}'", dbConnection);
				MySqlDataReader userSearchReader = userSearchCommand.ExecuteReader();
				if (userSearchReader.Read())
				{
					int user_index = Int32.Parse(userSearchReader[0].ToString());
					string user_id = userSearchReader[1].ToString();
					string user_pw = userSearchReader[2].ToString();
					string user_nick = userSearchReader[3].ToString();
					int user_cash = Int32.Parse(userSearchReader[4].ToString());
					int user_money = Int32.Parse(userSearchReader[5].ToString());
					userSearchReader.Close();
					NetworkData.UserData user = new NetworkData.UserData(user_index, user_id, user_nick);
					user.userCash = user_cash;
					user.userMoney = user_money;
					_userData = user;
					message = "";
					return true;
				}
				else
				{
					userSearchReader.Close();
					_userData = null;
					message = "일치하는 유저가 없습니다";
					return false;
				}
			}
			catch (Exception e)
			{
				_userData = null;
				message = "DB 문제가 발생하였습니다 : " + e.Message;
				return false;
			}
		}
		
	}
}
