using System;
using System.Data;
using System.Drawing.Printing;
using System.Windows;
using System.Threading;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace MSB_SERVER
{
    public class DatabaseManager
    {
		private static DatabaseManager INSTANCE;
		
		private readonly App serverApplication;

		private Thread databaseThread;

		private MySqlConnection dbConnection;

		public bool CURRENT_DB_CONNECTION = false;

		private bool MODULE_STOP_FLAG;

		public int totalUser;

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
			CURRENT_DB_CONNECTION = true;
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 시작");
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
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
			CURRENT_DB_CONNECTION = false;
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

				CURRENT_DB_CONNECTION = true;
				if (dbConnection != null && (dbConnection.State == ConnectionState.Connecting || dbConnection.State == ConnectionState.Executing || dbConnection.State == ConnectionState.Fetching || dbConnection.State == ConnectionState.Open))
				{
					serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, true);
				}
				else
				{
					if (dbConnection == null || dbConnection.State == ConnectionState.Broken || dbConnection.State == ConnectionState.Closed || dbConnection.Ping() == false)
					{
						CURRENT_DB_CONNECTION = false;
						serverApplication.graphicalManager.OnDatabaseModuleStatusChanged(true, false);
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager", "DATABASE 연결 끊김");
					}
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
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
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
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand userSearchCommand = new MySqlCommand($"SELECT * FROM `user` WHERE `user_id` = '{_id}'", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserLogin", userSearchCommand.ToString());
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
					LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserLogin", uuidInsertCommand.ToString());
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
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand userSearchCommand = new MySqlCommand($"SELECT * FROM `user` WHERE `user_id` = '{_id}'", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserRegister", userSearchCommand.ToString());
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
				MySqlCommand userInsertCommand = new MySqlCommand($"INSERT INTO `user` (`user_id`, `user_pw`, `user_uuid`) VALUES ('{_id}', '{passwordHash}', '{_uuid}')", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserRegister", userInsertCommand.ToString());
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
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
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
				message = "DB 문제가 발생하였습니다 : " + e.Message + " " + e.StackTrace;
				return false;
			}
		}
		
		public bool RequestUserNickname(string _id, string _nickname, ref string message)
		{
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand userSearchCommand = new MySqlCommand($"SELECT * FROM `user` WHERE `user_nick` = '{_nickname}'", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserNickname", userSearchCommand.ToString());
				using (MySqlDataReader dataReader = userSearchCommand.ExecuteReader())
				{
					if (dataReader.Read()) {
						message = "존재하는 닉네임입니다";
						dataReader.Close();
						return false;
					}
					dataReader.Close();
				}
				MySqlCommand command = new MySqlCommand($"UPDATE `user` SET `user_nick` = '{_nickname}' WHERE `user_id` = '{_id}'", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserNickname", command.ToString());
				int inserted = command.ExecuteNonQuery();
				if (inserted != 1)
				{
					message = "적용되지 않았습니다";
					return false;
				}
				
				return true;
			}
			catch (Exception e)
			{
				message = "DB 문제가 발생하였습니다 : " + e.Message + " " + e.StackTrace;
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserNickname", e.Message + " " + e.StackTrace);
				return false;
			}
		}
		
		public bool RequestLeaderBoard(string _id, ref string message)
		{
			try
			{
				JObject userRankResult = new JObject();
				JArray userRankArray = new JArray();
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand globalRankCommand = new MySqlCommand($"SELECT * FROM `user` ORDER BY `user_rank` DESC LIMIT 3", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserRank", globalRankCommand.ToString());
				using (MySqlDataReader dataReader = globalRankCommand.ExecuteReader())
				{
					while (dataReader.Read())
					{
						JObject userRankObject = new JObject();
						string user_id = dataReader.GetString(dataReader.GetOrdinal("user_id"));
						string user_nick = dataReader.GetString(dataReader.GetOrdinal("user_nick"));
						int user_rank = dataReader.GetInt32(dataReader.GetOrdinal("user_rank"));
						int user_played = dataReader.GetInt32(dataReader.GetOrdinal("user_played"));
						int user_win = dataReader.GetInt32(dataReader.GetOrdinal("user_win"));
						int user_lose = dataReader.GetInt32(dataReader.GetOrdinal("user_lose"));
						int user_kill = dataReader.GetInt32(dataReader.GetOrdinal("user_kill"));
						int user_death = dataReader.GetInt32(dataReader.GetOrdinal("user_death"));
						int user_assist = dataReader.GetInt32(dataReader.GetOrdinal("user_assist"));
						int user_damage_give = dataReader.GetInt32(dataReader.GetOrdinal("user_damage_give"));
						int user_damage_take = dataReader.GetInt32(dataReader.GetOrdinal("user_damage_take"));
						int user_character_1 = dataReader.GetInt32(dataReader.GetOrdinal("user_character_1"));
						int user_character_2 = dataReader.GetInt32(dataReader.GetOrdinal("user_character_2"));
						int user_character_3 = dataReader.GetInt32(dataReader.GetOrdinal("user_character_3"));
						userRankObject.Add("user_id", user_id);
						userRankObject.Add("user_nick", user_nick);
						userRankObject.Add("user_rank", user_rank);
						userRankObject.Add("user_played", user_played);
						userRankObject.Add("user_win", user_win);
						userRankObject.Add("user_lose", user_lose);
						userRankObject.Add("user_kill", user_kill);
						userRankObject.Add("user_death", user_death);
						userRankObject.Add("user_assist", user_assist);
						userRankObject.Add("user_damage_give", user_damage_give);
						userRankObject.Add("user_damage_take", user_damage_take);
						userRankObject.Add("user_character_1", user_character_1);
						userRankObject.Add("user_character_2", user_character_2);
						userRankObject.Add("user_character_3", user_character_3);
						userRankArray.Add(userRankObject);
					}
					userRankResult.Add("global_ranking", userRankArray);
					dataReader.Close();
				}
				MySqlCommand userRankCommand = new MySqlCommand($"select * from (select *, RANK() over (order by user_rank desc) as user_ranking from user) t where `user_id` = '{_id}'", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserRank", userRankCommand.ToString());
				using (MySqlDataReader dataReader = userRankCommand.ExecuteReader())
				{
					if (dataReader.Read()) {
						string user_id = dataReader.GetString(dataReader.GetOrdinal("user_id"));
						string user_nick = dataReader.GetString(dataReader.GetOrdinal("user_nick"));
						int user_rank = dataReader.GetInt32(dataReader.GetOrdinal("user_rank"));
						int user_played = dataReader.GetInt32(dataReader.GetOrdinal("user_played"));
						int user_win = dataReader.GetInt32(dataReader.GetOrdinal("user_win"));
						int user_lose = dataReader.GetInt32(dataReader.GetOrdinal("user_lose"));
						int user_kill = dataReader.GetInt32(dataReader.GetOrdinal("user_kill"));
						int user_death = dataReader.GetInt32(dataReader.GetOrdinal("user_death"));
						int user_assist = dataReader.GetInt32(dataReader.GetOrdinal("user_assist"));
						int user_damage_give = dataReader.GetInt32(dataReader.GetOrdinal("user_damage_give"));
						int user_damage_take = dataReader.GetInt32(dataReader.GetOrdinal("user_damage_take"));
						int user_character_1 = dataReader.GetInt32(dataReader.GetOrdinal("user_character_1"));
						int user_character_2 = dataReader.GetInt32(dataReader.GetOrdinal("user_character_2"));
						int user_character_3 = dataReader.GetInt32(dataReader.GetOrdinal("user_character_3"));
						int user_ranking = dataReader.GetInt32(dataReader.GetOrdinal("rank"));
						userRankResult.Add("user_id", user_id);
						userRankResult.Add("user_nick", user_nick);
						userRankResult.Add("user_rank", user_rank);
						userRankResult.Add("user_played", user_played);
						userRankResult.Add("user_win", user_win);
						userRankResult.Add("user_lose", user_lose);
						userRankResult.Add("user_kill", user_kill);
						userRankResult.Add("user_death", user_death);
						userRankResult.Add("user_assist", user_assist);
						userRankResult.Add("user_damage_give", user_damage_give);
						userRankResult.Add("user_damage_take", user_damage_take);
						userRankResult.Add("user_character_1", user_character_1);
						userRankResult.Add("user_character_2", user_character_2);
						userRankResult.Add("user_character_3", user_character_3);
						userRankResult.Add("user_ranking", user_ranking);
					}
					dataReader.Close();
				}

				message = userRankResult.ToString();
				return true;
			}
			catch (Exception e)
			{
				message = "DB 문제가 발생하였습니다 : " + e.Message + " " + e.StackTrace;
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : RequestUserRank", e.Message + " " + e.StackTrace);
				return false;
			}
		}

		public bool saveUserGameResult(ServerManager.GameRoom gameRoom, int _userIndex, NetworkData.ClientData _clientData)
		{
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand command = new MySqlCommand($"UPDATE `user` SET " +
				                                        $"`user_rank` = `user_rank` + @userRank, " +
				                                        $"`user_played` = `user_played` + @userPlayed, " +
				                                        $"`user_win` = `user_win` + @userWin, " +
				                                        $"`user_lose` = `user_lose` + @userLose, " +
				                                        $"`user_kill` = `user_kill` + @userKill, " +
				                                        $"`user_death` = `user_death` + @userDeath, " +
				                                        $"`user_assist` = `user_assist` + @userAssist, " +
				                                        $"`user_damage_give` = `user_damage_give` + @userDamageGive, " +
				                                        $"`user_damage_take` = `user_damage_take` + @userDamageTake, " +
				                                        $"`user_character_1` = `user_character_1` + @userCharacter1, " +
				                                        $"`user_character_2` = `user_character_2` + @userCharacter2, " +
				                                        $"`user_character_3` = `user_character_3` + @userCharacter3 " +
				                                        $"WHERE `user_index` = @userIndex", dbConnection);
				command.Parameters.AddWithValue("@userRank", _clientData.gameBonus);
				command.Parameters.AddWithValue("@userPlayed", 1);
				command.Parameters.AddWithValue("@userWin", _clientData.gameWin ? 1 : 0);
				command.Parameters.AddWithValue("@userLose", _clientData.gameLose ? 1 : 0);
				command.Parameters.AddWithValue("@userKill", _clientData.gameKill);
				command.Parameters.AddWithValue("@userDeath", _clientData.gameDeath);
				command.Parameters.AddWithValue("@userAssist", _clientData.gameAssist);
				command.Parameters.AddWithValue("@userDamageGive", _clientData.totalGivenDamage);
				command.Parameters.AddWithValue("@userDamageTake", _clientData.totalTakenDamage);
				command.Parameters.AddWithValue("@userCharacter1", _clientData.clientUser.userSkin == 0 ? 1 : 0);
				command.Parameters.AddWithValue("@userCharacter2", _clientData.clientUser.userSkin == 1 ? 1 : 0);
				command.Parameters.AddWithValue("@userCharacter3", _clientData.clientUser.userSkin == 2 ? 1 : 0);
				command.Parameters.AddWithValue("@userIndex", _userIndex);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveUserGameResult", command.ToString());
				int inserted = command.ExecuteNonQuery();
				if (inserted != 1)
				{
					LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveUserGameResult", "NOT UPDATED");
					return false;
				}
				return true;
			}
			catch (Exception e)
			{
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveUserGameResult", e.Message);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveUserGameResult", e.StackTrace);

				return false;
			}
		}
		
		public int saveUserMedal(ServerManager.GameRoom gameRoom, int _userIndex, int _medalIndex)
		{
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand sqlCommand = new MySqlCommand($"INSERT INTO `user_medal` (`medal_user`, `medal_type`, `medal_game`) VALUES ({_userIndex}, {_medalIndex}, {gameRoom.gameDatabaseIndex}); SELECT LAST_INSERT_ID()", dbConnection);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveUserMedal", sqlCommand.ToString());
				int inserted = Convert.ToInt32(sqlCommand.ExecuteScalar());
				if (inserted < 1)
				{
					LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveUserMedal", "NOT INSERTED");
					return 0;
				}
				return inserted;
			}
			catch (Exception e)
			{
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveUserMedal", e.Message);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveUserMedal", e.StackTrace);
				return 0;
			}
		}

		public int saveGameStart(ServerManager.GameRoom gameRoom)
		{
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand command = new MySqlCommand($"INSERT INTO `game` (`game_type`, `game_status`, `game_user1_index`, `game_user2_index`, `game_user3_index`, `game_user4_index`, `game_user5_index`, `game_user6_index`) VALUES (@gameType, @gameStatus, @gameUser1, @gameUser2, @gameUser3, @gameUser4, @gameUser5, @gameUser6); SELECT LAST_INSERT_ID()", dbConnection);
				command.Parameters.AddWithValue("@gameType", gameRoom.gameType == ServerManager.GameRoom.GAME_TYPE.TYPE_SOLO ? 1 : 2);
				command.Parameters.AddWithValue("@gameStatus", 1);
				if (gameRoom.gameType == ServerManager.GameRoom.GAME_TYPE.TYPE_SOLO)
				{
					command.Parameters.AddWithValue("@gameUser1", gameRoom.clientList[0].clientUser.userNumber);
					command.Parameters.AddWithValue("@gameUser2", gameRoom.clientList[1].clientUser.userNumber);
					command.Parameters.AddWithValue("@gameUser3", 0);
					command.Parameters.AddWithValue("@gameUser4", 0);
					command.Parameters.AddWithValue("@gameUser5", 0);
					command.Parameters.AddWithValue("@gameUser6", 0);
				}
				if (gameRoom.gameType == ServerManager.GameRoom.GAME_TYPE.TYPE_TEAM)
				{
					command.Parameters.AddWithValue("@gameUser1", gameRoom.clientList[0].clientUser.userNumber);
					command.Parameters.AddWithValue("@gameUser2", gameRoom.clientList[1].clientUser.userNumber);
					command.Parameters.AddWithValue("@gameUser3", gameRoom.clientList[2].clientUser.userNumber);
					command.Parameters.AddWithValue("@gameUser4", gameRoom.clientList[3].clientUser.userNumber);
					command.Parameters.AddWithValue("@gameUser5", gameRoom.clientList[4].clientUser.userNumber);
					command.Parameters.AddWithValue("@gameUser6", gameRoom.clientList[5].clientUser.userNumber);
				}
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveGameStart", command.ToString());
				int inserted = Convert.ToInt32(command.ExecuteScalar());
				if (inserted < 1)
				{
					LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveGameStart", "NOT INSERTED");
					return 0;
				}
				return inserted;
			}
			catch (Exception e)
			{
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveGameStart", e.Message);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveGameStart", e.StackTrace);
				return 0;
			}
		}
		public bool saveGameResult(ServerManager.GameRoom gameRoom)
		{
			try
			{
				if (dbConnection == null || dbConnection.State == ConnectionState.Closed || dbConnection.State == ConnectionState.Broken || dbConnection.Ping() == false)
				{
					dbConnection = null;
					dbConnection = new MySqlConnection("SERVER=localhost;DATABASE=msb;UID=msb;PASSWORD=4nocK2EPOgBG8bt6;Charset=utf8");
					dbConnection.Open();
				}
				MySqlCommand command = new MySqlCommand($"UPDATE `game` SET `game_status` = @gameStatus, `game_kill_blue` = @gameKillBlue, `game_kill_red` = @gameKillRed, `game_death_blue` = @gameDeathBlue, `game_death_red` = @gameDeathRed, `game_point_blue` = @gamePointBlue, `game_point_red` = @gamePointRed, `game_result_blue` = @gameResultBlue, `game_result_red` = @gameResultRed, `game_user1_medal` = @gameUser1Medal, `game_user2_medal` = @gameUser2Medal, `game_user3_medal` = @gameUser3Medal, `game_user4_medal` = @gameUser4Medal, `game_user5_medal` = @gameUser5Medal, `game_user6_medal` = @gameUser6Medal WHERE `game_index` = @gameDatabaseIndex", dbConnection);
				command.Parameters.AddWithValue("@gameStatus", 2);
				command.Parameters.AddWithValue("@gameKillBlue", gameRoom.gameBlueKill);
				command.Parameters.AddWithValue("@gameKillRed", gameRoom.gameRedKill);
				command.Parameters.AddWithValue("@gameDeathBlue", gameRoom.gameBlueDeath);
				command.Parameters.AddWithValue("@gameDeathRed", gameRoom.gameRedDeath);
				command.Parameters.AddWithValue("@gamePointBlue", gameRoom.gameBluePoint);
				command.Parameters.AddWithValue("@gamePointRed", gameRoom.gameRedPoint);
				if (gameRoom.gameBlueTotal == gameRoom.gameRedTotal)
				{
					command.Parameters.AddWithValue("@gameResultBlue", 2);
					command.Parameters.AddWithValue("@gameResultRed", 2);
				}
				if (gameRoom.gameBlueTotal > gameRoom.gameRedTotal)
				{
					command.Parameters.AddWithValue("@gameResultBlue", 3);
					command.Parameters.AddWithValue("@gameResultRed", 1);
				}
				if (gameRoom.gameBlueTotal < gameRoom.gameRedTotal)
				{
					command.Parameters.AddWithValue("@gameResultBlue", 1);
					command.Parameters.AddWithValue("@gameResultRed", 3);
				}
				command.Parameters.AddWithValue("@gameUser1Medal", gameRoom.clientList[0] != null ? string.Join(",", gameRoom.clientList[0].medalHistory.ToArray()) : "");
				command.Parameters.AddWithValue("@gameUser2Medal", gameRoom.clientList[1] != null ? string.Join(",", gameRoom.clientList[1].medalHistory.ToArray()) : "");
				if (gameRoom.gameType == ServerManager.GameRoom.GAME_TYPE.TYPE_SOLO)
				{
					command.Parameters.AddWithValue("@gameUser3Medal", "");
					command.Parameters.AddWithValue("@gameUser4Medal", "");
					command.Parameters.AddWithValue("@gameUser5Medal", "");
					command.Parameters.AddWithValue("@gameUser6Medal", "");
				}
				if (gameRoom.gameType == ServerManager.GameRoom.GAME_TYPE.TYPE_TEAM)
				{
					command.Parameters.AddWithValue("@gameUser3Medal", gameRoom.clientList[2] != null ? string.Join(",", gameRoom.clientList[2].medalHistory.ToArray()) : "");
					command.Parameters.AddWithValue("@gameUser4Medal", gameRoom.clientList[3] != null ? string.Join(",", gameRoom.clientList[3].medalHistory.ToArray()) : "");
					command.Parameters.AddWithValue("@gameUser5Medal", gameRoom.clientList[4] != null ? string.Join(",", gameRoom.clientList[4].medalHistory.ToArray()) : "");
					command.Parameters.AddWithValue("@gameUser6Medal", gameRoom.clientList[5] != null ? string.Join(",", gameRoom.clientList[5].medalHistory.ToArray()) : "");
				}
				command.Parameters.AddWithValue("@gameDatabaseIndex", gameRoom.gameDatabaseIndex);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveGameResult", command.ToString());
				int inserted = command.ExecuteNonQuery();
				if (inserted != 1)
				{
					LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveGameResult", "NOT UPDATED");
					return false;
				}
				return true;
			}
			catch (Exception e)
			{
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveGameResult", e.Message);
				LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "DatabaseManager : saveGameResult", e.StackTrace);
				return false;
			}
		}

    }
}
