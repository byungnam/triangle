package JUnitTest;

import static org.junit.Assert.assertTrue;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;
import java.util.Map;

import org.junit.After;
import org.junit.AfterClass;
import org.junit.Before;
import org.junit.BeforeClass;
import org.junit.Test;

import Triangle.DBServer.DBServer;
import Triangle.GameServer.GameServer;
import Triangle.LoginServer.LoginServer;
import Triangle.Protocol.GameProtocol.CreateUnit;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.TriangleConfigure.TriangleConf;
import Triangle.TriangleValues.TriangleValues;
import UnitTestUtils.UtilityFunctions;

public class DBServerTest {

	Socket socket_db;
	OutputStream os_db;
	InputStream is_db;

	static UtilityFunctions util;

	static int accountNumber = 1;
	static GameServer gamesvr;
	static DBServer dbsvr;
	static LoginServer loginsvr;
	static Map<Integer, String> unitNumberMap; // unitNum, unitName
	static Map<Integer, String> teamNumberMap; // teamNum, teamName
	static Map<Integer, Integer> regiments; // unitNum, teamNum
	static int accountUnitCount;
	static int[] teamUnitCount;

	static int lastUnitNumber;
	static int lastTeamNumber;

	@BeforeClass
	public static void before() throws Exception {
		dbsvr = new DBServer(false);
		dbsvr.init();
	}

	@AfterClass
	public static void after() throws Exception {
		dbsvr.shutdown();
	}

	@Before
	public void connect() throws Exception {
		socket_db = new Socket(TriangleConf.ADDRESS_DBSERVER, TriangleConf.PORT_DBSERVER_COMMUNICATOR);
		os_db = socket_db.getOutputStream();
		is_db = socket_db.getInputStream();
		
		Thread.sleep(100);
	}

	@After
	public void disconnect() throws Exception {
		is_db.close();
		os_db.close();
		socket_db.close();
	}

	@Test
	public void connectionTest() {
		String address = socket_db.getInetAddress().getHostAddress();
		assertTrue(address.equals(TriangleConf.ADDRESS_DBSERVER));
	}

	@Test
	public void CreateUnitTest() {
		int unitNum = lastUnitNumber;
		try {
			GameMessage.newBuilder().setGameMessageType(GameMessageType.CreateUnit).setAccountNumber(
					accountNumber).setCreateUnit(
					CreateUnit.newBuilder().setName("CreateUnit" + unitNum).setCls(
							TriangleValues.UnitClass.SOLDIER.getNumber())).build().writeDelimitedTo(
					os_db);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_db);

			assertTrue(recv.getResult());
		} catch (IOException e) {
			e.printStackTrace();
		}
	}
}
