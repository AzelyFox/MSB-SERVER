using System;
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
        private bool MODULE_STOP_FLAG = false;

		private static class COMMAND_ACTION
		{
			public const string ACTION_DEBUG = "DEBUG";
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
            public const string CLEAR_LOG_ALL = "ALL";
            public const string CLEAR_LOG_SYSTEM = "SYSTEM";
            public const string CLEAR_LOG_NETWORK = "NETWORK";
            public const string DEBUG_LOG_ON = "ON";
            public const string DEBUG_LOG_OFF = "OFF";
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
            } else if (controlStatusThread.IsAlive)
            {
                return;
            }
            controlStatusThread.Start();
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
        
		public void ApplyCommand(string commandRaw)
		{
			if (string.IsNullOrEmpty(commandRaw))
			{
				return;
			}
			if (!commandRaw.StartsWith("/"))
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "NOT COMMAND");
				return;
			}

			try
			{
				var command = commandRaw.Substring(1).Split(' ');

				switch(command[0].ToUpper())
				{
					case COMMAND_ACTION.ACTION_HELP:
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "clear all/system/network\ndebug on/off\ndetail on/off\nsync tcp/udp\ningame tcp/udp\nscroll on/off\nset ?");
						break;
                    case COMMAND_ACTION.ACTION_CLEAR:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.CLEAR_LOG_ALL))
                            {
                                serverApplication.logManager.ClearLog();
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "CLEARED LOG");
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.CLEAR_LOG_SYSTEM))
                            {
                                serverApplication.logManager.ClearLog(1);
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "CLEARED SYSTEM LOG");
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.CLEAR_LOG_NETWORK))
                            {
                                serverApplication.logManager.ClearLog(2);
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "CLEARED NETWORK LOG");
                            }
                        } catch (Exception)
                        {

                        }
                        break;
                    case COMMAND_ACTION.ACTION_DEBUG:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.DEBUG_LOG_ON))
                            {
                                serverApplication.logManager.SAVE_DEBUG_LOGS = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "DEBUG PRINT ON");
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.DEBUG_LOG_OFF))
                            {
                                serverApplication.logManager.SAVE_DEBUG_LOGS = false;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "DEBUG PRINT OFF");
                            }
                        } catch (Exception)
                        {

                        }
                        break;
                    case COMMAND_ACTION.ACTION_DETAIL:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.DETAIL_LOG_ON))
                            {
                                serverApplication.serverManager.DETAIL_LOG = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "DETAIL PRINT ON");
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.DETAIL_LOG_OFF))
                            {
                                serverApplication.serverManager.DETAIL_LOG = false;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "DETAIL PRINT OFF");
                            }
                        }
                        catch (Exception)
                        {

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
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SYNC_UDP))
                            {
                                serverApplication.serverManager.SYNC_PROTOCOL_TCP = false;
                                serverApplication.serverManager.SYNC_PROTOCOL_UDP = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "CLIENT SYNC PROTOCOL IS UDP");
                            }
                        }
                        catch (Exception)
                        {

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
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.INGAME_UDP))
                            {
                                serverApplication.serverManager.INGAME_SYNC_PROTOCOL_TCP = false;
                                serverApplication.serverManager.INGAME_SYNC_PROTOCOL_UDP = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "INGAME SYNC PROTOCOL IS UDP");
                            }
                        }
                        catch (Exception)
                        {

                        }
                        break;
                    case COMMAND_ACTION.ACTION_SCROLL:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SCROLL_ON))
                            {
                                serverApplication.logManager.SCROLL_TO_END = true;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "AUTO SCROLL ON");
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SCROLL_OFF))
                            {
                                serverApplication.logManager.SCROLL_TO_END = false;
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "AUTO SCROLL OFF");
                            }
                        }
                        catch (Exception)
                        {

                        }
                        break;
                    case COMMAND_ACTION.ACTION_SET_GAME:
                        try
                        {
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_HELP))
                            {
                                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw,
                                    COMMAND_TARGET.SET_GAME_TIME + " : 게임 플레이타임\n" +
                                    COMMAND_TARGET.SET_GAME_USER_RESPAWN + " : 유저 리스폰시간\n" +
                                    COMMAND_TARGET.SET_GAME_HEAL + " : 힐팩 회복용량\n" +
                                    COMMAND_TARGET.SET_GAME_HEAL_RESPAWN + " : 힐팩 스폰시간\n" +
                                    COMMAND_TARGET.SET_GAME_OBJECT_HEALTH + " : 오브젝트 체력\n" +
                                    COMMAND_TARGET.SET_GAME_POINT_VALUE + " : 스코어 증가점수\n" +
                                    COMMAND_TARGET.SET_GAME_POINT_RESPAWN + " : 스코어 스폰시간");
                            }
                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_TIME))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameTime = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "게임 플레이타임 : " + inputValue + " SEC");
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_USER_RESPAWN))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameUserSpawn = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "유저 리스폰타임 : " + inputValue + " SEC");
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_HEAL))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameHealPackValue = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "힐팩 회복용량 : " + inputValue);
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_HEAL_RESPAWN))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameHealPackSpawn = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "힐팩 스폰시간 : " + inputValue + " SEC");
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_OBJECT_HEALTH))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gameObjectHealth = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "오브젝트 체력 : " + inputValue);
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_POINT_VALUE))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gamePointValue = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "스코어 증가점수 : " + inputValue);
                                }
                            }

                            if (command[1].ToUpper().Equals(COMMAND_TARGET.SET_GAME_POINT_RESPAWN))
                            {
                                var inputValue = int.Parse(command[2]);
                                if (inputValue >= 0)
                                {
                                    ServerManager.GameRoom.gamePointSpawn = inputValue;
                                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "스코어 스폰시간 : " + inputValue + " SEC");
                                }
                            }
                        }
                        catch (Exception)
                        {

                        }
                        break;
                    default:
						serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "NOT COMMAND");
						break;
				}
			} catch (Exception e)
			{
				serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, commandRaw, "ERROR : " + e);
			}
		}
        
        private class RemoteCommand : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                string commandRaw = e.Data;
                try
                {
                    LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "REMOTE CONTROL", commandRaw);
                    CommandManager.GetInstance().ApplyCommand(commandRaw);
                }
                catch (Exception exception)
                {
                    LogManager.GetInstance().NewLog(LogManager.LOG_LEVEL.LOG_CRITICAL, LogManager.LOG_TARGET.LOG_SYSTEM, "REMOTE CONTROL", exception.Message);
                }
                
            }
        }
	}
}
