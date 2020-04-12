using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MSB_SERVER
{
	public class CommandManager
	{
		private static CommandManager INSTANCE;

		private readonly App serverApplication;
        
        private WebSocketServer controlServer = null;
        private Thread controlStatusThread;
        
        private static WebSocket serviceDEVXControl = null;
        private static Thread serviceDEVXMonitor;
        
        private bool MODULE_STOP_FLAG = false;

		private static class COMMAND_ACTION
        {
            public const string ACTION_START = "START";
            public const string ACTION_STOP = "STOP";
            public const string ACTION_STATUS = "STATUS";
			public const string ACTION_DEBUG = "DEBUG";
			public const string ACTION_NORMAL = "NORMAL";
            public const string ACTION_DETAIL = "DETAIL";
            public const string ACTION_CLEAR = "CLEAR";
            public const string ACTION_SYNC = "SYNC";
            public const string ACTION_INGAME = "INGAME";
            public const string ACTION_SCROLL = "SCROLL";
            public const string ACTION_SET_GAME = "SET";
            public const string ACTION_HELP = "?";
		}

		private static class COMMAND_TARGET
        {
            public const string START_MASTER = "MASTER";
            public const string START_DATABASE = "DATABASE";
            public const string STOP_MASTER = "MASTER";
            public const string STOP_DATABASE = "DATABASE";
            public const string STATUS_ALL = "ALL";
            public const string CLEAR_LOG_ALL = "ALL";
            public const string CLEAR_LOG_SYSTEM = "SYSTEM";
            public const string CLEAR_LOG_NETWORK = "NETWORK";
            public const string DEBUG_LOG_ON = "ON";
            public const string DEBUG_LOG_OFF = "OFF";
            public const string NORMAL_LOG_ON = "ON";
            public const string NORMAL_LOG_OFF = "OFF";
            public const string DETAIL_LOG_ON = "ON";
            public const string DETAIL_LOG_OFF = "OFF";
            public const string SYNC_TCP = "TCP";
            public const string SYNC_UDP = "UDP";
            public const string INGAME_TCP = "TCP";
            public const string INGAME_UDP = "UDP";
            public const string SCROLL_ON = "ON";
            public const string SCROLL_OFF = "OFF";
            public const string SET_GAME_HELP = "?";
            public const string SET_GAME_TIME = "GAMETIME";
            public const string SET_GAME_USER_RESPAWN = "RESPAWN";
            public const string SET_GAME_HEAL = "HEALVALUE";
            public const string SET_GAME_HEAL_RESPAWN = "HEALRESPAWN";
            public const string SET_GAME_OBJECT_HEALTH = "OBJECTVALUE";
            public const string SET_GAME_POINT_VALUE = "POINTVALUE";
            public const string SET_GAME_POINT_RESPAWN = "POINTRESPAWN";
		}

		private CommandManager()
		{
			serverApplication = (App) Application.Current;
		}

		public static CommandManager GetInstance()
		{
			if (INSTANCE == null) INSTANCE = new CommandManager();
			return INSTANCE;
		}

        public void StartController()
        {
            controlServer = new WebSocketServer(9983);
            controlServer.AddWebSocketService<RemoteCommand>("/RemoteCommand");
            controlServer.Start();
            
            MODULE_STOP_FLAG = false;
            serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "CommandManager", "REMOTE COMMAND 시작");
            
            if (controlStatusThread == null || !controlStatusThread.IsAlive)
            {
                controlStatusThread = new Thread(DoCommandStatus)
                {
                    Priority = ThreadPriority.Lowest
                };
                controlStatusThread.Start();
            }
            
            serviceDEVXControl = new WebSocket("ws://localhost:8888/MSBListener");
            serviceDEVXControl.Connect();
            if (serviceDEVXMonitor == null || !serviceDEVXMonitor.IsAlive)
            {
                serviceDEVXMonitor = new Thread(MonitorDEVX)
                {
                    Priority = ThreadPriority.Lowest
                };
                serviceDEVXMonitor.Start();
            }
        }

        public void StopController()
        {
            MODULE_STOP_FLAG = true;
            controlServer?.Stop();
        }
        
        private void DoCommandStatus()
        {
            serverApplication.graphicalManager.OnCommandModuleStatusChanged(true, true);
            while (true)
            {
                if (MODULE_STOP_FLAG)
                {
                    break;
                }

                if (controlServer != null && controlServer.IsListening)
                {
                    serverApplication.graphicalManager.OnCommandModuleStatusChanged(true, true);
                }
                else
                {
                    serverApplication.graphicalManager.OnCommandModuleStatusChanged(true, false);
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "CommandManager", "REMOTE COMMAND 연결 끊김");
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

        /**
         * type (int) : critical message : 2
         * message (string) : output message
         */
        public void OnCriticalMessage(string message)
        {
            JObject resultMessage = new JObject();
            resultMessage.Add("type", 2);
            resultMessage.Add("message", message);
            if (serviceDEVXControl != null && serviceDEVXControl.IsAlive) serviceDEVXControl.Send(message);
        }
        
        /**
         * type (int) : command result : 1
         * result 1 : command success
         * result -1 : invalid command
         * result -2 : error occurred
         * result -3 : cannot execute
         * message (string) : output message
         */
        public JObject ApplyCommand(string commandRaw)
		{
            JObject resultMessage = new JObject();
            resultMessage.Add("type", 1);
            
			if (string.IsNullOrEmpty(commandRaw))
			{
                resultMessage.Add("result", -1);
				return resultMessage;
			}
			if (!commandRaw.StartsWith("/"))
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "NOT COMMAND");
                resultMessage.Add("result", -1);
                return resultMessage;
			}

			try
			{
				var command = commandRaw.Substring(1).Split(' ');

				switch(command[0].ToUpper())
				{
					case COMMAND_ACTION.ACTION_HELP:
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "clear all/system/network\ndebug on/off\nnormal on/off\ndetail on/off\nsync tcp/udp\ningame tcp/udp\nscroll on/off\nset ?");
                        resultMessage.Add("result", 1);
                        resultMessage.Add("message", "clear all/system/network\ndebug on/off\nnormal on/off\ndetail on/off\nsync tcp/udp\ningame tcp/udp\nscroll on/off\nset ?");
                        return resultMessage;
						break;
                    case COMMAND_ACTION.ACTION_START:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.START_MASTER))
                            {
                                if (NetworkManager.IS_SERVER_RUNNING)
                                {
                                    resultMessage.Add("result", -3);
                                    resultMessage.Add("message", "SERVER ALREADY RUNNING");
                                    return resultMessage;
                                }
                                else
                                {
                                    serverApplication.networkManager.ServerRun("localhost", 9993);
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "SERVER STARTED IN 9993");
                                    return resultMessage;
                                }
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.START_DATABASE))
                            {
                                if (DatabaseManager.GetInstance().CURRENT_DB_CONNECTION)
                                {
                                    resultMessage.Add("result", -3);
                                    resultMessage.Add("message", "DB MODULE ALREADY RUNNING");
                                    return resultMessage;
                                }
                                else
                                {
                                    DatabaseManager.GetInstance().StartDatabase();
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "DB MODULE STARTED");
                                    return resultMessage;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_STOP:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.STOP_MASTER))
                            {
                                if (!NetworkManager.IS_SERVER_RUNNING)
                                {
                                    resultMessage.Add("result", -3);
                                    resultMessage.Add("message", "SERVER ALREADY STOPPED");
                                    return resultMessage;
                                }
                                else
                                {
                                    serverApplication.networkManager.ServerStop();
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "SERVER STOPPED");
                                    return resultMessage;
                                }
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.STOP_DATABASE))
                            {
                                if (!DatabaseManager.GetInstance().CURRENT_DB_CONNECTION)
                                {
                                    resultMessage.Add("result", -3);
                                    resultMessage.Add("message", "DB MODULE ALREADY STOPPED");
                                    return resultMessage;
                                }
                                else
                                {
                                    DatabaseManager.GetInstance().StopDatabase();
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "DB MODULE STOPPED");
                                    return resultMessage;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_STATUS:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.STATUS_ALL))
                            {
                                JObject resultData = new JObject();
                                resultData.Add("MASTER", NetworkManager.IS_SERVER_RUNNING ? "ON" : "OFF");
                                resultData.Add("SOLOMM", (serverApplication.serverManager.soloMatchMaker != null && serverApplication.serverManager.soloMatchMaker.IsAlive) ? "ON" : "OFF");
                                resultData.Add("TEAMMM", (serverApplication.serverManager.teamMatchMaker != null && serverApplication.serverManager.teamMatchMaker.IsAlive) ? "ON" : "OFF");
                                resultData.Add("DATABASE", serverApplication.databaseManager.CURRENT_DB_CONNECTION ? "ON" : "OFF");
                                resultData.Add("USERTOTAL", serverApplication.databaseManager.totalUser);
                                resultData.Add("USERLIVE", ServerManager.serverUserList != null ? ServerManager.serverUserList.Count : 0);
                                resultData.Add("SYNC_CLIENT", serverApplication.serverManager.SYNC_PROTOCOL_TCP ? "TCP" : "UDP");
                                resultData.Add("INGAME_CLIENT", serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP ? "TCP" : "UDP");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", resultData);
                                return resultMessage;
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_CLEAR:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.CLEAR_LOG_ALL))
                            {
                                serverApplication.logManager.ClearLog();
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "CLEARED LOG");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "CLEARED LOG");
                                return resultMessage;
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.CLEAR_LOG_SYSTEM))
                            {
                                serverApplication.logManager.ClearLog(1);
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "CLEARED SYSTEM LOG");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "CLEARED SYSTEM LOG");
                                return resultMessage;
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.CLEAR_LOG_NETWORK))
                            {
                                serverApplication.logManager.ClearLog(2);
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "CLEARED NETWORK LOG");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "CLEARED NETWORK LOG");
                                return resultMessage;
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_DEBUG:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.DEBUG_LOG_ON))
                            {
                                serverApplication.logManager.SAVE_DEBUG_LOGS = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "DEBUG LEVEL PRINT ON");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "DEBUG LEVEL PRINT ON");
                                return resultMessage;
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.DEBUG_LOG_OFF))
                            {
                                serverApplication.logManager.SAVE_DEBUG_LOGS = false;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "DEBUG LEVEL PRINT OFF");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "DEBUG LEVEL PRINT OFF");
                                return resultMessage;
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_NORMAL:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.NORMAL_LOG_ON))
                            {
                                serverApplication.logManager.SAVE_DEBUG_LOGS = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "NORMAL LEVEL PRINT ON");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "NORMAL LEVEL PRINT ON");
                                return resultMessage;
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.NORMAL_LOG_OFF))
                            {
                                serverApplication.logManager.SAVE_DEBUG_LOGS = false;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "NORMAL LEVEL PRINT OFF");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "NORMAL LEVEL PRINT OFF");
                                return resultMessage;
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_DETAIL:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.DETAIL_LOG_ON))
                            {
                                serverApplication.serverManager.DETAIL_LOG = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "DETAIL PRINT ON");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "DETAIL LEVEL PRINT ON");
                                return resultMessage;
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.DETAIL_LOG_OFF))
                            {
                                serverApplication.serverManager.DETAIL_LOG = false;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "DETAIL PRINT OFF");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "DETAIL LEVEL PRINT OFF");
                                return resultMessage;
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_SYNC:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SYNC_TCP))
                            {
                                serverApplication.serverManager.SYNC_PROTOCOL_UDP = false;
                                serverApplication.serverManager.SYNC_PROTOCOL_TCP = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "CLIENT SYNC PROTOCOL IS TCP");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "CLIENT SYNC PROTOCOL IS NOW TCP");
                                return resultMessage;
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SYNC_UDP))
                            {
                                serverApplication.serverManager.SYNC_PROTOCOL_TCP = false;
                                serverApplication.serverManager.SYNC_PROTOCOL_UDP = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "CLIENT SYNC PROTOCOL IS UDP");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "CLIENT SYNC PROTOCOL IS NOW UDP");
                                return resultMessage;
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_INGAME:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.INGAME_TCP))
                            {
                                serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP = false;
                                serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "INGAME SYNC PROTOCOL IS TCP");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "INGAME SYNC PROTOCOL IS NOW TCP");
                                return resultMessage;
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.INGAME_UDP))
                            {
                                serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP = false;
                                serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "INGAME SYNC PROTOCOL IS UDP");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "INGAME SYNC PROTOCOL IS NOW UDP");
                                return resultMessage;
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_SCROLL:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SCROLL_ON))
                            {
                                serverApplication.logManager.SCROLL_TO_END = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "AUTO SCROLL ON");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "LOG AUTO SCROLL ON");
                                return resultMessage;
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SCROLL_OFF))
                            {
                                serverApplication.logManager.SCROLL_TO_END = false;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "AUTO SCROLL OFF");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", "LOG AUTO SCROLL OFF");
                                return resultMessage;
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    case COMMAND_ACTION.ACTION_SET_GAME:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_HELP))
                            {
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw,
                                    COMMAND_TARGET.SET_GAME_TIME + " : GAME PLAYTIME\n" +
                                    COMMAND_TARGET.SET_GAME_USER_RESPAWN + " : USER SPAWN TIME\n" +
                                    COMMAND_TARGET.SET_GAME_HEAL + " : HEALPACK AMOUNT\n" +
                                    COMMAND_TARGET.SET_GAME_HEAL_RESPAWN + " : HEALPACK SPAWN TIME\n" +
                                    COMMAND_TARGET.SET_GAME_OBJECT_HEALTH + " : OBJECT HEALTH\n" +
                                    COMMAND_TARGET.SET_GAME_POINT_VALUE + " : SCORE AMOUNT\n" +
                                    COMMAND_TARGET.SET_GAME_POINT_RESPAWN + " : SCORE SPAWN TIME");
                                resultMessage.Add("result", 1);
                                resultMessage.Add("message", COMMAND_TARGET.SET_GAME_TIME + " : GAME PLAYTIME\n" +
                                                             COMMAND_TARGET.SET_GAME_USER_RESPAWN + " : USER SPAWN TIME\n" +
                                                             COMMAND_TARGET.SET_GAME_HEAL + " : HEALPACK AMOUNT\n" +
                                                             COMMAND_TARGET.SET_GAME_HEAL_RESPAWN + " : HEALPACK SPAWN TIME\n" +
                                                             COMMAND_TARGET.SET_GAME_OBJECT_HEALTH + " : OBJECT HEALTH\n" +
                                                             COMMAND_TARGET.SET_GAME_POINT_VALUE + " : SCORE AMOUNT\n" +
                                                             COMMAND_TARGET.SET_GAME_POINT_RESPAWN + " : SCORE SPAWN TIME");
                                return resultMessage;
                            }
                            
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_TIME))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameTime = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "게임 플레이타임 : " + inputValue + " SEC");
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "GAME PLAYTIME IS NOW " + inputValue + " SEC");
                                    return resultMessage;
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_USER_RESPAWN))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameUserSpawn = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "유저 리스폰타임 : " + inputValue + " SEC");
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "USER SPAWN TIME IS NOW " + inputValue + " SEC");
                                    return resultMessage;
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_HEAL))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameHealPackValue = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "힐팩 회복용량 : " + inputValue);
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "HEALPACK AMOUNT IS NOW " + inputValue);
                                    return resultMessage;
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_HEAL_RESPAWN))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameHealPackSpawn = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "힐팩 스폰시간 : " + inputValue + " SEC");
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "HEALPACK SPAWN TIME IS NOW " + inputValue + " SEC");
                                    return resultMessage;
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_OBJECT_HEALTH))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameObjectHealth = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "오브젝트 체력 : " + inputValue);
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "OBJECT HEALTH IS NOW " + inputValue);
                                    return resultMessage;
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_POINT_VALUE))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gamePointValue = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "스코어 증가점수 : " + inputValue);
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "SCORE AMOUNT IS NOW " + inputValue);
                                    return resultMessage;
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_POINT_RESPAWN))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gamePointSpawn = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "스코어 스폰시간 : " + inputValue + " SEC");
                                    resultMessage.Add("result", 1);
                                    resultMessage.Add("message", "SCORE SPAWN TIME IS NOW " + inputValue + " SEC");
                                    return resultMessage;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            resultMessage.Add("result", -2);
                            resultMessage.Add("message", e.ToString());
                            return resultMessage;
                        }
                        break;
                    default:
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "NOT COMMAND");
                        resultMessage.Add("result", -1);
                        return resultMessage;
						break;
				}
			} catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "ERROR : " + e);
                resultMessage.Add("result", -2);
                resultMessage.Add("message", e.ToString());
                return resultMessage;
			}

            return resultMessage;
        }
        
        private void MonitorDEVX()
        {
            while (true)
            {
                if (MODULE_STOP_FLAG) break;
                if (serviceDEVXControl == null) continue;

                if (serviceDEVXControl != null)
                {
                    if (!serviceDEVXControl.IsAlive)
                    {
                        serviceDEVXControl.Connect();
                        serverApplication.graphicalManager.OnCommandModuleStatusChanged(true, false);
                    }
                }
                else
                {
                    serverApplication.graphicalManager.OnCommandModuleStatusChanged(true, false);
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "CommandManager", "DEVX LISTENER 초기화 실패");
                }

                Thread.Sleep(1000);
            }
        }
        
        private class RemoteCommand : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                string commandRaw = e.Data;
                try
                {
                    LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "REMOTE CONTROL", commandRaw.Trim());
                    JObject commandResult = GetInstance().ApplyCommand(commandRaw.Trim());
                    serviceDEVXControl?.Send(commandResult.ToString());
                }
                catch (Exception exception)
                {
                    LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "REMOTE CONTROL", exception.Message);
                }
                
            }
        }
	}
}
