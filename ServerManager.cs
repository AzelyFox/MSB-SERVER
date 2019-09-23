using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json.Linq;
using Nettention.Proud;
using System.Collections.Concurrent;

namespace MSB_SERVER
{
	public class ServerManager
	{
		private static ServerManager INSTANCE;

		private static MSB_SERVER.App serverApplication;

		public class GameRoom
		{
			public enum GAME_TYPE
			{
				TYPE_SOLO, TYPE_TEAM
			}
			public enum GAME_STATE
			{
				STATE_READY, STATE_INGAME, STATE_END, STATE_CLEAR
			}
			public int gameNumber;
			public GAME_TYPE gameType;
			public GAME_STATE gameState;
			public List<NetworkData.ClientData> clientBlueList;
			public List<NetworkData.ClientData> clientRedList;
            public Thread gameThread;
            public Thread readyCheckThread;

            public static int gameTime = 60;
            public static int gameHealPackValue = 30;
            public static int gameHealPackSpawn = 5;
            public static int gameUserSpawn = 5;
            public static int gameObjectHealth = 10;
            public static int gamePointValue = 1;
            public static int gamePointSpawn = 15;
            public int gameBlueKill = 0;
            public int gameBlueDeath = 0;
            public int gameBlueScore = 0;
            public int gameRedKill = 0;
            public int gameRedDeath = 0;
            public int gameRedScore = 0;
            public int[] gameObjects = new int[10] { gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth };
            public int[] gameHealPacks = new int[] { 1, 1, 1 };
            public int[] gamePointPacks = new int[] { 1, 1, 1 };

			public GameRoom(int number, GAME_TYPE type, GAME_STATE state)
			{
				gameNumber = number;
				gameType = type;
				gameState = state;
				clientBlueList = new List<NetworkData.ClientData>();
				clientRedList = new List<NetworkData.ClientData>();
			}

