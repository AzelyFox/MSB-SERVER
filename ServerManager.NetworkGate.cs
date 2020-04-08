using System;
using Nettention.Proud;
using Newtonsoft.Json.Linq;

namespace MSB_SERVER
{
    public partial class ServerManager
    {
        public class NetworkGate
        {
            private static NetworkGate NETWORK_GATE;

            private NetworkGate()
            {
                
            }
            
            public static NetworkGate GetNetworkGate()
            {
                return NETWORK_GATE ??= new NetworkGate();
            }

            public void OnConnectResult(HostID hostID)
            {
                
            }

            public static void OnLoginResult(HostID hostID, int result, int num, string id, string nick, int rank, int money, int cash, int weapon, int skin, int game, string message)
            {
                JObject data = new JObject {{"result", result}, {"num", num}, {"id", id}, {"nick", nick}, {"rank", rank}, {"money", money}, {"cash", cash}, {"weapon", weapon}, {"skin", skin}, {"game", game}, {"message", message}};
                serverApplication.networkManager.netS2CProxy.OnLoginResult(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnStatusResult(HostID hostID, int result, int num, string id, string nick, int rank, int money, int cash, int weapon, int skin, int game, string message)
            {
                JObject data = new JObject {{"result", result}, {"num", num}, {"id", id}, {"nick", nick}, {"rank", rank}, {"money", money}, {"cash", cash}, {"weapon", weapon}, {"skin", skin}, {"game", game}, {"message", message}};
                serverApplication.networkManager.netS2CProxy.OnStatusResult(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnSystemResult(HostID hostID, int result, string type, string dataRaw)
            {
                JObject data = new JObject {{"result", result}, {"type", type}, {"data", dataRaw}};
                serverApplication.networkManager.netS2CProxy.OnSystemResult(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameMatched(HostID hostID, int result, int room, string message)
            {
                JObject data = new JObject {{"result", result}, {"room", room}, {"message", message}};
                serverApplication.networkManager.netS2CProxy.OnGameMatched(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameInfo(HostID hostID, int result, int room, int mode, string users)
            {
                JObject data = new JObject {{"result", result}, {"room", room}, {"mode", mode}, {"users", users}};
                serverApplication.networkManager.netS2CProxy.OnGameInfo(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameStatusCountdown(HostID hostID, int count)
            {
                JObject data = new JObject {{"count", count}};
                serverApplication.networkManager.netS2CProxy.OnGameStatusCountdown(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameStatusTime(HostID hostID, int time)
            {
                JObject data = new JObject{{"time", time}};
                serverApplication.networkManager.netS2CProxy.OnGameStatusTime(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameStatusReady(HostID hostID, string readyData)
            {
                serverApplication.networkManager.netS2CProxy.OnGameStatusReady(hostID, RmiContext.ReliableSend, readyData);
            }

            public static void OnGameStatusScore(HostID hostID, int blueKill, int blueDeath, int bluePoint, int redKill, int redDeath, int redPoint)
            {
                JObject data = new JObject {{"blueKill", blueKill}, {"blueDeath", blueDeath}, {"bluePoint", bluePoint}, {"redKill", redKill}, {"redDeath", redDeath}, {"redPoint", redPoint}};
                serverApplication.networkManager.netS2CProxy.OnGameStatusScore(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameStatusMessage(HostID hostID, int type, string message)
            {
                JObject data = new JObject {{"type", type}, {"message", message}};
                serverApplication.networkManager.netS2CProxy.OnGameStatusMessage(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameEventHealth(HostID hostID, int num, int health)
            {
                JObject data = new JObject {{"num", num}, {"health", health}};
                serverApplication.networkManager.netS2CProxy.OnGameEventHealth(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameEventDamage(HostID hostID, int from, int to, int amount, string option)
            {
                JObject data = new JObject {{"from", from}, {"to", to}, {"amount", amount}, {"option", option}};
                serverApplication.networkManager.netS2CProxy.OnGameEventDamage(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameEventObject(HostID hostID, int num, int health)
            {
                JObject data = new JObject {{"num", num}, {"health", health}};
                serverApplication.networkManager.netS2CProxy.OnGameEventObject(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameEventItem(HostID hostID, int type, int num, int action)
            {
                JObject data = new JObject {{"type", type}, {"num", num}, {"action", action}};
                serverApplication.networkManager.netS2CProxy.OnGameEventItem(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameEventKill(HostID hostID, int from, int to, string option)
            {
                JObject data = new JObject {{"from", from}, {"to", to}, {"option", option}};
                serverApplication.networkManager.netS2CProxy.OnGameEventKill(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameEventRespawn(HostID hostID, int num, int time)
            {
                JObject data = new JObject {{"num", num}, {"time", time}};
                serverApplication.networkManager.netS2CProxy.OnGameEventRespawn(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void OnGameResult(HostID hostID, string resultData)
            {
                JObject data = new JObject {{"resultData", resultData}};
                serverApplication.networkManager.netS2CProxy.OnGameResult(hostID, RmiContext.ReliableSend, data.ToString());
            }

            public static void EventUserLogin(HostID hostID, string _data)
            {
                JObject data = JObject.Parse(_data);
                string _id = data.GetValue("id")?.ToString() ?? "";
                string _pw = data.GetValue("pw")?.ToString() ?? "";
                string _uuid = data.GetValue("uuid")?.ToString() ?? "";
                serverApplication.serverManager.OnUserLogin(hostID, _id, _pw, _uuid);
            }
            
            public static void EventUserStatus(HostID hostID, string _data)
            {
                JObject data = JObject.Parse(_data);
                string _id = data.GetValue("id")?.ToString() ?? "";
                serverApplication.serverManager.OnUserStatus(hostID, _id);
            }
            
            public static void EventUserSystem(HostID hostID, string _data)
            {
                JObject data = JObject.Parse(_data);
                string _type = data.GetValue("type")?.ToString() ?? String.Empty;
                string _id = data.GetValue("id")?.ToString() ?? "";
                string _nickname = data.GetValue("nickname")?.ToString() ?? "";
                int _index = data.GetValue("index")?.Value<int>()?? -1;
                if (_type.Equals("nick"))
                {
                    serverApplication.serverManager.OnUserSystemNick(hostID, _id, _nickname);
                }
                if (_type.Equals("rank"))
                {
                    serverApplication.serverManager.OnUserSystemRank(hostID, _id);
                }
                if (_type.Equals("medal"))
                {
                    serverApplication.serverManager.OnUserSystemMedal(hostID, _index);
                }
            }
            
            public static void EventGameQueue(HostID hostID, string _data)
            {
                JObject data = JObject.Parse(_data);
                int _mode = data.GetValue("mode").Value<int>();
                if (_mode == -1)
                {
                    serverApplication.serverManager.OnQuitQueue(hostID);
                    return;
                }
                int _weapon = data.GetValue("weapon").Value<int>();
                int _skin = data.GetValue("skin").Value<int>();
                if (_mode == 0)
                {
                    serverApplication.serverManager.OnGameSoloQueue(hostID, _weapon, _skin);
                }
                if (_mode == 1)
                {
                    serverApplication.serverManager.OnGameTeamQueue(hostID, _weapon, _skin);
                }
            }
            
            public static void EventGameInfo(HostID hostID, int _room, string _data)
            {
                serverApplication.serverManager.OnGameInfo(hostID, _room);
            }
            
            public static void EventGameActionReady(HostID hostID, int _room, string _data)
            {
                serverApplication.serverManager.OnGameUserActionReady(hostID, _room);
            }
            
            public static void EventGameActionDamage(HostID hostID, int _room, string _data)
            {
                JObject data = JObject.Parse(_data);
                int target = data.GetValue("target").Value<int>();
                int amount = data.GetValue("amount").Value<int>();
                string option = data.GetValue("option").ToString();
                serverApplication.serverManager.OnGameUserActionDamage(hostID, _room, target, amount, option);
            }
            
            public static void EventGameActionObject(HostID hostID, int _room, string _data)
            {
                JObject data = JObject.Parse(_data);
                int target = data.GetValue("target").Value<int>();
                int amount = data.GetValue("amount").Value<int>();
                serverApplication.serverManager.OnGameUserActionObject(hostID, _room, target, amount);
            }
            
            public static void EventGameActionItem(HostID hostID, int _room, string _data)
            {
                JObject data = JObject.Parse(_data);
                int type = data.GetValue("type").Value<int>();
                int target = data.GetValue("target").Value<int>();
                serverApplication.serverManager.OnGameUserActionItem(hostID, _room, type, target);
            }

            public static void EventGameUserMove(HostID hostID, int _room, string _data)
            {
                serverApplication.serverManager.OnGameUserMove(hostID, _room, _data);
            }
            
            public static void EventGameUserSync(HostID hostID, int _room, string _data)
            {
                serverApplication.serverManager.OnGameUserSync(hostID, _room, _data);
            }
            
        }
        
    }
}