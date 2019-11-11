using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading;
using Newtonsoft.Json.Linq;
using Nettention.Proud;
using System.Collections.Concurrent;

namespace MSB_SERVER
{
	public partial class ServerManager
	{
		private static ServerManager INSTANCE;
		private static App serverApplication;

		private ConcurrentDictionary<HostID, NetworkData.ClientData> serverUserList;
		private ConcurrentDictionary<int, NetworkData.ClientData> serverUserIndexer;

		private List<NetworkData.ClientData> soloGameQueue;
		private List<NetworkData.ClientData> teamGameQueue;

		private List<GameRoom> serverGameList;

		private Thread soloMatchMaker;
		private Thread teamMatchMaker;

		private Thread statusCountThread;
		private Thread statusGameThread;

		private bool MODULE_QUEUE_STOP_FLAG;
		private bool MODULE_STATUS_STOP_FLAG;
		private bool MODULE_ROOM_STOP_FLAG;

		public bool SYNC_PROTOCOL_TCP = true;
        public bool SYNC_PROTOCOL_UDP = false;

        public bool INGAME_SYNC_PROTOCOL_TCP = true;
        public bool INGAME_SYNC_PROTOCOL_UDP = false;

        public bool DETAIL_LOG = false;

		private ServerManager()
		{
			serverApplication = (App) Application.Current;
			serverUserList = new ConcurrentDictionary<HostID, NetworkData.ClientData>();
			serverUserIndexer = new ConcurrentDictionary<int, NetworkData.ClientData>();
			soloGameQueue = new List<NetworkData.ClientData>();
			teamGameQueue = new List<NetworkData.ClientData>();
			serverGameList = new List<GameRoom>();
		}

		public static ServerManager GetInstance()
		{
			if (INSTANCE == null) INSTANCE = new ServerManager();
			return INSTANCE;
		}

		public void StartQueue()
		{
			MODULE_QUEUE_STOP_FLAG = false;
			StartSoloQueueMaker();
			StartTeamQueueMaker();
		}

		public void StopQueue()
		{
			MODULE_QUEUE_STOP_FLAG = true;
		}

		public void StartStatus()
		{
			MODULE_STATUS_STOP_FLAG = false;
			StartStatusCount();
			StartStatusGame();
		}

		public void StopStatus()
		{
			MODULE_STATUS_STOP_FLAG = true;
		}

        public void StartRoom()
        {
            MODULE_ROOM_STOP_FLAG = false;
        }

        public void StopRoom()
        {
            MODULE_ROOM_STOP_FLAG = true;
        }

        private void StartSoloQueueMaker()
		{
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "SoloMatchMaker 시작");
			if (soloMatchMaker == null || !soloMatchMaker.IsAlive)
			{
                soloMatchMaker = new Thread(SoloQueueMaker)
                {
                    Priority = ThreadPriority.Lowest
                };
            }
			else if (soloMatchMaker.IsAlive)
			{
				return;
			}
			soloMatchMaker.Start();
		}

