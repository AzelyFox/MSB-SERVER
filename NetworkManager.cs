using System;
using System.Text;
using System.Windows;
using System.Net;
using System.Net.Sockets;
using Nettention.Proud;

namespace MSB_SERVER
{
	public class NetworkManager
	{
		private static NetworkManager INSTANCE;

		private static App serverApplication;

		private static bool IS_SERVER_RUNNING;

		public DateTime serverStartTime;

		private IPAddress SERVER_IP;
		private int SERVER_PORT;

        private NetServer networkServer;
        private ThreadPool netWorkerThreadPool = new ThreadPool(4);
        private ThreadPool userWorkerThreadPool = new ThreadPool(4);
        private System.Guid guidVersion = new System.Guid("{0x27ad1634,0x381e,0x4228,{0x98,0xa,0xda,0xc8,0xeb,0x5f,0x4e,0x83}}");

        internal MSBS2C.Proxy netS2CProxy = new MSBS2C.Proxy();
        internal MSBC2S.Stub netC2SStub = new MSBC2S.Stub();

		private NetworkManager()
		{
			serverApplication = (App) Application.Current;
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
                    SERVER_IP = IPAddress.Parse("127.0.0.1");
                }
                else
                {
                    SERVER_IP = IPAddress.Parse(_SERVER_IP);
                }

                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "서버 시작 [" + SERVER_IP + ":" + SERVER_PORT + "]");

                // ReSharper disable once RedundantCheckBeforeAssignment
                if (networkServer != null)
                {
                    networkServer = null;
                }
                networkServer = new NetServer();
                networkServer.AttachStub(netC2SStub);
                networkServer.AttachProxy(netS2CProxy);

                // ReSharper disable once RedundantAssignment
                networkServer.ConnectionRequestHandler = (clientAddr, userData, reply) =>
                {
                    reply = new ByteArray();
                    reply.Clear();
                    return true;
                };

                networkServer.ClientHackSuspectedHandler = (clientID, hackType) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Client Hack Suspected : " + clientID + ":" + hackType + "");
                };

                networkServer.ClientJoinHandler = clientInfo =>
                {
                    serverApplication.serverManager.OnUserConnected(clientInfo.hostID);
                };

                networkServer.ClientLeaveHandler = (clientInfo, errorInfo, comment) =>
                {
                    serverApplication.serverManager.OnUserDisconnected(clientInfo.hostID);
                };

                networkServer.ErrorHandler = errorInfo =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Error : " + errorInfo);
                };

                networkServer.WarningHandler = errorInfo =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Warning : " + errorInfo);
                };

                networkServer.ExceptionHandler = e =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Exception : " + e);
                };

                networkServer.InformationHandler = errorInfo =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "Information : " + errorInfo);
                };

                networkServer.NoRmiProcessedHandler = rmiID =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "NoRmiProcessed : " + rmiID);
                };

                networkServer.P2PGroupJoinMemberAckCompleteHandler = (groupHostID, memberHostID, result) =>
                {
                    serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_NORMAL, LogManager.LOG_TARGET.LOG_SYSTEM, "NetworkManager", "P2P Group Complete : " + groupHostID);
                };

                networkServer.TickHandler = context =>
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
                startParameter.serverAddrAtClient = "203.250.148.113";
                startParameter.localNicAddr = "203.250.148.113";
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
                ((App) Application.Current).MSBUnhandledException(e, "NetworkManager");
                ServerStop();
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

            //networkServer.Dispose();
            networkServer = null;
        }

        private void InitializeListeners()
        {
            netC2SStub.OnLoginRequest = OnUserLogin;
            netC2SStub.OnStatusRequest = OnUserStatus;
            netC2SStub.OnSystemRequest = OnSystemRequest;
            netC2SStub.OnGameQueueRequest = OnGameQueue;
            netC2SStub.OnGameInfoRequest = OnGameInfo;
            netC2SStub.OnGameActionReady = OnGameUserActionReady;
            netC2SStub.OnGameActionDamage = OnGameUserActionDamage;
            netC2SStub.OnGameActionObject = OnGameUserActionObject;
            netC2SStub.OnGameActionItem = OnGameUserActionItem;
            netC2SStub.OnGameUserMove = OnGameUserMove;
            netC2SStub.OnGameUserSync = OnGameUserSync;
        }
        
        private static bool OnUserLogin(HostID host, RmiContext rmiContext, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserLogin");
                ServerManager.NetworkGate.EventUserLogin(host, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserLogin ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnUserStatus(HostID host, RmiContext rmiContext, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserStatus");
                ServerManager.NetworkGate.EventUserStatus(host, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnUserStatus ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnSystemRequest(HostID host, RmiContext rmiContext, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnSystemRequest");
                ServerManager.NetworkGate.EventUserSystem(host, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnSystemRequest ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnGameQueue(HostID host, RmiContext rmiContext, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameQueue");
                ServerManager.NetworkGate.EventGameQueue(host, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameQueue ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnGameInfo(HostID host, RmiContext rmiContext, int _room, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameInfo");
                ServerManager.NetworkGate.EventGameInfo(host, _room, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameInfo ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnGameUserActionReady(HostID host, RmiContext rmiContext, int _room, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionReady");
                ServerManager.NetworkGate.EventGameActionReady(host, _room, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionReady ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnGameUserActionDamage(HostID host, RmiContext rmiContext, int _room, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionDamage");
                ServerManager.NetworkGate.EventGameActionDamage(host, _room, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionDamage ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnGameUserActionObject(HostID host, RmiContext rmiContext, int _room, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionObject");
                ServerManager.NetworkGate.EventGameActionObject(host, _room, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionObject ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnGameUserActionItem(HostID host, RmiContext rmiContext, int _room, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionItem");
                ServerManager.NetworkGate.EventGameActionItem(host, _room, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserActionItem ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnGameUserMove(HostID host, RmiContext rmiContext, int _room, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserMove");
                ServerManager.NetworkGate.EventGameUserMove(host, _room, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserMove ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        private static bool OnGameUserSync(HostID host, RmiContext rmiContext, int _room, string _data)
        {
            try
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserSync");
                ServerManager.NetworkGate.EventGameUserSync(host, _room, _data);
            }
            catch (Exception e)
            {
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", "OnGameUserSync ERROR");
                serverApplication.logManager.NewLog(LogManager.LOG_LEVEL.LOG_DEBUG, LogManager.LOG_TARGET.LOG_NETWORK, "NetworkManager", e.ToString());
            }
            return true;
        }

        public static bool IsServerRunning()
		{
			return IS_SERVER_RUNNING;
		}

    }
}
