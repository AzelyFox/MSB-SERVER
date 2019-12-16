using System.Collections.Generic;
using Nettention.Proud;

namespace MSB_SERVER
{
	public class NetworkData
	{
		public class UserData
		{
			public int userNumber;
			public string userID;
			public string userNick;
			public int userRank;
			public int userMoney;
			public int userCash;
			public int userWeapon;
			public int userSkin;

			public UserData(int _userNUM, string _userID, string _userNICK)
			{
				userNumber = _userNUM;
				userID = _userID;
				userNick = _userNICK;
				userRank = 0;
				userMoney = 0;
				userCash = 0;
				userWeapon = 0;
				userSkin = 0;
			}
		}

		public class ClientData
		{
			public UserData clientUser;
			public HostID clientHID;
			public ServerManager.GameRoom currentGame;
			public bool currentReady;
            public int gameRespawn = 0;
            public int gameHealth = 100;
            public int gameKill = 0;
            public int gameDeath = 0;
            public int gameBonus = 0;
            public int enduredDamage = 0;
            public long lastKillTime = 0;
            public int stunTime = 0;
            public ClientData stunGivenClient = null;
            public int totalGivenDamage = 0;
            public int totalTakenDamage = 0;
            public List<ClientData> killHistory = new List<ClientData>();
            public List<ClientData> assistHistory = new List<ClientData>();
            public List<ClientData> killStreakHistory = new List<ClientData>();
            public List<int> medalHistory = new List<int>();
		}

        public static class OnGameInfo
        {
            public const string TAG = "[GI]";
            public const string TAG_RESULT = "RESULT";
            public const string TAG_ROOM = "ROOM";
            public const string TAG_MODE = "MODE";
            public const string TAG_MESSAGE = "MSG";
            public const string TAG_USERS = "USERS";
            public const string TAG_USER_NUM = "NUM";
            public const string TAG_USER_ID = "ID";
            public const string TAG_USER_NICK = "NICK";
            public const string TAG_USER_RANK = "RANK";
            public const string TAG_USER_WEAPON = "WEAPON";
            public const string TAG_USER_SKIN = "SKIN";
        }

	}
	
}