            public void OnGameUserReady(NetworkData.ClientData userClient)
            {
                userClient.currentReady = true;

                bool isEveryPlayerReady = true;

                if (readyCheckThread == null)
                {
                    readyCheckThread = new Thread(new ThreadStart(DoReadyCheck));
                    readyCheckThread.Start();
                }

                try
                {
                    JArray readyData = new JArray();
                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        JObject clientReadyData = new JObject
                        {
                            { clientData.clientUser.userNumber.ToString(), clientData.currentReady }
                        };
                        readyData.Add(clientReadyData);
                        if (!clientData.currentReady)
                        {
                            isEveryPlayerReady = false;
                        }
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        JObject clientReadyData = new JObject
                        {
                            { clientData.clientUser.userNumber.ToString(), clientData.currentReady }
                        };
                        readyData.Add(clientReadyData);
                        if (!clientData.currentReady)
                        {
                            isEveryPlayerReady = false;
                        }
                    }

                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusReady(clientData.clientHID, RmiContext.ReliableSend, readyData.ToString());
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusReady(clientData.clientHID, RmiContext.UnreliableSend, readyData.ToString());
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusReady(clientData.clientHID, RmiContext.ReliableSend, readyData.ToString());
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusReady(clientData.clientHID, RmiContext.UnreliableSend, readyData.ToString());
                    }
                } catch (Exception e)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "G]OnGameUserReady ERROR");
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
                }

                if (isEveryPlayerReady && gameThread == null)
                {
                    gameThread = new Thread(new ThreadStart(DoGameStart));
                    gameThread.Start();
                }
            }

            public void OnGameUserDamage(NetworkData.ClientData userClient, int dataTarget, int dataAmount, String dataOption)
            {
                try
                {
                    if (gameState != GAME_STATE.STATE_INGAME) return;

                    NetworkData.ClientData targetClient = null;
                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        if (clientData.clientUser.userNumber == dataTarget)
                        {
                            targetClient = clientData;
                            break;
                        }
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        if (clientData.clientUser.userNumber == dataTarget)
                        {
                            targetClient = clientData;
                            break;
                        }
                    }
                    if (targetClient == null || targetClient.gameRespawn > 0) return;
                    if (targetClient.gameHealth < dataAmount)
                    {
                        dataAmount = targetClient.gameHealth;
                    }
                    targetClient.gameHealth -= dataAmount;

                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventDamage(clientData.clientHID, RmiContext.ReliableSend, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataAmount, dataOption);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventDamage(clientData.clientHID, RmiContext.UnreliableSend, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataAmount, dataOption);
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventDamage(clientData.clientHID, RmiContext.ReliableSend, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataAmount, dataOption);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventDamage(clientData.clientHID, RmiContext.UnreliableSend, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataAmount, dataOption);
                    }

                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventHealth(clientData.clientHID, RmiContext.ReliableSend, targetClient.clientUser.userNumber, targetClient.gameHealth);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventHealth(clientData.clientHID, RmiContext.UnreliableSend, targetClient.clientUser.userNumber, targetClient.gameHealth);
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventHealth(clientData.clientHID, RmiContext.ReliableSend, targetClient.clientUser.userNumber, targetClient.gameHealth);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventHealth(clientData.clientHID, RmiContext.UnreliableSend, targetClient.clientUser.userNumber, targetClient.gameHealth);
                    }

                    if (targetClient.gameHealth <= 0)
                    {
                        foreach (NetworkData.ClientData clientData in clientBlueList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventKill(clientData.clientHID, RmiContext.ReliableSend, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataOption);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventKill(clientData.clientHID, RmiContext.UnreliableSend, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataOption);
                        }
                        foreach (NetworkData.ClientData clientData in clientRedList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventKill(clientData.clientHID, RmiContext.ReliableSend, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataOption);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventKill(clientData.clientHID, RmiContext.UnreliableSend, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataOption);
                        }

                        foreach (NetworkData.ClientData clientData in clientBlueList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusMessage(clientData.clientHID, RmiContext.ReliableSend, 0, userClient.clientUser.userNick + "KILLED " + targetClient.clientUser.userNick + "!");
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusMessage(clientData.clientHID, RmiContext.UnreliableSend, 0, userClient.clientUser.userNick + "KILLED " + targetClient.clientUser.userNick + "!");
                        }
                        foreach (NetworkData.ClientData clientData in clientRedList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusMessage(clientData.clientHID, RmiContext.ReliableSend, 0, userClient.clientUser.userNick + "KILLED " + targetClient.clientUser.userNick + "!");
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusMessage(clientData.clientHID, RmiContext.UnreliableSend, 0, userClient.clientUser.userNick + "KILLED " + targetClient.clientUser.userNick + "!");
                        }

                        if (userClient!= targetClient) userClient.gameKill++;
                        targetClient.gameDeath++;
                        targetClient.gameRespawn = gameUserSpawn;

                        gameBlueKill = 0;
                        gameBlueDeath = 0;
                        gameRedKill = 0;
                        gameRedDeath = 0;
                        foreach (NetworkData.ClientData clientData in clientBlueList)
                        {
                            gameBlueKill += clientData.gameKill;
                            gameBlueDeath += clientData.gameDeath;
                        }
                        foreach (NetworkData.ClientData clientData in clientRedList)
                        {
                            gameRedKill += clientData.gameKill;
                            gameRedDeath += clientData.gameDeath;
                        }

                        foreach (NetworkData.ClientData clientData in clientBlueList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusScore(clientData.clientHID, RmiContext.ReliableSend, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusScore(clientData.clientHID, RmiContext.UnreliableSend, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
                        }
                        foreach (NetworkData.ClientData clientData in clientRedList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusScore(clientData.clientHID, RmiContext.ReliableSend, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusScore(clientData.clientHID, RmiContext.UnreliableSend, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
                        }
                    }
                } catch (Exception e)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "G]OnGameUserDamage ERROR");
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
                }
            }

            public void OnGameUserObject(NetworkData.ClientData userClient, int dataTarget, int dataAmount)
            {
                try
                {
                    if (gameState != GAME_STATE.STATE_INGAME) return;

                    if (dataTarget >= gameObjects.Length) return;
                    if (gameObjects[dataTarget] < dataAmount)
                    {
                        dataAmount = gameObjects[dataTarget];
                    }
                    gameObjects[dataTarget] -= dataAmount;

                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventObject(clientData.clientHID, RmiContext.ReliableSend, dataTarget, gameObjects[dataTarget]);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventObject(clientData.clientHID, RmiContext.UnreliableSend, dataTarget, gameObjects[dataTarget]);
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventObject(clientData.clientHID, RmiContext.ReliableSend, dataTarget, gameObjects[dataTarget]);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventObject(clientData.clientHID, RmiContext.UnreliableSend, dataTarget, gameObjects[dataTarget]);
                    }

                } catch (Exception e)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "G]OnGameUserObject ERROR");
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
                }
            }

            public void OnGameUserItem(NetworkData.ClientData userClient, int type, int dataTarget)
            {
                try
                {
                    if (gameState != GAME_STATE.STATE_INGAME) return;

                    if (type == 0) // SCORE POINT ITEM
                    {
                        if (dataTarget >= gamePointPacks.Length) return;
                        if (gamePointPacks[dataTarget] > 0) return;

                        foreach (NetworkData.ClientData clientData in clientBlueList)
                        {
                            if (userClient.clientUser.userNumber == clientData.clientUser.userNumber)
                            {
                                gameBlueScore += gamePointValue;
                                break;
                            }
                        }
                        foreach (NetworkData.ClientData clientData in clientRedList)
                        {
                            if (userClient.clientUser.userNumber == clientData.clientUser.userNumber)
                            {
                                gameRedScore += gamePointValue;
                                break;
                            }
                        }

                        foreach (NetworkData.ClientData clientData in clientBlueList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.ReliableSend, type, dataTarget, userClient.clientUser.userNumber);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.UnreliableSend, type, dataTarget, userClient.clientUser.userNumber);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusScore(clientData.clientHID, RmiContext.ReliableSend, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusScore(clientData.clientHID, RmiContext.UnreliableSend, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
                        }
                        foreach (NetworkData.ClientData clientData in clientRedList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.ReliableSend, type, dataTarget, userClient.clientUser.userNumber);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.UnreliableSend, type, dataTarget, userClient.clientUser.userNumber);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusScore(clientData.clientHID, RmiContext.ReliableSend, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusScore(clientData.clientHID, RmiContext.UnreliableSend, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
                        }

                        gamePointPacks[dataTarget] = gamePointSpawn;
                    }

                    if (type == 1) // HEALPACK ITEM
                    {
                        if (dataTarget >= gameHealPacks.Length) return;
                        if (gameHealPacks[dataTarget] > 0) return;

                        userClient.gameHealth += gameHealPackValue;
                        if (userClient.gameHealth > 100) userClient.gameHealth = 100;

                        foreach (NetworkData.ClientData clientData in clientBlueList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.ReliableSend, type, dataTarget, userClient.clientUser.userNumber);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.UnreliableSend, type, dataTarget, userClient.clientUser.userNumber);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventHealth(clientData.clientHID, RmiContext.ReliableSend, userClient.clientUser.userNumber, userClient.gameHealth);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventHealth(clientData.clientHID, RmiContext.UnreliableSend, userClient.clientUser.userNumber, userClient.gameHealth);
                        }
                        foreach (NetworkData.ClientData clientData in clientRedList)
                        {
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.ReliableSend, type, dataTarget, userClient.clientUser.userNumber);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.UnreliableSend, type, dataTarget, userClient.clientUser.userNumber);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventHealth(clientData.clientHID, RmiContext.ReliableSend, userClient.clientUser.userNumber, userClient.gameHealth);
                            if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventHealth(clientData.clientHID, RmiContext.UnreliableSend, userClient.clientUser.userNumber, userClient.gameHealth);
                        }

                        gameHealPacks[dataTarget] = gameHealPackSpawn;
                    }

                    

                } catch (Exception e)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "G]OnGameUserItem ERROR");
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
                }
            }

            private void DoReadyCheck()
            {
                Thread.Sleep(1000);
                if (gameState != GAME_STATE.STATE_READY) return;
                if (ServerManager.GetInstance().MODULE_ROOM_STOP_FLAG) return;
                Thread.Sleep(1000);
                if (gameState != GAME_STATE.STATE_READY) return;
                if (ServerManager.GetInstance().MODULE_ROOM_STOP_FLAG) return;
                Thread.Sleep(1000);
                if (gameState != GAME_STATE.STATE_READY) return;
                if (ServerManager.GetInstance().MODULE_ROOM_STOP_FLAG) return;
                Thread.Sleep(1000);
                if (gameState != GAME_STATE.STATE_READY) return;
                if (ServerManager.GetInstance().MODULE_ROOM_STOP_FLAG) return;
                Thread.Sleep(1000);
                if (gameState != GAME_STATE.STATE_READY) return;
                if (ServerManager.GetInstance().MODULE_ROOM_STOP_FLAG) return;
                gameThread = new Thread(new ThreadStart(DoGameStart));
                gameThread.Start();
            }

            private void DoGameStart()
            {
                gameState = GAME_STATE.STATE_INGAME;

                foreach (NetworkData.ClientData clientData in clientBlueList)
                {
                    clientData.gameDeath = 0;
                    clientData.gameKill = 0;
                    clientData.gameRespawn = 0;
                    clientData.gameHealth = 100;
                }
                foreach (NetworkData.ClientData clientData in clientRedList)
                {
                    clientData.gameDeath = 0;
                    clientData.gameKill = 0;
                    clientData.gameRespawn = 0;
                    clientData.gameHealth = 100;
                }

                for (int countDown = 3; countDown >= 0; countDown --)
                {
                    if (ServerManager.GetInstance().MODULE_ROOM_STOP_FLAG)
                    {
                        return;
                    }

                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusCountdown(clientData.clientHID, RmiContext.ReliableSend, countDown);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusCountdown(clientData.clientHID, RmiContext.UnreliableSend, countDown);
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusCountdown(clientData.clientHID, RmiContext.ReliableSend, countDown);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusCountdown(clientData.clientHID, RmiContext.UnreliableSend, countDown);
                    }

                    if (countDown > 0)
                    {
                        Thread.Sleep(1000);
                    }
                }

                for (int remainGameTime = gameTime; remainGameTime >= 0; remainGameTime--)
                {
                    if (ServerManager.GetInstance().MODULE_ROOM_STOP_FLAG)
                    {
                        return;
                    }

                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusTime(clientData.clientHID, RmiContext.ReliableSend, remainGameTime);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusTime(clientData.clientHID, RmiContext.UnreliableSend, remainGameTime);
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusTime(clientData.clientHID, RmiContext.ReliableSend, remainGameTime);
                        if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameStatusTime(clientData.clientHID, RmiContext.UnreliableSend, remainGameTime);
                    }

                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        if (clientData.gameRespawn > 0)
                        {
                            clientData.gameRespawn--;
                            clientData.gameHealth = 100;

                            foreach (NetworkData.ClientData cData in clientBlueList)
                            {
                                if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventRespawn(cData.clientHID, RmiContext.ReliableSend, clientData.clientUser.userNumber, clientData.gameRespawn);
                                if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventRespawn(cData.clientHID, RmiContext.UnreliableSend, clientData.clientUser.userNumber, clientData.gameRespawn);
                            }
                            foreach (NetworkData.ClientData cData in clientRedList)
                            {
                                if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventRespawn(cData.clientHID, RmiContext.ReliableSend, clientData.clientUser.userNumber, clientData.gameRespawn);
                                if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventRespawn(cData.clientHID, RmiContext.UnreliableSend, clientData.clientUser.userNumber, clientData.gameRespawn);
                            }
                        }
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        if (clientData.gameRespawn > 0)
                        {
                            clientData.gameRespawn--;
                            clientData.gameHealth = 100;

                            foreach (NetworkData.ClientData cData in clientBlueList)
                            {
                                if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventRespawn(cData.clientHID, RmiContext.ReliableSend, clientData.clientUser.userNumber, clientData.gameRespawn);
                                if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventRespawn(cData.clientHID, RmiContext.UnreliableSend, clientData.clientUser.userNumber, clientData.gameRespawn);
                            }
                            foreach (NetworkData.ClientData cData in clientRedList)
                            {
                                if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventRespawn(cData.clientHID, RmiContext.ReliableSend, clientData.clientUser.userNumber, clientData.gameRespawn);
                                if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventRespawn(cData.clientHID, RmiContext.UnreliableSend, clientData.clientUser.userNumber, clientData.gameRespawn);
                            }
                        }
                    }

                    for (int index = 0; index < gameHealPacks.Length; index ++)
                    {
                        if (gameHealPacks[index] > 0)
                        {
                            gameHealPacks[index] --;
                            if (gameHealPacks[index] == 0)
                            {
                                foreach (NetworkData.ClientData clientData in clientBlueList)
                                {
                                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.ReliableSend, 1, index, 0);
                                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.UnreliableSend, 1, index, 0);
                                }
                                foreach (NetworkData.ClientData clientData in clientRedList)
                                {
                                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.ReliableSend, 1, index, 0);
                                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.UnreliableSend, 1, index, 0);
                                }
                            }
                        }
                    }

                    for (int index = 0; index < gamePointPacks.Length; index++)
                    {
                        if (gamePointPacks[index] > 0)
                        {
                            gamePointPacks[index]--;
                            if (gamePointPacks[index] == 0)
                            {
                                foreach (NetworkData.ClientData clientData in clientBlueList)
                                {
                                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.ReliableSend, 0, index, 0);
                                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.UnreliableSend, 0, index, 0);
                                }
                                foreach (NetworkData.ClientData clientData in clientRedList)
                                {
                                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.ReliableSend, 0, index, 0);
                                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameEventItem(clientData.clientHID, RmiContext.UnreliableSend, 0, index, 0);
                                }
                            }
                        }
                    }

                    if (remainGameTime > 0)
                    {
                        Thread.Sleep(1000);
                    }
                }

                DoGameEnd();
            }

            private void DoGameEnd()
            {
                gameState = GAME_STATE.STATE_END;

                foreach (NetworkData.ClientData clientData in clientBlueList)
                {
                    clientData.currentReady = false;
                    clientData.currentGame = null;
                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameResult(clientData.clientHID, RmiContext.ReliableSend, String.Empty);
                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameResult(clientData.clientHID, RmiContext.UnreliableSend, String.Empty);
                }
                foreach (NetworkData.ClientData clientData in clientRedList)
                {
                    clientData.currentReady = false;
                    clientData.currentGame = null;
                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP) NetworkManager.GetInstance().netS2CProxy.OnGameResult(clientData.clientHID, RmiContext.ReliableSend, String.Empty);
                    if (serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP) NetworkManager.GetInstance().netS2CProxy.OnGameResult(clientData.clientHID, RmiContext.UnreliableSend, String.Empty);
                }

                gameState = GAME_STATE.STATE_CLEAR;
            }
		}

		public ConcurrentDictionary<HostID, NetworkData.ClientData> serverUserList;

		public List<NetworkData.ClientData> soloGameQueue;
		public List<NetworkData.ClientData> teamGameQueue;

		public List<GameRoom> serverGameList;

		private Thread soloMatchMaker;
		private Thread teamMatchMaker;

		private Thread statusCountThread;
		private Thread statusGameThread;

		private bool MODULE_QUEUE_STOP_FLAG = false;
		private bool MODULE_STATUS_STOP_FLAG = false;
		private bool MODULE_ROOM_STOP_FLAG = false;

        public bool SYNC_PROTOCOL_TCP = true;
        public bool SYNC_PROTOCOL_UDP = false;

        public bool INGAME_SYNC_PROTOCOL_TCP = true;
        public bool INGAME_SYNC_PROTOCOL_UDP = false;

        public bool DETAIL_LOG = false;

		private ServerManager()
		{
			serverApplication = (MSB_SERVER.App) Application.Current;
			serverUserList = new ConcurrentDictionary<HostID, NetworkData.ClientData>();
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
                soloMatchMaker = new Thread(new ThreadStart(SoloQueueMaker))
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
                teamMatchMaker = new Thread(new ThreadStart(TeamQueueMaker))
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
						serverApplication.graphicalManager.OnSoloModuleStatusChanged(true, true, soloGameQueue.Count);
						Thread.Sleep(1000);
						continue;
					}
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
                        soloRoom.clientBlueList.Add(clientDataA);
						soloRoom.clientRedList.Add(clientDataB);
						clientDataA.currentGame = soloRoom;
						clientDataB.currentGame = soloRoom;
						clientDataA.currentReady = false;
						clientDataB.currentReady = false;
						serverGameList.Add(soloRoom);
						soloRoom.gameNumber = serverGameList.IndexOf(soloRoom);

                        JObject resultObject = new JObject
                        {
                            { NetworkData.OnSoloMatched.TAG_RESULT, true },
                            { NetworkData.OnSoloMatched.TAG_ROOM, soloRoom.gameNumber },
                            { NetworkData.OnSoloMatched.TAG_MESSAGE, String.Empty }
                        };

                        serverApplication.networkManager.netS2CProxy.OnSoloMatched(clientDataA.clientHID, RmiContext.ReliableSend, 1, soloRoom.gameNumber, String.Empty);
                        serverApplication.networkManager.netS2CProxy.OnSoloMatched(clientDataB.clientHID, RmiContext.ReliableSend, 1, soloRoom.gameNumber, String.Empty);

						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "Solo Game\n" + clientDataA.clientUser.userID + " VS " + clientDataB.clientUser.userID);
					}
				}
				catch (Exception e)
				{
					serverApplication.graphicalManager.OnSoloModuleStatusChanged(true, false, soloGameQueue.Count);
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
						serverApplication.graphicalManager.OnTeamModuleStatusChanged(true, true, teamGameQueue.Count);
						Thread.Sleep(1000);
						continue;
					}
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

                        JObject resultObject = new JObject
                        {
                            { NetworkData.OnTeamMatched.TAG_RESULT, true },
                            { NetworkData.OnTeamMatched.TAG_ROOM, teamRoom.gameNumber },
                            { NetworkData.OnTeamMatched.TAG_MESSAGE, String.Empty }
                        };

                        serverApplication.networkManager.netS2CProxy.OnTeamMatched(clientDataAA.clientHID, RmiContext.ReliableSend, 1, teamRoom.gameNumber, String.Empty);
                        serverApplication.networkManager.netS2CProxy.OnTeamMatched(clientDataAB.clientHID, RmiContext.ReliableSend, 1, teamRoom.gameNumber, String.Empty);
                        serverApplication.networkManager.netS2CProxy.OnTeamMatched(clientDataAC.clientHID, RmiContext.ReliableSend, 1, teamRoom.gameNumber, String.Empty);
                        serverApplication.networkManager.netS2CProxy.OnTeamMatched(clientDataBA.clientHID, RmiContext.ReliableSend, 1, teamRoom.gameNumber, String.Empty);
                        serverApplication.networkManager.netS2CProxy.OnTeamMatched(clientDataBB.clientHID, RmiContext.ReliableSend, 1, teamRoom.gameNumber, String.Empty);
                        serverApplication.networkManager.netS2CProxy.OnTeamMatched(clientDataBC.clientHID, RmiContext.ReliableSend, 1, teamRoom.gameNumber, String.Empty);

                        serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "Team Game\n" + clientDataAA.clientUser.userID + " VS " + clientDataBA.clientUser.userID + "\n" + clientDataAB.clientUser.userID + " VS " + clientDataBB.clientUser.userID + "\n" + clientDataAC.clientUser.userID + " VS " + clientDataBC.clientUser.userID);
					}
				} catch (Exception e)
				{
					serverApplication.graphicalManager.OnTeamModuleStatusChanged(true, false, teamGameQueue.Count);
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
                statusCountThread = new Thread(new ThreadStart(DoStatusCount))
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
                statusGameThread = new Thread(new ThreadStart(DoStatusGame))
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
			int countLive = 0;
			int countTotal = 0;
			int countRoom = 0;

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

		private NetworkData.ClientData GetClientData(HostID hostID)
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

        private NetworkData.ClientData GetClientData(NetworkData.UserData userData)
        {
            NetworkData.ClientData targetClient = null;
            foreach (KeyValuePair<HostID, NetworkData.ClientData> pair in serverApplication.serverManager.serverUserList)
            {
                if (pair.Value == null)
                {
                    continue;
                }
                if (pair.Value.clientUser == null || pair.Value.clientUser.userID == null || pair.Value.clientUser.userID.Equals(String.Empty))
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

        public NetworkData.ClientData OnUserConnected(HostID hostID)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            if (targetClient != null)
            {
                return targetClient;
            }
            NetworkData.ClientData clientObject = new NetworkData.ClientData
            {
                clientHID = hostID
            };
            serverUserList.TryAdd(hostID, clientObject);

			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "유저 접속 : " + hostID);

			return clientObject;
		}

		public void OnUserDisconnected(HostID hostID)
		{
			NetworkData.ClientData targetClient = GetClientData(hostID);
            if (targetClient != null)
            {
                serverApplication.serverManager.serverUserList.TryRemove(hostID, out targetClient);
			    soloGameQueue.Remove(targetClient);
			    teamGameQueue.Remove(targetClient);
            }
			serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "유저 종료 : " + hostID);
		}

		public void OnUserLogin(HostID hostID, string _id, string _pw)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserLogin");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_id : " + _id + "\n_pw : " + _pw);
			try
			{
                string resultMSG = String.Empty;
                bool resultSuccess = serverApplication.databaseManager.RequestUserLogin(_id, _pw, out NetworkData.UserData resultUser, ref resultMSG);
                if (!resultSuccess)
				{
                    serverApplication.networkManager.netS2CProxy.OnLoginResult(targetClient.clientHID, RmiContext.ReliableSend, -1, -1, String.Empty, String.Empty, -1, -1, -1, -1, -1, -1, resultMSG);
                    return;
                }
                if (!targetClient.clientHID.Equals(hostID))
                {
                    // 기존 클라이언트 처리
                }
                targetClient.clientUser = resultUser;
                targetClient.clientHID = hostID;
                int gameRoom = -1;
                if (targetClient.currentGame != null)
                {
                    gameRoom = targetClient.currentGame.gameNumber;
                }
                serverApplication.networkManager.netS2CProxy.OnLoginResult(targetClient.clientHID, RmiContext.ReliableSend, 1, resultUser.userNumber, resultUser.userID, resultUser.userNick, resultUser.userRank, resultUser.userMoney, resultUser.userCash, resultUser.userWeapon, resultUser.userSkin, gameRoom, resultMSG);
			} catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserLogin ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

		public void OnUserRegister(HostID hostID, String id, String pw, String nick)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserRegister");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_id : " + id + "\n_pw : " + pw + "\n_nick : " + nick);
            try
			{
				string resultMSG = String.Empty;
				bool resultSuccess = serverApplication.databaseManager.RequestUserRegister(id, pw, nick, ref resultMSG);
                JObject resultObject = new JObject
                {
                    { NetworkData.OnRegisterResult.TAG_RESULT, resultSuccess },
                    { NetworkData.OnRegisterResult.TAG_MESSAGE, resultMSG }
                };
                serverApplication.networkManager.netS2CProxy.OnRegisterResult(targetClient.clientHID, RmiContext.ReliableSend, 1, resultMSG);
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserRegister RequestResult : " + resultObject.ToString());
			} catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserRegister ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

		public void OnUserStatus(HostID hostID, String id)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserStatus");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_id : " + id);
            try
			{
                string resultMSG = String.Empty;
                bool resultSuccess = serverApplication.databaseManager.RequestUserStatus(id, out NetworkData.UserData resultUser, ref resultMSG);
                serverApplication.networkManager.netS2CProxy.OnStatusResult(targetClient.clientHID, RmiContext.ReliableSend, 1, resultUser.userNumber, resultUser.userID, resultUser.userNick, resultUser.userRank, resultUser.userMoney, resultUser.userCash, resultUser.userWeapon, resultUser.userSkin, -2, resultMSG);
			}
			catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnUserStatus ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

		public void OnSoloQueue(HostID hostID, int weapon, int skin)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnSoloQueue");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_weapon : " + weapon + "\n_skin : " + skin);
            try
			{
				targetClient.clientUser.userWeapon = weapon;
				targetClient.clientUser.userSkin = skin;
				soloGameQueue.Remove(targetClient);
				teamGameQueue.Remove(targetClient);
				soloGameQueue.Add(targetClient);
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
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnTeamQueue");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_weapon : " + weapon + "\n_skin : " + skin);
            try
			{
				targetClient.clientUser.userWeapon = weapon;
				targetClient.clientUser.userSkin = skin;
				soloGameQueue.Remove(targetClient);
				teamGameQueue.Remove(targetClient);
				teamGameQueue.Add(targetClient);
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
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnQuitQueue");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID);
            try
			{
				soloGameQueue.Remove(targetClient);
				teamGameQueue.Remove(targetClient);
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
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameInfo");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room);
            try
            {
                GameRoom targetRoom = (GameRoom) serverGameList[room];

                if (targetRoom == null)
                {
                    serverApplication.networkManager.netS2CProxy.OnGameInfo(hostID, RmiContext.ReliableSend, -1, -1, -1, String.Empty);
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
                serverApplication.networkManager.netS2CProxy.OnGameInfo(hostID, RmiContext.ReliableSend, 1, targetRoom.gameNumber, (int) targetRoom.gameType, userArray.ToString());
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameInfo ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }

		public void OnGameUserActionReady(HostID hostID, int room)
		{
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionReady");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room);
            try
			{
                if (targetClient == null || ((GameRoom) serverGameList[room]) == null)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionReady NO TARGET");
                    return;
                }
                ((GameRoom) serverGameList[room]).OnGameUserReady(targetClient);
			}
			catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionReady ERROR");
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
			}
		}

        public void OnGameUserActionDamage(HostID hostID, int room, int num, int amount, String option)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionDamage");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room + "\n_num : " + num + "\n_amount : " + amount + "\n_option : " + option);
            try
            {
                if (targetClient == null || ((GameRoom) serverGameList[room]) == null)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionDamage NO TARGET");
                    return;
                }
                ((GameRoom) serverGameList[room]).OnGameUserDamage(targetClient, num, amount, option);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionDamage ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }

        public void OnGameUserActionObject(HostID hostID, int room, int num, int amount)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionObject");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room + "\n_num : " + num + "\n_amount : " + amount);
            try
            {
                if (targetClient == null || ((GameRoom) serverGameList[room]) == null)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionObject NO TARGET");
                    return;
                }
                ((GameRoom) serverGameList[room]).OnGameUserObject(targetClient, num, amount);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionObject ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }

        public void OnGameUserActionItem(HostID hostID, int room, int type, int num)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionItem");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_room : " + room + "\n_type : " + type + "\n_num : " + num);
            try
            {
                if (targetClient == null || ((GameRoom) serverGameList[room]) == null)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionItem NO TARGET");
                    return;
                }
                ((GameRoom) serverGameList[room]).OnGameUserItem(targetClient, type,  num);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserActionItem ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
            }
        }

        public void OnGameUserMove(HostID hostID, int gameRoom, string data)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserMove");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_data : " + data);
            try
            {
                GameRoom targetRoom = (GameRoom) serverGameList[gameRoom];
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

        public void OnGameUserSync(HostID hostID, int gameRoom, string data)
        {
            serverUserList.TryGetValue(hostID, out NetworkData.ClientData targetClient);
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "OnGameUserSync");
            if (DETAIL_LOG) serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "_hostID : " + hostID + "\n_data : " + data);
            try
            {
                GameRoom targetRoom = (GameRoom) serverGameList[gameRoom];
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
