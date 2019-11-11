using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace MSB_SERVER
{
    public partial class ServerManager
    {
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
            public List<NetworkData.ClientData> clientList;
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
            public int gameBlueKill;
            public int gameBlueDeath;
            public int gameBlueScore;
            public int gameRedKill;
            public int gameRedDeath;
            public int gameRedScore;
            public int[] gameObjects = { gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth };
            public int[] gameHealPacks = { 1, 1, 1 };
            public int[] gamePointPacks = { 1, 1, 1 };

            public GameRoom(int number, GAME_TYPE type, GAME_STATE state)
            {
                gameNumber = number;
                gameType = type;
                gameState = state;
                clientList = new List<NetworkData.ClientData>();
                clientBlueList = new List<NetworkData.ClientData>();
                clientRedList = new List<NetworkData.ClientData>();
            }

            public void OnGameUserReady(NetworkData.ClientData userClient)
            {
                userClient.currentReady = true;

                bool isEveryPlayerReady = true;

                if (readyCheckThread == null)
                {
                    readyCheckThread = new Thread(DoReadyCheck);
                    readyCheckThread.Start();
                }

                try
                {
                    JArray readyData = new JArray();
                    foreach (NetworkData.ClientData clientData in clientList)
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

                    foreach (NetworkData.ClientData clientData in clientList)
                    {
                        NetworkGate.OnGameStatusReady(clientData.clientHID, readyData.ToString());
                    }
                } catch (Exception e)
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", "G]OnGameUserReady ERROR");
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_NETWORK, "ServerManager", e.ToString());
                }

                if (isEveryPlayerReady && gameThread == null)
                {
                    gameThread = new Thread(DoGameStart);
                    gameThread.Start();
                }
            }

            public void OnGameUserDamage(NetworkData.ClientData userClient, int dataTarget, int dataAmount, string dataOption)
            {
                try
                {
                    if (gameState != GAME_STATE.STATE_INGAME) return;

                    NetworkData.ClientData targetClient = null;
                    foreach (NetworkData.ClientData clientData in clientList)
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

                    foreach (NetworkData.ClientData clientData in clientList)
                    {
                        NetworkGate.OnGameEventDamage(clientData.clientHID, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataAmount, dataOption);
                        NetworkGate.OnGameEventHealth(clientData.clientHID, targetClient.clientUser.userNumber, targetClient.gameHealth);
                    }

                    if (targetClient.gameHealth <= 0)
                    {
                        foreach (NetworkData.ClientData clientData in clientList)
                        {
                            NetworkGate.OnGameEventKill(clientData.clientHID, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataOption);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 0, userClient.clientUser.userNick + "KILLED " + targetClient.clientUser.userNick);
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

                        foreach (NetworkData.ClientData clientData in clientList)
                        {
                            NetworkGate.OnGameStatusScore(clientData.clientHID, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
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

                    foreach (NetworkData.ClientData clientData in clientList)
                    {
                        NetworkGate.OnGameEventObject(clientData.clientHID, dataTarget, gameObjects[dataTarget]);
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

                        foreach (NetworkData.ClientData clientData in clientList)
                        {
                            NetworkGate.OnGameEventItem(clientData.clientHID, type, dataTarget, userClient.clientUser.userNumber);
                            NetworkGate.OnGameStatusScore(clientData.clientHID, gameBlueKill, gameBlueDeath, gameBlueKill * 2 + gameBlueScore, gameRedKill, gameRedDeath, gameRedKill * 2 + gameRedScore);
                        }

                        gamePointPacks[dataTarget] = gamePointSpawn;
                    }

                    if (type == 1) // HEALPACK ITEM
                    {
                        if (dataTarget >= gameHealPacks.Length) return;
                        if (gameHealPacks[dataTarget] > 0) return;

                        userClient.gameHealth += gameHealPackValue;
                        if (userClient.gameHealth > 100) userClient.gameHealth = 100;

                        foreach (NetworkData.ClientData clientData in clientList)
                        {
                            NetworkGate.OnGameEventItem(clientData.clientHID, type, dataTarget, userClient.clientUser.userNumber);
                            NetworkGate.OnGameEventHealth(clientData.clientHID, userClient.clientUser.userNumber, userClient.gameHealth);
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
                if (GetInstance().MODULE_ROOM_STOP_FLAG) return;
                Thread.Sleep(1000);
                if (gameState != GAME_STATE.STATE_READY) return;
                if (GetInstance().MODULE_ROOM_STOP_FLAG) return;
                Thread.Sleep(1000);
                if (gameState != GAME_STATE.STATE_READY) return;
                if (GetInstance().MODULE_ROOM_STOP_FLAG) return;
                Thread.Sleep(1000);
                if (gameState != GAME_STATE.STATE_READY) return;
                if (GetInstance().MODULE_ROOM_STOP_FLAG) return;
                Thread.Sleep(1000);
                if (gameState != GAME_STATE.STATE_READY) return;
                if (GetInstance().MODULE_ROOM_STOP_FLAG) return;
                gameThread = new Thread(DoGameStart);
                gameThread.Start();
            }

            private void DoGameStart()
            {
                gameState = GAME_STATE.STATE_INGAME;

                foreach (NetworkData.ClientData clientData in clientList)
                {
                    clientData.gameDeath = 0;
                    clientData.gameKill = 0;
                    clientData.gameRespawn = 0;
                    clientData.gameHealth = 100;
                }

                for (int countDown = 3; countDown >= 0; countDown --)
                {
                    if (GetInstance().MODULE_ROOM_STOP_FLAG)
                    {
                        return;
                    }

                    foreach (NetworkData.ClientData clientData in clientList)
                    {
                        NetworkGate.OnGameStatusCountdown(clientData.clientHID, countDown);
                    }

                    if (countDown > 0)
                    {
                        Thread.Sleep(1000);
                    }
                }

                for (int remainGameTime = gameTime; remainGameTime >= 0; remainGameTime--)
                {
                    if (GetInstance().MODULE_ROOM_STOP_FLAG)
                    {
                        return;
                    }

                    foreach (NetworkData.ClientData clientData in clientList)
                    {
                        NetworkGate.OnGameStatusTime(clientData.clientHID, remainGameTime);
                        
                        if (clientData.gameRespawn > 0)
                        {
                            clientData.gameRespawn--;
                            clientData.gameHealth = 100;

                            foreach (NetworkData.ClientData cData in clientList)
                            {
                                NetworkGate.OnGameEventRespawn(cData.clientHID, clientData.clientUser.userNumber, clientData.gameRespawn);
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
                                foreach (NetworkData.ClientData clientData in clientList)
                                {
                                    NetworkGate.OnGameEventItem(clientData.clientHID, 1, index, 0);
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
                                foreach (NetworkData.ClientData clientData in clientList)
                                {
                                    NetworkGate.OnGameEventItem(clientData.clientHID, 0, index, 0);
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

                foreach (NetworkData.ClientData clientData in clientList)
                {
                    clientData.currentReady = false;
                    clientData.currentGame = null;
                    NetworkGate.OnGameResult(clientData.clientHID, string.Empty);
                }

                gameState = GAME_STATE.STATE_CLEAR;
            }
        }
    }
}