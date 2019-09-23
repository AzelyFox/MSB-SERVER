using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Nettention.Proud;

namespace MSB_SERVER
{
	public class NetworkManager
	{
		private static NetworkManager INSTANCE;

		private static MSB_SERVER.App serverApplication;

		private static bool IS_SERVER_RUNNING;

		public DateTime serverStartTime;

		private IPAddress SERVER_IP;
		private int SERVER_PORT;

        public bool serverRunLoop;
        private NetServer networkServer;
        private Nettention.Proud.ThreadPool netWorkerThreadPool = new Nettention.Proud.ThreadPool(4);
        private Nettention.Proud.ThreadPool userWorkerThreadPool = new Nettention.Proud.ThreadPool(4);
        private System.Guid guidVersion = new System.Guid("{0x27ad1634,0x381e,0x4228,{0x98,0xa,0xda,0xc8,0xeb,0x5f,0x4e,0x83}}");

        internal MSBS2C.Proxy netS2CProxy = new MSBS2C.Proxy();
        internal MSBC2S.Stub netC2SStub = new MSBC2S.Stub();

		private NetworkManager()
		{
			serverApplication = (MSB_SERVER.App) Application.Current;
			IS_SERVER_RUNNING = false;
		}

		public static NetworkManager GetInstance()
		{
			if (INSTANCE == null) INSTANCE = new NetworkManager();
			return INSTANCE;
		}

		public class StateObject
		{
			public Socket workSocket = null;
			public const int bufferSize = 1024;
			public byte[] buffer = new byte[bufferSize];
			public StringBuilder stringBuilder = new StringBuilder();
		}

		public void ServerRun(string _SERVER_IP, int _SERVER_PORT)
		{
            try
            {
                SERVER_PORT = _SERVER_PORT;

                serverApplication.logManager.ClearLog();

                if (_SERVER_IP.Equals("localhost"))
                {
                    SERVER_IP = System.Net.IPAddress.Parse("127.0.0.1");
                }
                else
                {
                    SERVER_IP = System.Net.IPAddress.Parse(_SERVER_IP);
                }

                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "서버 시작 [" + SERVER_IP + ":" + SERVER_PORT + "]");

                if (networkServer != null)
                {
                    networkServer.Dispose();
                }
                networkServer = new NetServer();
                serverRunLoop = true;
                networkServer.AttachStub(netC2SStub);
                networkServer.AttachProxy(netS2CProxy);

                networkServer.ConnectionRequestHandler = (AddrPort clientAddr, ByteArray userData, ByteArray reply) =>
                {
                    reply = new ByteArray();
                    reply.Clear();
                    return true;
                };

                networkServer.ClientHackSuspectedHandler = (HostID clientID, HackType hackType) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Client Hack Suspected : " + clientID + ":" + hackType + "");
                };

                networkServer.ClientJoinHandler = (NetClientInfo clientInfo) =>
                {
                    serverApplication.serverManager.OnUserConnected(clientInfo.hostID);
                };

                networkServer.ClientLeaveHandler = (NetClientInfo clientInfo, ErrorInfo errorInfo, ByteArray comment) =>
                {
                    serverApplication.serverManager.OnUserDisconnected(clientInfo.hostID);
                };

                networkServer.ErrorHandler = (ErrorInfo errorInfo) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Error : " + errorInfo);
                };

