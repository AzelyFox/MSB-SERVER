﻿




// Generated by PIDL compiler.
// Do not modify this file, but modify the source .pidl file.

using System;
using System.Net;

namespace MSBC2C
{
	internal class Proxy:Nettention.Proud.RmiProxy
	{
public bool OnGameMove(Nettention.Proud.HostID remote,Nettention.Proud.RmiContext rmiContext, int room, String data)
{
	Nettention.Proud.Message __msg=new Nettention.Proud.Message();
		__msg.SimplePacketMode = core.IsSimplePacketMode();
		Nettention.Proud.RmiID __msgid= Common.OnGameMove;
		__msg.Write(__msgid);
		Nettention.Proud.Marshaler.Write(__msg, room);
		Nettention.Proud.Marshaler.Write(__msg, data);
		
	Nettention.Proud.HostID[] __list = new Nettention.Proud.HostID[1];
	__list[0] = remote;
		
	return RmiSend(__list,rmiContext,__msg,
		RmiName_OnGameMove, Common.OnGameMove);
}

public bool OnGameMove(Nettention.Proud.HostID[] remotes,Nettention.Proud.RmiContext rmiContext, int room, String data)
{
	Nettention.Proud.Message __msg=new Nettention.Proud.Message();
__msg.SimplePacketMode = core.IsSimplePacketMode();
Nettention.Proud.RmiID __msgid= Common.OnGameMove;
__msg.Write(__msgid);
Nettention.Proud.Marshaler.Write(__msg, room);
Nettention.Proud.Marshaler.Write(__msg, data);
		
	return RmiSend(remotes,rmiContext,__msg,
		RmiName_OnGameMove, Common.OnGameMove);
}
public bool OnGameSync(Nettention.Proud.HostID remote,Nettention.Proud.RmiContext rmiContext, int room, String data)
{
	Nettention.Proud.Message __msg=new Nettention.Proud.Message();
		__msg.SimplePacketMode = core.IsSimplePacketMode();
		Nettention.Proud.RmiID __msgid= Common.OnGameSync;
		__msg.Write(__msgid);
		Nettention.Proud.Marshaler.Write(__msg, room);
		Nettention.Proud.Marshaler.Write(__msg, data);
		
	Nettention.Proud.HostID[] __list = new Nettention.Proud.HostID[1];
	__list[0] = remote;
		
	return RmiSend(__list,rmiContext,__msg,
		RmiName_OnGameSync, Common.OnGameSync);
}

public bool OnGameSync(Nettention.Proud.HostID[] remotes,Nettention.Proud.RmiContext rmiContext, int room, String data)
{
	Nettention.Proud.Message __msg=new Nettention.Proud.Message();
__msg.SimplePacketMode = core.IsSimplePacketMode();
Nettention.Proud.RmiID __msgid= Common.OnGameSync;
__msg.Write(__msgid);
Nettention.Proud.Marshaler.Write(__msg, room);
Nettention.Proud.Marshaler.Write(__msg, data);
		
	return RmiSend(remotes,rmiContext,__msg,
		RmiName_OnGameSync, Common.OnGameSync);
}
#if USE_RMI_NAME_STRING
// RMI name declaration.
// It is the unique pointer that indicates RMI name such as RMI profiler.
public const string RmiName_OnGameMove="OnGameMove";
public const string RmiName_OnGameSync="OnGameSync";
       
public const string RmiName_First = RmiName_OnGameMove;
#else
// RMI name declaration.
// It is the unique pointer that indicates RMI name such as RMI profiler.
public const string RmiName_OnGameMove="";
public const string RmiName_OnGameSync="";
       
public const string RmiName_First = "";
#endif
		public override Nettention.Proud.RmiID[] GetRmiIDList() { return Common.RmiIDList; } 
	}
}

