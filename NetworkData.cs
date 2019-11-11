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
		}

		public static class EventUserLogin
		{
			public const string TAG = "<UL>";
			public const string TAG_ID = "ID";
			public const string TAG_PW = "PW";
		}

		public static class EventUserRegister
		{
			public const string TAG = "<UR>";
			public const string TAG_ID = "ID";
			public const string TAG_PW = "PW";
			public const string TAG_NICK = "NICK";
		}

		public static class EventUserStatus
		{
			public const string TAG = "<US>";
			public const string TAG_ID = "ID";
		}

		public static class EventSoloQueue
		{
			public const string TAG = "<SQ>";
			public const string TAG_WEAPON = "WEAPON";
			public const string TAG_SKIN = "SKIN";
		}

		public static class EventTeamQueue
		{
			public const string TAG = "<TQ>";
			public const string TAG_WEAPON = "WEAPON";
			public const string TAG_SKIN = "SKIN";
		}

		public static class EventQuitQueue
		{
			public const string TAG = "<QQ>";
		}

        public static class EventGameInfo
        {
            public const string TAG = "<GI>";
            public const string TAG_ROOM = "ROOM";
        }

        public static class EventGameUserMove
		{
			public const string TAG = "<GUM>";
			public const string TAG_DATA = "DATA";
		}

		public static class EventGameUserSync
		{
			public const string TAG = "<GUS>";
			public const string TAG_DATA = "DATA";
		}

		public static class EventGameUserAction
		{
			public const string TAG = "<GUA>";
			public const string TAG_TYPE = "TYPE";
            public const string TAG_DATA = "DATA";
            public const string TYPE_READY = "READY";
            public const string TYPE_DAMAGE = "DAMAGE";
            public const string TYPE_OBJECT = "OBJECT";
            public const string TYPE_ITEM = "ITEM";
            public const string DATA_READY = "READY";
            public const string DATA_DAMAGE_TARGET = "TARGET";
            public const string DATA_DAMAGE_AMOUNT = "AMOUNT";
            public const string DATA_DAMAGE_OPTION = "OPTION";
            public const string DATA_OBJECT_TARGET = "TARGET";
            public const string DATA_OBJECT_AMOUNT = "AMOUNT";
            public const string DATA_ITEM_TARGET = "TARGET";
        }

		public static class OnLoginResult
		{
			public const string TAG = "[LR]";
			public const string TAG_RESULT = "RESULT";
			public const string TAG_NUM = "NUM";
			public const string TAG_ID = "ID";
			public const string TAG_NICK = "NICK";
			public const string TAG_RANK = "RANK";
			public const string TAG_MONEY = "MONEY";
			public const string TAG_CASH = "CASH";
			public const string TAG_WEAPON = "WEAPON";
			public const string TAG_SKIN = "SKIN";
            public const string TAG_GAME = "GAME";
			public const string TAG_MESSAGE = "MSG";
		}

		public static class OnRegisterResult
		{
			public const string TAG = "[RR]";
			public const string TAG_RESULT = "RESULT";
			public const string TAG_MESSAGE = "MSG";
		}

		public static class OnStatusResult
		{
			public const string TAG = "[SR]";
			public const string TAG_RESULT = "RESULT";
			public const string TAG_NUM = "NUM";
			public const string TAG_ID = "ID";
			public const string TAG_NICK = "NICK";
			public const string TAG_RANK = "RANK";
			public const string TAG_MONEY = "MONEY";
			public const string TAG_CASH = "CASH";
			public const string TAG_WEAPON = "WEAPON";
			public const string TAG_SKIN = "SKIN";
			public const string TAG_MESSAGE = "MSG";
		}

		public static class OnSoloMatched
		{
			public const string TAG = "[SM]";
			public const string TAG_RESULT = "RESULT";
			public const string TAG_ROOM = "ROOM";
			public const string TAG_MESSAGE = "MSG";
		}

		public static class OnTeamMatched
		{
			public const string TAG = "[TM]";
			public const string TAG_RESULT = "RESULT";
			public const string TAG_ROOM = "ROOM";
			public const string TAG_MESSAGE = "MSG";
		}

        public static class OnGameUserMove
        {
            public const string TAG = "[GUM]";
            public const string TAG_DATA = "DATA";
        }

        public static class OnGameUserSync
        {
            public const string TAG = "[GUS]";
            public const string TAG_DATA = "DATA";
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

		public static class OnGameStatus
		{
			public const string TAG = "[GS]";
			public const string TAG_TYPE = "TYPE";
			public const string TAG_DATA = "DATA";
            public const string TYPE_COUNTDOWN = "COUNTDOWN";
            public const string TYPE_TIME = "TIME";
            public const string TYPE_READY = "READY";
            public const string TYPE_SCORE = "SCORE";
            public const string TYPE_MESSAGE = "MESSAGE";
            public const string DATA_SCORE_BLUE_KILL = "BK";
            public const string DATA_SCORE_BLUE_DEATH = "BD";
            public const string DATA_SCORE_RED_KILL = "RK";
            public const string DATA_SCORE_RED_DEATH = "RD";
            public const string DATA_MESSAGE_MSG = "MSG";
            public const string DATA_MESSAGE_OPTION = "OPTION";
        }

        public static class OnGameEvent
        {
            public const string TAG = "[GE]";
            public const string TAG_TYPE = "TYPE";
            public const string TAG_DATA = "DATA";
            public const string TYPE_HEALTH = "HEALTH";
            public const string TYPE_DAMAGE = "DAMAGE";
            public const string TYPE_OBJECT = "OBJECT";
            public const string TYPE_ITEM = "ITEM";
            public const string TYPE_KILL = "KILL";
            public const string TYPE_RESPAWN = "RESPAWN";
            public const string TYPE_LEAVE = "LEAVE";
            public const string TYPE_JOIN = "JOIN";
            public const string DATA_HEALTH_NUM = "NUM";
            public const string DATA_HEALTH_AMOUNT = "HEALTH";
            public const string DATA_DAMAGE_FROM = "FROM";
            public const string DATA_DAMAGE_TO = "TO";
            public const string DATA_DAMAGE_AMOUNT = "AMOUNT";
            public const string DATA_DAMAGE_OPTION = "OPTION";
            public const string DATA_OBJECT_NUM = "NUM";
            public const string DATA_OBJECT_HEALTH = "HEALTH";
            public const string DATA_ITEM_NUM = "NUM";
            public const string DATA_ITEM_ACTION = "ACTION";
            public const string DATA_KILL_FROM = "FROM";
            public const string DATA_KILL_TO = "TO";
            public const string DATA_KILL_OPTION = "OPTION";
            public const string DATA_RESPAWN_USER = "USER";
            public const string DATA_RESPAWN_TIME = "TIME";
        }

        public static class OnGameResult
		{
			public const string TAG = "[GR]";
			public const string TAG_DATA = "DATA";
		}

	}
	
}