		private void StartTeamQueueMaker()
		{
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "TeamMatchMaker 시작");
			if (teamMatchMaker == null || !teamMatchMaker.IsAlive)
			{
                teamMatchMaker = new Thread(TeamQueueMaker)
                {
                    Priority = ThreadPriority.Lowest
                };
            }
			else if (teamMatchMaker.IsAlive)
			{
				return;
			}
			teamMatchMaker.Start();
		}

		private void SoloQueueMaker()
		{
			serverApplication.graphicalManager.OnSoloModuleStatusChanged(true, true, 0);
			while (true)
			{
				if (MODULE_QUEUE_STOP_FLAG)
				{
					break;
				}
				try
				{
					if (soloGameQueue == null || soloGameQueue.Count < 2)
					{
						serverApplication.graphicalManager.OnSoloModuleStatusChanged(true, true, soloGameQueue?.Count ?? 0);
						Thread.Sleep(3000);
						continue;
					}
					soloGameQueue.Sort((clientA, clientB) => clientA.clientUser.userRank.CompareTo(clientB.clientUser.userRank));
					while (true)
					{
						if (soloGameQueue == null || soloGameQueue.Count < 2)
						{
							break;
						}
						NetworkData.ClientData clientDataA = soloGameQueue.ElementAt(0);
						NetworkData.ClientData clientDataB = soloGameQueue.ElementAt(1);
                        soloGameQueue.RemoveAt(0);
						soloGameQueue.RemoveAt(0);
                        GameRoom soloRoom = new GameRoom(0, GameRoom.GAME_TYPE.TYPE_SOLO, GameRoom.GAME_STATE.STATE_READY);
                        soloRoom.clientList.Add(clientDataA);
                        soloRoom.clientList.Add(clientDataB);
                        soloRoom.clientBlueList.Add(clientDataA);
						soloRoom.clientRedList.Add(clientDataB);
						clientDataA.currentGame = soloRoom;
						clientDataB.currentGame = soloRoom;
						clientDataA.currentReady = false;
						clientDataB.currentReady = false;
						serverGameList.Add(soloRoom);
						soloRoom.gameNumber = serverGameList.IndexOf(soloRoom);

						NetworkGate.OnGameMatched(clientDataA.clientHID, 1, soloRoom.gameNumber, string.Empty);
                        NetworkGate.OnGameMatched(clientDataB.clientHID, 1, soloRoom.gameNumber, string.Empty);

						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "Solo Game\n" + clientDataA.clientUser.userID + " VS " + clientDataB.clientUser.userID);
					}
				}
				catch (Exception e)
				{
					serverApplication.graphicalManager.OnSoloModuleStatusChanged(true, false, soloGameQueue?.Count ?? 0);
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "SoloMatchMaker 에러");
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", e.ToString());
					return;
				}
			}

			try
			{
				serverApplication.graphicalManager.OnSoloModuleStatusChanged(false, true, 0);
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "SoloMatchMaker 종료");
			}
			catch { }
		}

		private void TeamQueueMaker()
		{
			serverApplication.graphicalManager.OnTeamModuleStatusChanged(true, true, 0);
			while (true)
			{
				if (MODULE_QUEUE_STOP_FLAG)
				{
					break;
				}
				try
				{
					if (teamGameQueue == null || teamGameQueue.Count < 2)
					{
						serverApplication.graphicalManager.OnTeamModuleStatusChanged(true, true, teamGameQueue?.Count ?? 0);
						Thread.Sleep(3000);
						continue;
					}
					teamGameQueue.Sort((clientA, clientB) => clientA.clientUser.userRank.CompareTo(clientB.clientUser.userRank));
					while (true)
					{
						if (teamGameQueue == null || teamGameQueue.Count < 6)
						{
							break;
						}
						NetworkData.ClientData clientDataAA = teamGameQueue.ElementAt(0);
						NetworkData.ClientData clientDataBA = teamGameQueue.ElementAt(1);
						NetworkData.ClientData clientDataAB = teamGameQueue.ElementAt(2);
						NetworkData.ClientData clientDataBB = teamGameQueue.ElementAt(3);
						NetworkData.ClientData clientDataAC = teamGameQueue.ElementAt(4);
						NetworkData.ClientData clientDataBC = teamGameQueue.ElementAt(5);
						teamGameQueue.RemoveAt(0);
						teamGameQueue.RemoveAt(0);
                        teamGameQueue.RemoveAt(0);
                        teamGameQueue.RemoveAt(0);
                        teamGameQueue.RemoveAt(0);
                        teamGameQueue.RemoveAt(0);
                        GameRoom teamRoom = new GameRoom(0, GameRoom.GAME_TYPE.TYPE_TEAM, GameRoom.GAME_STATE.STATE_READY);
                        teamRoom.clientList.Add(clientDataAA);
                        teamRoom.clientList.Add(clientDataAB);
                        teamRoom.clientList.Add(clientDataAC);
                        teamRoom.clientList.Add(clientDataBA);
                        teamRoom.clientList.Add(clientDataBB);
                        teamRoom.clientList.Add(clientDataBC);
						teamRoom.clientBlueList.Add(clientDataAA);
						teamRoom.clientBlueList.Add(clientDataAB);
						teamRoom.clientBlueList.Add(clientDataAC);
						teamRoom.clientRedList.Add(clientDataBA);
						teamRoom.clientRedList.Add(clientDataBB);
						teamRoom.clientRedList.Add(clientDataBC);
						clientDataAA.currentGame = teamRoom;
						clientDataAB.currentGame = teamRoom;
						clientDataAC.currentGame = teamRoom;
						clientDataBA.currentGame = teamRoom;
						clientDataBB.currentGame = teamRoom;
						clientDataBC.currentGame = teamRoom;
						clientDataAA.currentReady = false;
						clientDataAB.currentReady = false;
						clientDataAC.currentReady = false;
						clientDataBA.currentReady = false;
						clientDataBB.currentReady = false;
						clientDataBC.currentReady = false;
						serverGameList.Add(teamRoom);
						teamRoom.gameNumber = serverGameList.IndexOf(teamRoom);

						NetworkGate.OnGameMatched(clientDataAA.clientHID, 1, teamRoom.gameNumber, string.Empty);
                        NetworkGate.OnGameMatched(clientDataAB.clientHID, 1, teamRoom.gameNumber, string.Empty);
                        NetworkGate.OnGameMatched(clientDataAC.clientHID, 1, teamRoom.gameNumber, string.Empty);
                        NetworkGate.OnGameMatched(clientDataBA.clientHID, 1, teamRoom.gameNumber, string.Empty);
                        NetworkGate.OnGameMatched(clientDataBB.clientHID, 1, teamRoom.gameNumber, string.Empty);
                        NetworkGate.OnGameMatched(clientDataBC.clientHID, 1, teamRoom.gameNumber, string.Empty);

                        serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "Team Game\n" + clientDataAA.clientUser.userID + " VS " + clientDataBA.clientUser.userID + "\n" + clientDataAB.clientUser.userID + " VS " + clientDataBB.clientUser.userID + "\n" + clientDataAC.clientUser.userID + " VS " + clientDataBC.clientUser.userID);
					}
				} catch (Exception e)
				{
					serverApplication.graphicalManager.OnTeamModuleStatusChanged(true, false, teamGameQueue?.Count ?? 0);
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "TeamMatchMaker 에러");
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", e.ToString());
				}
			}

			try
			{
				serverApplication.graphicalManager.OnTeamModuleStatusChanged(false, true, 0);
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "TeamMatchMaker 종료");
			} catch { }
		}

		private void StartStatusCount()
		{
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "CountModule 시작");
			if (statusCountThread == null || !statusCountThread.IsAlive)
			{
                statusCountThread = new Thread(DoStatusCount)
                {
                    Priority = ThreadPriority.Lowest
                };
            }
			else if (statusCountThread.IsAlive)
			{
				return;
			}
			statusCountThread.Start();
		}
		
		private void StartStatusGame()
		{
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "SyncModule 시작");
			if (statusGameThread == null || !statusGameThread.IsAlive)
			{
                statusGameThread = new Thread(DoStatusGame)
                {
                    Priority = ThreadPriority.Lowest
                };
            }
			else if (statusGameThread.IsAlive)
			{
				return;
			}
			statusGameThread.Start();
		}

		private void DoStatusCount()
		{
			int countLive;
			int countTotal;
			int countRoom;

			while (true)
			{
				if (MODULE_STATUS_STOP_FLAG)
				{
					break;
				}
				try
				{
					countLive = serverApplication.serverManager.serverUserList.Count;
					countTotal = serverApplication.databaseManager.GetTotalUserCount();
					countRoom = serverApplication.serverManager.serverGameList.Count;
						serverApplication.graphicalManager.OnUserCountChanged(true, true, countLive, countTotal);
						serverApplication.graphicalManager.OnRoomCountChanged(true, true, countRoom);
					Thread.Sleep(1000);
				} catch (Exception e)
				{
						serverApplication.graphicalManager.OnUserCountChanged(true, false, 0, 0);
						serverApplication.graphicalManager.OnRoomCountChanged(true, false, 0);
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "COUNT 스레드 에러");
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", e.ToString());
				}
			}

			try
			{
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", "COUNT 스레드 종료");
					serverApplication.graphicalManager.OnUserCountChanged(false, false, 0, 0);
					serverApplication.graphicalManager.OnRoomCountChanged(false, false, 0);
			}
			catch { }
		}

		private void DoStatusGame()
		{
			StringBuilder stringUserBuilder = new StringBuilder();
			StringBuilder stringRoomBuilder = new StringBuilder();

			while (true)
			{
				if (MODULE_STATUS_STOP_FLAG)
				{
					break;
				}
				try
				{
					stringUserBuilder.Clear();
					stringRoomBuilder.Clear();
					foreach (KeyValuePair<HostID, NetworkData.ClientData> pair in serverUserList)
					{
						if (pair.Value.clientUser != null && pair.Value.clientUser.userNick != null && pair.Value.clientUser.userNick.Length > 0)
						{
							stringUserBuilder.AppendLine(pair.Value.clientUser.userNick + "[" + pair.Key + "]");
						} else
						{
							stringUserBuilder.AppendLine(pair.Value.clientHID.GetHashCode().ToString() + "(" + pair.Key + ")");
						}
					}
					foreach (GameRoom gameRoom in serverGameList)
					{
						stringRoomBuilder.AppendLine((gameRoom.gameType == GameRoom.GAME_TYPE.TYPE_SOLO ? "[S]" : "[T]") + " " + gameRoom.GetHashCode().ToString());
					}
					serverApplication.graphicalManager.OnGameStatusUserSync(true, true, stringUserBuilder.ToString());
					serverApplication.graphicalManager.OnGameStatusRoomSync(true, true, stringRoomBuilder.ToString());
					Thread.Sleep(1000);
				} catch (Exception e)
				{
					serverApplication.graphicalManager.OnGameStatusUserSync(true, false, null);
					serverApplication.graphicalManager.OnGameStatusRoomSync(true, false, null);
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "SYNC 스레드 에러");
					serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", e.ToString());
				}
			}

			try
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "StatusManager", "SYNC 스레드 종료");
				serverApplication.graphicalManager.OnGameStatusUserSync(false, false, null);
				serverApplication.graphicalManager.OnGameStatusRoomSync(false, false, null);
			}
			catch { }
		}

		private static NetworkData.ClientData GetClientData(HostID hostID)
		{
			NetworkData.ClientData targetClient;
			foreach (KeyValuePair<HostID, NetworkData.ClientData> pair in serverApplication.serverManager.serverUserList)
			{
				if (pair.Value.clientHID.Equals(hostID))
				{
					targetClient = pair.Value;
                    return targetClient;
                }
			}
            return null;
		}

		// ReSharper disable once UnusedMember.Local
		private NetworkData.ClientData GetClientData(NetworkData.UserData userData)
        {
            NetworkData.ClientData targetClient = null;
            foreach (KeyValuePair<HostID, NetworkData.ClientData> pair in serverApplication.serverManager.serverUserList)
            {
	            if (pair.Value?.clientUser == null || pair.Value.clientUser.userID == null || pair.Value.clientUser.userID.Equals(string.Empty))
                {
                    continue;
                }
                if (pair.Value.clientUser.userID.Equals(userData.userID))
                {
                    targetClient = pair.Value;
                    break;
                }
            }
            return targetClient;
        }

        public void OnUserConnected(HostID hostID)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client != null)
            {
                return;
            }
            NetworkData.ClientData clientObject = new NetworkData.ClientData
            {
                clientHID = hostID
            };
            serverUserList.TryAdd(hostID, clientObject);

			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "유저 접속 : " + hostID);
		}

		public void OnUserDisconnected(HostID hostID)
		{
			NetworkData.ClientData client = GetClientData(hostID);
            if (client != null)
            {
                serverApplication.serverManager.serverUserList.TryRemove(hostID, out client);
			    soloGameQueue.Remove(client);
			    teamGameQueue.Remove(client);
            }
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "유저 종료 : " + hostID);
		}

		public void OnUserLogin(HostID hostID, string id, string pw, string uuid)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserLogin : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserLogin");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_id : " + id + "\n_pw : " + pw);
			try
			{
                string resultMSG = string.Empty;
                bool resultSuccess = serverApplication.databaseManager.RequestUserLogin(id, pw,uuid, out NetworkData.UserData resultUser, ref resultMSG);
                if (!resultSuccess)
				{
                    NetworkGate.OnLoginResult(client.clientHID, -1, -1, string.Empty, string.Empty, -1, -1, -1, -1, -1, -1, resultMSG);
                    return;
                }
                client.clientUser = resultUser;
                client.clientHID = hostID;
                serverUserIndexer.TryAdd(resultUser.userNumber, client);
                int gameRoom = client.currentGame?.gameNumber ?? -1;
                NetworkGate.OnLoginResult(client.clientHID, 1, resultUser.userNumber, resultUser.userID, resultUser.userNick, resultUser.userRank, resultUser.userMoney, resultUser.userCash, resultUser.userWeapon, resultUser.userSkin, gameRoom, resultMSG);
			} catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserLogin ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

		public void OnUserStatus(HostID hostID, string id)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserStatus : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserStatus");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_id : " + id);
            try
			{
                string resultMSG = string.Empty;
                bool resultSuccess = serverApplication.databaseManager.RequestUserStatus(id, out NetworkData.UserData resultUser, ref resultMSG);
                if (!resultSuccess)
                {
	                NetworkGate.OnStatusResult(client.clientHID, -1, -1, string.Empty, string.Empty, -1, -1, -1, -1, -1, -1, resultMSG);
	                return;
                }
                int gameRoom = -1;
                if (serverUserIndexer.TryGetValue(resultUser.userNumber, out NetworkData.ClientData targetClient))
                {
	                gameRoom = targetClient.currentGame?.gameNumber ?? -1;
                }
                NetworkGate.OnStatusResult(client.clientHID, 1, resultUser.userNumber, resultUser.userID, resultUser.userNick, resultUser.userRank, resultUser.userMoney, resultUser.userCash, resultUser.userWeapon, resultUser.userSkin, gameRoom, resultMSG);
			}
			catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserStatus ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}
		
		public void OnUserSystem(HostID hostID, string id)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserSystem : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserSystem");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_id : " + id);
            try
			{
				
			}
			catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserSystem ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

		public void OnGameQueue(HostID hostID, int weapon, int skin)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnSoloQueue : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnSoloQueue");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_weapon : " + weapon + "\n_skin : " + skin);
            try
			{
				client.clientUser.userWeapon = weapon;
				client.clientUser.userSkin = skin;
				soloGameQueue.Remove(client);
				teamGameQueue.Remove(client);
				soloGameQueue.Add(client);
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnSoloQueue OK");
			}
			catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnSoloQueue ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

		public void OnTeamQueue(HostID hostID, int weapon, int skin)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnTeamQueue : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnTeamQueue");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_weapon : " + weapon + "\n_skin : " + skin);
            try
			{
				client.clientUser.userWeapon = weapon;
				client.clientUser.userSkin = skin;
				soloGameQueue.Remove(client);
				teamGameQueue.Remove(client);
				teamGameQueue.Add(client);
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnTeamQueue OK");
			}
			catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnTeamQueue ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

		public void OnQuitQueue(HostID hostID)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnQuitQueue : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnQuitQueue");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID);
            try
			{
				soloGameQueue.Remove(client);
				teamGameQueue.Remove(client);
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnQuitQueue OK");
			}
			catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnQuitQueue ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

        public void OnGameInfo(HostID hostID, int room)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameInfo : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameInfo");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room);
            try
            {
                GameRoom targetRoom = serverGameList[room];
                if (targetRoom == null)
                {
                    NetworkGate.OnGameInfo(client.clientHID, -1, -1, -1, string.Empty);
                    return;
                }
                JArray userArray = new JArray();
                foreach (NetworkData.ClientData clientData in targetRoom.clientBlueList)
                {
                    JObject clientObject = new JObject
                            {
                                { NetworkData.OnGameInfo.TAG_USER_NUM, clientData.clientUser.userNumber },
                                { NetworkData.OnGameInfo.TAG_USER_ID, clientData.clientUser.userID },
                                { NetworkData.OnGameInfo.TAG_USER_NICK, clientData.clientUser.userNick },
                                { NetworkData.OnGameInfo.TAG_USER_RANK, clientData.clientUser.userRank },
                                { NetworkData.OnGameInfo.TAG_USER_WEAPON, clientData.clientUser.userWeapon },
                                { NetworkData.OnGameInfo.TAG_USER_SKIN, clientData.clientUser.userSkin }
                            };
                    userArray.Add(clientObject);
                }
                foreach (NetworkData.ClientData clientData in targetRoom.clientRedList)
                {
                    JObject clientObject = new JObject
                            {
                                { NetworkData.OnGameInfo.TAG_USER_NUM, clientData.clientUser.userNumber },
                                { NetworkData.OnGameInfo.TAG_USER_ID, clientData.clientUser.userID },
                                { NetworkData.OnGameInfo.TAG_USER_NICK, clientData.clientUser.userNick },
                                { NetworkData.OnGameInfo.TAG_USER_RANK, clientData.clientUser.userRank },
                                { NetworkData.OnGameInfo.TAG_USER_WEAPON, clientData.clientUser.userWeapon },
                                { NetworkData.OnGameInfo.TAG_USER_SKIN, clientData.clientUser.userSkin }
                            };
                    userArray.Add(clientObject);
                }
                NetworkGate.OnGameInfo(client.clientHID, 1, targetRoom.gameNumber, (int) targetRoom.gameType, userArray.ToString());
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameInfo ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }

		public void OnGameUserActionReady(HostID hostID, int room)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionReady : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionReady");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room);
            try
			{
                if (serverGameList[room] == null)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionReady NO TARGET");
                    return;
                }
                serverGameList[room].OnGameUserReady(client);
			}
			catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionReady ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

        public void OnGameUserActionDamage(HostID hostID, int room, int num, int amount, string option)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionDamage : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionDamage");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room + "\n_num : " + num + "\n_amount : " + amount + "\n_option : " + option);
            try
            {
                if (serverGameList[room] == null)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionDamage NO TARGET");
                    return;
                }
                serverGameList[room].OnGameUserDamage(client, num, amount, option);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionDamage ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }

        public void OnGameUserActionObject(HostID hostID, int room, int num, int amount)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionObject : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionObject");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room + "\n_num : " + num + "\n_amount : " + amount);
            try
            {
                if (serverGameList[room] == null)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionObject NO TARGET");
                    return;
                }
                serverGameList[room].OnGameUserObject(client, num, amount);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionObject ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }

        public void OnGameUserActionItem(HostID hostID, int room, int type, int num)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionItem : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionItem");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room + "\n_type : " + type + "\n_num : " + num);
            try
            {
                if (serverGameList[room] == null)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionItem NO TARGET");
                    return;
                }
                serverGameList[room].OnGameUserItem(client, type, num);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionItem ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }

        private void OnGameUserMove(HostID hostID, int gameRoom, string data)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserMove : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserMove");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_data : " + data);
            try
            {
                GameRoom targetRoom = serverGameList[gameRoom];
                foreach (NetworkData.ClientData clientData in targetRoom.clientBlueList)
                {
                    if (SYNC_PROTOCOL_TCP) serverApplication.networkManager.netS2CProxy.OnGameUserMove(clientData.clientHID, RmiContext.ReliableSend, data);
                    if (SYNC_PROTOCOL_UDP) serverApplication.networkManager.netS2CProxy.OnGameUserMove(clientData.clientHID, RmiContext.UnreliableSend, data);
                }
                foreach (NetworkData.ClientData clientData in targetRoom.clientRedList)
                {
                    if (SYNC_PROTOCOL_TCP) serverApplication.networkManager.netS2CProxy.OnGameUserMove(clientData.clientHID, RmiContext.ReliableSend, data);
                    if (SYNC_PROTOCOL_UDP) serverApplication.networkManager.netS2CProxy.OnGameUserMove(clientData.clientHID, RmiContext.UnreliableSend, data);
                }
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserMove ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }

        private void OnGameUserSync(HostID hostID, int gameRoom, string data)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData client);
            if (client == null)
            {
	            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserSync : NO CLIENT FOR HOST " + hostID);
	            return;
            }
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserSync");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_data : " + data);
            try
            {
                GameRoom targetRoom = serverGameList[gameRoom];
                foreach (NetworkData.ClientData clientData in targetRoom.clientBlueList)
                {
                    if (SYNC_PROTOCOL_TCP) serverApplication.networkManager.netS2CProxy.OnGameUserSync(clientData.clientHID, RmiContext.ReliableSend, data);
                    if (SYNC_PROTOCOL_UDP) serverApplication.networkManager.netS2CProxy.OnGameUserSync(clientData.clientHID, RmiContext.UnreliableSend, data);
                }
                foreach (NetworkData.ClientData clientData in targetRoom.clientRedList)
                {
                    if (SYNC_PROTOCOL_TCP) serverApplication.networkManager.netS2CProxy.OnGameUserSync(clientData.clientHID, RmiContext.ReliableSend, data);
                    if (SYNC_PROTOCOL_UDP) serverApplication.networkManager.netS2CProxy.OnGameUserSync(clientData.clientHID, RmiContext.UnreliableSend, data);
                }
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserSync ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }
    }
}
