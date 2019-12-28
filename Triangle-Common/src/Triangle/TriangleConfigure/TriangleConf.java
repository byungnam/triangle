package Triangle.TriangleConfigure;

import java.util.ArrayList;
import java.util.List;

@SuppressWarnings("serial")
public class TriangleConf {

//	public static final String IP_ADDRESS = "61.33.38.198";
//	public static final String IP_ADDRESS = "192.168.30.3";
	public static final int PORT_toClient_LOGINSERVER = 20000;

	public static final int PORT_LOGINSERVER_COMMUNICATOR = 20002;
	public static final int PORT_DBSERVER_COMMUNICATOR = 20003;
	
	private static int PORT_GAMESERVER_One = 20010;
	
	
	public static final String ADDRESS_DBSERVER = "61.33.38.198";
	public static final String ADDRESS_GAMESERVER = "61.33.38.198";
	public static final String ADDRESS_LOGINSERVER = "61.33.38.198";
	
	public static final String AccountTable = "Triangle.Account.db";
	public static final String DataTable = "Triangle.Data.db";
//	public static final String NPCDataTable = "Triangle.NPCData.db";

	public static final String TestAccountTable = "Triangle.TestAccount.db";
	public static final String TestDataTable = "Triangle.TestData.db";
//	public static final String TestNPCDataTable = "Triangle.TestNPCData.db";
	
	public static final List<String> SERVER_NAMES = new ArrayList<String>() {
		{
			add("One");
		}
	};
	public static final List<String> SERVER_ADDRESSES = new ArrayList<String>() {
		{
			add("61.33.38.198");
		}
	};

	public static final List<Integer> SERVER_PORTS = new ArrayList<Integer>() {
		{
			add(PORT_GAMESERVER_One);
		}
	};

}