                networkServer.WarningHandler = (ErrorInfo errorInfo) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Warning : " + errorInfo);
                };

                networkServer.ExceptionHandler = (Exception e) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Exception : " + e);
                };

                networkServer.InformationHandler = (ErrorInfo errorInfo) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Information : " + errorInfo);
                };

                networkServer.NoRmiProcessedHandler = (RmiID rmiID) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "NoRmiProcessed : " + rmiID);
                };

                networkServer.P2PGroupJoinMemberAckCompleteHandler = (HostID groupHostID, HostID memberHostID, ErrorType result) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "P2P Group Complete : " + groupHostID);
                };

                networkServer.TickHandler = (object context) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Tick : " + context);
                };

                networkServer.UserWorkerThreadBeginHandler = () =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "UserWorkerThread Start");
                };

                networkServer.UserWorkerThreadEndHandler = () =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "UserWorkerThread End");
                };

                StartServerParameter startParameter = new StartServerParameter();
                startParameter.protocolVersion = new Nettention.Proud.Guid(guidVersion);
                startParameter.tcpPorts = new IntArray();
                startParameter.tcpPorts.Add(SERVER_PORT);
                startParameter.serverAddrAtClient = "203.250.148.54";
                startParameter.localNicAddr = "203.250.148.54";
                startParameter.SetExternalNetWorkerThreadPool(netWorkerThreadPool);
                startParameter.SetExternalUserWorkerThreadPool(userWorkerThreadPool);

                InitializeListeners();

                networkServer.Start(startParameter);

                IS_SERVER_RUNNING = true;
                serverApplication.graphicalManager.OnServerStatusChanged(true, IS_SERVER_RUNNING);

                serverStartTime = DateTime.Now;

                serverApplication.logManager.StartLogger();
                serverApplication.databaseManager.StartDatabase();
                serverApplication.serverManager.StartQueue();
                serverApplication.serverManager.StartRoom();
                serverApplication.serverManager.StartStatus();
                serverApplication.statusManager.StartModules();
            } catch (Exception e)
            {
                ((MSB_SERVER.App) Application.Current).MSBUnhandledException(e, "NetworkManager");
            }
		}

		public void ServerStop()
		{
			IS_SERVER_RUNNING = false;
			serverApplication.graphicalManager.OnServerStatusChanged(false, IS_SERVER_RUNNING);

			serverApplication.statusManager.StopModules();
			serverApplication.serverManager.StopStatus();
			serverApplication.serverManager.StopQueue();
			serverApplication.serverManager.StopRoom();
			serverApplication.databaseManager.StopDatabase();
			serverApplication.logManager.StopLogger();

            networkServer.Dispose();
		}

        private void InitializeListeners()
        {
            netC2SStub.OnLoginRequest = OnUserLogin;
            netC2SStub.OnRegisterRequest = OnUserRegister;
            netC2SStub.OnStatusRequest = OnUserStatus;
            netC2SStub.OnSoloQueueRequest = OnSoloQueue;
            netC2SStub.OnTeamQueueRequest = OnTeamQueue;
            netC2SStub.OnQuitQueueRequest = OnQuitQueue;
            netC2SStub.OnGameInfoRequest = OnGameInfo;
            netC2SStub.OnGameActionReady = OnGameUserActionReady;
            netC2SStub.OnGameActionDamage = OnGameUserActionDamage;
            netC2SStub.OnGameActionObject = OnGameUserActionObject;
            netC2SStub.OnGameActionItem = OnGameUserActionItem;
            netC2SStub.OnGameUserMove = OnGameUserMove;
            netC2SStub.OnGameUserSync = OnGameUserSync;
        }

        private bool OnUserLogin(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, String id, String pw)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserLogin");
                serverApplication.serverManager.OnUserLogin(remote, id, pw);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserLogin ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnUserRegister(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, String id, String pw, String nick)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserRegister");
                serverApplication.serverManager.OnUserRegister(remote, id, pw, nick);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserRegister ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnUserStatus(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, String id)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserStatus");
                serverApplication.serverManager.OnUserStatus(remote, id);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserStatus ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnSoloQueue(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, int weapon, int skin)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnSoloQueue");
                serverApplication.serverManager.OnSoloQueue(remote, weapon, skin);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnSoloQueue ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnTeamQueue(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, int weapon, int skin)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnTeamQueue");
                serverApplication.serverManager.OnTeamQueue(remote, weapon, skin);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnTeamQueue ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnQuitQueue(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnQuitQueue");
                serverApplication.serverManager.OnQuitQueue(remote);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnQuitQueue ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnGameInfo(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, int room)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameInfo");
                serverApplication.serverManager.OnGameInfo(remote, room);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameInfo ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnGameUserActionReady(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, int room)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionReady");
                serverApplication.serverManager.OnGameUserActionReady(remote, room);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionReady ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnGameUserActionDamage(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, int room, int num, int amount, String option)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionDamage");
                serverApplication.serverManager.OnGameUserActionDamage(remote, room, num, amount, option);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionDamage ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnGameUserActionObject(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, int room, int num, int amount)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionObject");
                serverApplication.serverManager.OnGameUserActionObject(remote, room, num, amount);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionObject ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnGameUserActionItem(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, int room, int type, int num)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionItem");
                serverApplication.serverManager.OnGameUserActionItem(remote, room, type, num);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionItem ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnGameUserMove(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, int gameRoom, string data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserMove");
                serverApplication.serverManager.OnGameUserMove(remote, gameRoom, data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserMove ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private bool OnGameUserSync(Nettention.Proud.HostID remote, Nettention.Proud.RmiContext rmiContext, int gameRoom, string data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserSync");
                serverApplication.serverManager.OnGameUserSync(remote, gameRoom, data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserSync ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        public bool IsServerRunning()
		{
			return IS_SERVER_RUNNING;
		}

    }
}
