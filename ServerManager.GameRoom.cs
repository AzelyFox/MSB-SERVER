using System;
using System.Collections.Generic;
using System.Linq;
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
            public int gameDatabaseIndex = 0;
            public readonly GAME_TYPE gameType;
            public GAME_STATE gameState;
            public List<NetworkData.ClientData> clientList;
            public List<NetworkData.ClientData> clientBlueList;
            public List<NetworkData.ClientData> clientRedList;
            Thread gameThread;
            Thread readyCheckThread;

            public static int gameTime = 60;
            public static int gameHealPackValue = 30;
            public static int gameHealPackSpawn = 5;
            public static int gameUserSpawn = 5;
            public static int gameObjectHealth = 10;
            public static int gamePointValue = 1;
            public static int gamePointSpawn = 15;
            public static int gameMatchValue = 40;
            public int gameBlueKill;
            public int gameBlueDeath;
            public int gameBluePoint;
            public int gameBlueTotal;
            public int gameRedKill;
            public int gameRedDeath;
            public int gameRedPoint;
            public int gameRedTotal;
            int[] gameObjects = { gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth, gameObjectHealth };
            int[] gameHealPacks = { 1, 1, 1 };
            int[] gamePointPacks = { 1, 1, 1 };

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
                    foreach (NetworkData.ClientData clientData in clientList)
                    {
                        clientData.currentReady = true;
                        clientData.gameRespawn = 0;
                        clientData.gameHealth = 100;
                        clientData.gameKill = 0;
                        clientData.gameDeath = 0;
                        clientData.gameBonus = 0;
                        clientData.enduredDamage = 0;
                        clientData.lastKillTime = 0;
                        clientData.stunTime = 0;
                        clientData.stunGivenClient = null;
                        clientData.totalGivenDamage = 0;
                        clientData.totalTakenDamage = 0;
                        clientData.killHistory?.Clear();
                        clientData.killStreakHistory?.Clear();
                        clientData.assistHistory?.Clear();
                        clientData.medalHistory?.Clear();
                    }
                    
                    gameDatabaseIndex = DatabaseManager.GetInstance().saveGameStart(this);
                    
                    gameThread = new Thread(DoGameStart);
                    gameThread.Start();
                }
            }

            public void OnGameUserDamage(NetworkData.ClientData userClient, int dataTarget, int dataAmount, string dataOption)
            {
                try
                {
                    if (gameState != GAME_STATE.STATE_INGAME) return;
                    
                    long currentTimeStamp = (long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

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
                    targetClient.enduredDamage += dataAmount;
                    userClient.totalGivenDamage += dataAmount;
                    targetClient.totalTakenDamage += dataAmount;

                    foreach (NetworkData.ClientData clientData in clientList)
                    {
                        NetworkGate.OnGameEventDamage(clientData.clientHID, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataAmount, dataOption);
                        NetworkGate.OnGameEventHealth(clientData.clientHID, targetClient.clientUser.userNumber, targetClient.gameHealth);
                    }

                    if (targetClient.gameHealth <= 0)
                    {
                        if (userClient != targetClient)
                        {
                            userClient.gameKill++;
                            userClient.killHistory.Add(targetClient);
                            userClient.killStreakHistory.Add(targetClient);
                            userClient.gameBonus++;
                        }
                        targetClient.gameDeath++;
                        targetClient.lastKillTime = 0;
                        targetClient.enduredDamage = 0;
                        targetClient.killStreakHistory = new List<NetworkData.ClientData>();
                        targetClient.stunTime = 0;
                        targetClient.stunGivenClient = null;
                        targetClient.gameBonus--;
                        
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
                        gameBlueTotal = gameBlueKill + gameBluePoint;
                        gameRedTotal = gameRedKill + gameRedPoint;
                        
                        // User Client Killed Target Client Count
                        int UCKTC = userClient.killHistory.Count(client => client!=null && client == targetClient);
                        // Target Client Killed User Client Count
                        int TCKUC = targetClient.killHistory.Count(client => client!=null && client == userClient);
                        JObject killObject = new JObject();
                        killObject.Add("killMaker", userClient.clientUser.userNumber);
                        killObject.Add("killTarget", targetClient.clientUser.userNumber);
                        killObject.Add("killCount", UCKTC);
                        killObject.Add("deathCount", TCKUC);
                        
                        foreach (NetworkData.ClientData clientData in clientList)
                        {
                            NetworkGate.OnGameEventKill(clientData.clientHID, userClient.clientUser.userNumber, targetClient.clientUser.userNumber, dataOption);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 0, userClient.clientUser.userNick + "KILLED " + targetClient.clientUser.userNick);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 1, killObject.ToString());
                        }
                        
                        // CHECK MEDAL - FIRST KILL
                        int gameTotalKill = 0;
                        if (userClient != targetClient)
                        {
                            foreach (NetworkData.ClientData clientData in clientList)
                            {
                                gameTotalKill += clientData.gameKill;
                            }
                            if (gameTotalKill == 0)
                            {
                                // ACHIEVE userClient FIRST KILL MEDAL
                                userClient.gameBonus++;
                                int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, userClient.clientUser.userNumber, 1);
                                userClient.medalHistory.Add(medalIndex);
                                NetworkGate.OnGameStatusMessage(userClient.clientHID, 2, "1");
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] FIRST KILL : " + userClient.clientUser.userID);
                            }
                        }

                        // CHECK MEDAL - TIT FOR TAT
                        if (userClient != targetClient && targetClient.gameKill > 0)
                        {
                            if (targetClient.killStreakHistory != null && targetClient.killStreakHistory.Count > 0 &&
                                targetClient.killStreakHistory.Contains(userClient))
                            {
                                // ACHIEVE userClient TIT FOR TAT
                                userClient.gameBonus++;
                                int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, userClient.clientUser.userNumber, 3);
                                userClient.medalHistory.Add(medalIndex);
                                NetworkGate.OnGameStatusMessage(userClient.clientHID, 2, "3");
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] TIT FOR TAT : " + userClient.clientUser.userID);
                            }
                        }
                        
                        // CHECK MEDAL - KILL STREAK
                        if (userClient != targetClient && userClient.killStreakHistory != null && userClient.killStreakHistory.Count > 0)
                        {
                            if (userClient.lastKillTime + 5 > currentTimeStamp)
                            {
                                // ACHIEVE userClient KILL STREAK
                                userClient.gameBonus++;
                                int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, userClient.clientUser.userNumber, 2);
                                userClient.medalHistory.Add(medalIndex);
                                NetworkGate.OnGameStatusMessage(userClient.clientHID, 2, "2");
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] KILL STREAK : " + userClient.clientUser.userID);
                            }
                        }
                        
                        if (userClient != targetClient)
                        {
                            userClient.lastKillTime = currentTimeStamp;
                        }
                        
                        // CHECK MEDAL - ASSIST
                        if (userClient != targetClient && targetClient.stunTime > 0 && targetClient.stunGivenClient != null && targetClient.stunGivenClient != userClient)
                        {
                            foreach (NetworkData.ClientData clientData in clientList)
                            {
                                if (clientData.Equals(targetClient.stunGivenClient))
                                {
                                    // ACHIEVE clientData ASSIST
                                    userClient.gameBonus++;
                                    clientData.assistHistory.Add(targetClient);
                                    int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 6);
                                    clientData.medalHistory.Add(medalIndex);
                                    NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "6");
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] ASSIST : " + clientData.clientUser.userID);
                                }
                            }
                        }
                        
                        foreach (NetworkData.ClientData clientData in clientList)
                        {
                            NetworkGate.OnGameStatusScore(clientData.clientHID, gameBlueKill, gameBlueDeath, gameBlueTotal, gameRedKill, gameRedDeath, gameRedTotal);
                        }
                    }
                    else
                    {
                        // CHECK MEDAL - ADAMANT
                        if (targetClient.enduredDamage >= 200)
                        {
                            // ACHIEVE targetClient ADAMANT
                            targetClient.gameBonus++;
                            targetClient.enduredDamage -= 200;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, targetClient.clientUser.userNumber, 4);
                            targetClient.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(targetClient.clientHID, 2, "4");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] ADAMANT : " + targetClient.clientUser.userID);
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

                        userClient.gameBonus++;

                        foreach (NetworkData.ClientData clientData in clientBlueList)
                        {
                            if (userClient.clientUser.userNumber == clientData.clientUser.userNumber)
                            {
                                gameBluePoint += gamePointValue;
                                break;
                            }
                        }
                        foreach (NetworkData.ClientData clientData in clientRedList)
                        {
                            if (userClient.clientUser.userNumber == clientData.clientUser.userNumber)
                            {
                                gameRedPoint += gamePointValue;
                                break;
                            }
                        }

                        gameBlueTotal = gameBlueKill + gameBluePoint;
                        gameRedTotal = gameRedKill + gameRedPoint;
                        
                        foreach (NetworkData.ClientData clientData in clientList)
                        {
                            NetworkGate.OnGameEventItem(clientData.clientHID, type, dataTarget, userClient.clientUser.userNumber);
                            NetworkGate.OnGameStatusScore(clientData.clientHID, gameBlueKill, gameBlueDeath, gameBlueTotal, gameRedKill, gameRedDeath, gameRedTotal);
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

                int gameTotalDamage = 0;
                foreach (NetworkData.ClientData clientData in clientList)
                {
                    gameTotalDamage += clientData.totalGivenDamage;
                }
                
                // CHECK MEDAL - IM NOT MAD
                foreach (NetworkData.ClientData clientData in clientList)
                {
                    if (clientData.gameKill == 0)
                    {
                        // ACHIEVE clientData IM NOT MAD
                        int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 5);
                        clientData.medalHistory.Add(medalIndex);
                        NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "5");
                        serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] IM NOT MAD : " + clientData.clientUser.userID);
                    }
                }
                
                // CHECK MEDAL - ATTACK MEDAL
                foreach (NetworkData.ClientData clientData in clientList)
                {
                    if (gameType == GAME_TYPE.TYPE_SOLO)
                    {
                        if (clientData.totalGivenDamage >= gameTotalDamage/10 * 8)
                        {
                            // ACHIEVE clientData ATTACK GOLD
                            clientData.gameBonus += 4;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 9);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "9");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] ATTACK GOLD : " + clientData.clientUser.userID);
                        } else if (clientData.totalGivenDamage >= gameTotalDamage/10 * 7)
                        {
                            // ACHIEVE clientData ATTACK SILVER
                            clientData.gameBonus += 3;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 8);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "8");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] ATTACK SILVER : " + clientData.clientUser.userID);
                        } else if (clientData.totalGivenDamage >= gameTotalDamage/10 * 6)
                        {
                            // ACHIEVE clientData ATTACK BRONZE
                            clientData.gameBonus += 2;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 7);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "7");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] ATTACK BRONZE : " + clientData.clientUser.userID);
                        }
                    }
                    if (gameType == GAME_TYPE.TYPE_TEAM)
                    {
                        if (clientData.totalGivenDamage >= gameTotalDamage/10 * 7)
                        {
                            // ACHIEVE clientData ATTACK GOLD
                            clientData.gameBonus += 4;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 9);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "9");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] ATTACK GOLD : " + clientData.clientUser.userID);
                        } else if (clientData.totalGivenDamage >= gameTotalDamage/10 * 5)
                        {
                            // ACHIEVE clientData ATTACK SILVER
                            clientData.gameBonus += 3;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 8);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "8");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] ATTACK SILVER : " + clientData.clientUser.userID);
                        } else if (clientData.totalGivenDamage >= gameTotalDamage/10 * 3)
                        {
                            // ACHIEVE clientData ATTACK BRONZE
                            clientData.gameBonus += 2;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 7);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "7");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] ATTACK BRONZE : " + clientData.clientUser.userID);
                        }
                    }
                }
                
                // CHECK MEDAL - DEFENSE MEDAL
                foreach (NetworkData.ClientData clientData in clientList)
                {
                    if (gameType == GAME_TYPE.TYPE_SOLO)
                    {
                        if (clientData.totalTakenDamage >= gameTotalDamage/10 * 8)
                        {
                            // ACHIEVE clientData DEFENSE GOLD
                            clientData.gameBonus += 4;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 12);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "12");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] DEFENSE GOLD : " + clientData.clientUser.userID);
                        } else if (clientData.totalTakenDamage >= gameTotalDamage/10 * 7)
                        {
                            // ACHIEVE clientData DEFENSE SILVER
                            clientData.gameBonus += 3;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 11);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "11");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] DEFENSE SILVER : " + clientData.clientUser.userID);
                        } else if (clientData.totalTakenDamage >= gameTotalDamage/10 * 6)
                        {
                            // ACHIEVE clientData DEFENSE BRONZE
                            clientData.gameBonus += 2;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 10);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "10");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] DEFENSE BRONZE : " + clientData.clientUser.userID);
                        }
                    }
                    if (gameType == GAME_TYPE.TYPE_TEAM)
                    {
                        if (clientData.totalTakenDamage >= gameTotalDamage/10 * 7)
                        {
                            // ACHIEVE clientData DEFENSE GOLD
                            clientData.gameBonus += 4;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 12);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "12");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] DEFENSE GOLD : " + clientData.clientUser.userID);
                        } else if (clientData.totalTakenDamage >= gameTotalDamage/10 * 5)
                        {
                            // ACHIEVE clientData DEFENSE SILVER
                            clientData.gameBonus += 3;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 11);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "11");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] DEFENSE SILVER : " + clientData.clientUser.userID);
                        } else if (clientData.totalTakenDamage >= gameTotalDamage/10 * 3)
                        {
                            // ACHIEVE clientData DEFENSE BRONZE
                            clientData.gameBonus += 2;
                            int medalIndex = DatabaseManager.GetInstance().saveUserMedal(this, clientData.clientUser.userNumber, 10);
                            clientData.medalHistory.Add(medalIndex);
                            NetworkGate.OnGameStatusMessage(clientData.clientHID, 2, "10");
                            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_SYSTEM, "ServerManager", "G]MEDAL] DEFENSE BRONZE : " + clientData.clientUser.userID);
                        }
                    }
                }

                DatabaseManager.GetInstance().saveGameResult(this);
                
                // MM MODIFICATION
                if (gameBlueTotal == gameRedTotal)
                {
                    foreach (NetworkData.ClientData clientData in clientList)
                    {
                        DatabaseManager.GetInstance().saveUserRank(this, clientData.clientUser.userNumber, clientData.gameBonus);
                    }
                }
                if (gameBlueTotal > gameRedTotal)
                {
                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        DatabaseManager.GetInstance().saveUserRank(this, clientData.clientUser.userNumber, clientData.gameBonus + gameMatchValue);
                    }
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        DatabaseManager.GetInstance().saveUserRank(this, clientData.clientUser.userNumber, clientData.gameBonus - gameMatchValue);
                    }
                }
                if (gameBlueTotal < gameRedTotal)
                {
                    foreach (NetworkData.ClientData clientData in clientRedList)
                    {
                        DatabaseManager.GetInstance().saveUserRank(this, clientData.clientUser.userNumber, clientData.gameBonus + gameMatchValue);
                    }
                    foreach (NetworkData.ClientData clientData in clientBlueList)
                    {
                        DatabaseManager.GetInstance().saveUserRank(this, clientData.clientUser.userNumber, clientData.gameBonus - gameMatchValue);
                    }
                }
                
                Thread.Sleep(3000);

                foreach (NetworkData.ClientData clientData in clientList)
                {
                    clientData.currentReady = false;
                    clientData.currentGame = null;
                    NetworkGate.OnGameResult(clientData.clientHID, string.Empty);
                }

                gameState = GAME_STATE.STATE_CLEAR;

                serverApplication.serverManager.serverGameList.Remove(this);
            }
        }
    }
}