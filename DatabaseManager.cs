using System;
using System.Data;
using System.Drawing.Printing;
using System.Windows;
using System.Threading;
using MySql.Data.MySqlClient;

namespace MSB_SERVER
{
    public class DatabaseManager
    {
		private static DatabaseManager INSTANCE;
		
		private readonly App serverApplication;

		private Thread databaseThread;

		private MySqlConnection dbConnection;

		private bool MODULE_STOP_FLAG;

		private int totalUser;

		private DatabaseManager()
		{
			serverApplication = (App) Application.Current;
		}

		public static DatabaseManager GetInstance()
		{
			return INSTANCE ??= new DatabaseManager();
		}

		public void StartDatabase()
		{
			MODULE_STOP_FLAG = false;
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 시작");
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken)
				{
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
			}
			catch (Exception e)
			{
				dbConnection?.Close();
				dbConnection = null;
				serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, false);
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 연결 끊김");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", e.Message);
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
			RefreshUserCount();
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
			serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, true);
			while (true)
			{
				if (MODULE_STOP_FLAG)
				{
					break;
				}

				if (dbConnection == null || dbConnection.State == ConnectionState.Broken || dbConnection.State == ConnectionState.Closed)
				{
					serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, false);
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 연결 끊김");
					return;
				}
				
				try
				{
					Thread.Sleep(1000);
				} catch (Exception e)
				{
					serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, false);
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 에러");
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", e.ToString());
					return;
				}
			}
			
			try
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 종료");
				serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(false, false);
			}
			catch { }
		}

		private void RefreshUserCount()
		{
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken)
				{
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand userSearchCommand = new MySqlCommand("SELECT COUNT(`user_index`) as `user_count` FROM `user`", dbConnection);
				using (MySqlDataReader dataReader = userSearchCommand.ExecuteReader())
				{
					if (dataReader.Read()) {
                    	totalUser = dataReader.GetInt32(dataReader.GetOrdinal("user_count"));
                    }
                    else
                    {
                    	totalUser = -1;
                    }
                    dataReader.Close();
				}
			}
			catch { }
		}

		public int GetTotalUserCount()
		{
			return totalUser;
		}

		public bool RequestUserLogin(string _id, string _pw, string _uuid, out NetworkData.UserData _userData, ref string message)
		{
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken)
				{
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand userSearchCommand = new MySqlCommand($"SELECT * FROM `user` WHERE `user_id` = '{_id}'", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserLogin", $"SELECT * FROM `user` WHERE `user_id` = '{_id}'");
				using MySqlDataReader dataReader = userSearchCommand.ExecuteReader();
				if (dataReader.Read())
				{
					int user_index = dataReader.GetInt32(dataReader.GetOrdinal("user_index"));
					string user_id = dataReader.GetString(dataReader.GetOrdinal("user_id"));
					string user_pw = dataReader.GetString(dataReader.GetOrdinal("user_pw"));
					string user_nick = dataReader.GetString(dataReader.GetOrdinal("user_nick"));
					int user_cash = dataReader.GetInt32(dataReader.GetOrdinal("user_cash"));
					int user_money = dataReader.GetInt32(dataReader.GetOrdinal("user_money"));
					int user_rank = dataReader.GetInt32(dataReader.GetOrdinal("user_rank"));
					dataReader.Close();
					
					MySqlCommand uuidInsertCommand = new MySqlCommand($"UPDATE `user` SET `user_uuid` = '{_uuid}' WHERE `user_id` = '{_id}'", dbConnection);
					LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserLogin", $"UPDATE `user` SET `user_uuid` = '{_uuid}' WHERE `user_id` = '{_id}'");
					uuidInsertCommand.ExecuteNonQuery();

					NetworkData.UserData user = new NetworkData.UserData(user_index, user_id, user_nick)
					{
						userCash = user_cash, userMoney = user_money, userRank = user_rank
					};
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
					dataReader.Close();
					if (RequestUserRegister(_id, _pw, null, ref message))
					{
						return RequestUserLogin(_id, _pw, _uuid, out _userData, ref message);
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
				message = "DB 문제가 발생하였습니다 : " + e.Message + " " + e.StackTrace;
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserRegister", e.Message + " " + e.StackTrace);
				return false;
			}
		}

		private bool RequestUserRegister(string _id, string _pw, string _uuid, ref string message)
		{
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken)
				{
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand userSearchCommand = new MySqlCommand($"SELECT * FROM `user` WHERE `user_id` = '{_id}'", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserRegister", $"SELECT * FROM `user` WHERE `user_id` = '{_id}'");
				using (MySqlDataReader dataReader = userSearchCommand.ExecuteReader())
				{
					if (dataReader.Read()) {
                    	message = "존재하는 ID입니다";
                    	dataReader.Close();
                    	return false;
                    }
                    dataReader.Close();
				}
				string passwordHash = BCrypt.Net.BCrypt.HashPassword(_pw);
				MySqlCommand userInsertCommand = new MySqlCommand($"INSERT INTO `user` (`user_id`, `user_pw`, `user_uuid`) VALUES ('{_id}', '{passwordHash}', {_uuid})", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserRegister", $"INSERT INTO `user` (`user_id`, `user_pw`, `user_uuid`) VALUES ('{_id}', '{passwordHash}', {_uuid})");
				int inserted = userInsertCommand.ExecuteNonQuery();
				if (inserted != 1)
				{
					message = "가입되지 않았습니다";
					return false;
				}
				
				RefreshUserCount();
				return true;
			}
			catch (Exception e)
			{
				message = "DB 문제가 발생하였습니다 : " + e.Message + " " + e.StackTrace;
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserRegister", e.Message + " " + e.StackTrace);
				return false;
			}
		}

		// ReSharper disable once RedundantAssignment
		public bool RequestUserStatus(string _id, out NetworkData.UserData _userData, ref string message)
		{
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken)
				{
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand userSearchCommand = new MySqlCommand($"SELECT * FROM `user` WHERE `user_id` = '{_id}'", dbConnection);
				using MySqlDataReader dataReader = userSearchCommand.ExecuteReader();
				if (dataReader.Read())
				{
					int user_index = dataReader.GetInt32(dataReader.GetOrdinal("user_index"));
					string user_id = dataReader.GetString(dataReader.GetOrdinal("user_id"));
					string user_nick = dataReader.GetString(dataReader.GetOrdinal("user_nick"));
					int user_cash = dataReader.GetInt32(dataReader.GetOrdinal("user_cash"));
					int user_money = dataReader.GetInt32(dataReader.GetOrdinal("user_money"));
					int user_rank = dataReader.GetInt32(dataReader.GetOrdinal("user_rank"));
					dataReader.Close();
					NetworkData.UserData user = new NetworkData.UserData(user_index, user_id, user_nick)
					{
						userCash = user_cash, userMoney = user_money, userRank = user_rank
					};
					_userData = user;
					message = "";
					return true;
				}
				else
				{
					dataReader.Close();
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
