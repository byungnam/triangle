package JUnitTest;

import static org.junit.Assert.assertTrue;
import static org.junit.Assert.fail;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import org.junit.After;
import org.junit.AfterClass;
import org.junit.Before;
import org.junit.BeforeClass;
import org.junit.Test;

import Triangle.DBServer.DBServer;
import Triangle.DataDB.DataDB;
import Triangle.GameServer.GameServer;
import Triangle.LoginServer.LoginServer;
import Triangle.Protocol.GameProtocol.CreateTactic;
import Triangle.Protocol.GameProtocol.CreateTeam;
import Triangle.Protocol.GameProtocol.CreateUnit;
import Triangle.Protocol.GameProtocol.DeleteTactic;
import Triangle.Protocol.GameProtocol.DeleteTeam;
import Triangle.Protocol.GameProtocol.DeleteUnit;
import Triangle.Protocol.GameProtocol.EditTactic;
import Triangle.Protocol.GameProtocol.EditTeam;
import Triangle.Protocol.GameProtocol.EditUnit;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.Protocol.GameProtocol.GetTactic;
import Triangle.Protocol.GameProtocol.GetTeamByAccountNumber;
import Triangle.Protocol.GameProtocol.GetUnitByAccountNumber;
import Triangle.Protocol.GameProtocol.GetUnitByTeamNumber;
import Triangle.Protocol.GameProtocol.GetUnitByUnitNumber;
import Triangle.Protocol.GameProtocol.ProtocolTactic;
import Triangle.Protocol.GameProtocol.ProtocolTeam;
import Triangle.Protocol.GameProtocol.ProtocolUnit;
import Triangle.Protocol.GameProtocol.RegisterUnit;
import Triangle.Protocol.GameProtocol.UnregisterUnit;
import Triangle.TriangleConfigure.TriangleConf;
import Triangle.TriangleValues.TriangleValues;
import Triangle.dbms.ResetTables;
import UnitTestUtils.UtilityFunctions;

public class GameServerTest {

	Socket socket_game;
	OutputStream os_game;
	InputStream is_game;

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

	static DataDB dataDB;

	@BeforeClass
	public static void before() throws Exception {
		new ResetTables(false, true);

		unitNumberMap = new HashMap<>();
		teamNumberMap = new HashMap<>();
		regiments = new HashMap<>();

		dataDB = DataDB.getUser();
		dataDB.open(TriangleValues.FOR_TEST);
//		dataDB.CreateUnit(accountNumber, "DummyZero0",
//				TriangleValues.UnitClass.SOLDIER.getNumber());
//		unitNumberMap.put(0, "DummyZero0");
		dataDB.CreateUnit(accountNumber, "TestUnit1",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(1, "TestUnit1");
		dataDB.CreateUnit(accountNumber, "TestUnit2",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(2, "TestUnit2");
		dataDB.CreateUnit(accountNumber, "TestUnit3",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(3, "TestUnit3");
		dataDB.CreateUnit(accountNumber, "TestUnit4",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(4, "TestUnit4");
		dataDB.CreateUnit(accountNumber, "DeleteUnit5",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(5, "DeleteUnit5");
		dataDB.CreateUnit(accountNumber, "EditUnit6",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(6, "EditUnit6");
		dataDB.CreateUnit(accountNumber, "RegisterUnit7",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(7, "RegisterUnit7");
		dataDB.CreateUnit(accountNumber, "UnregisterUnit8",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(8, "UnregisterUnit8");
		dataDB.CreateUnit(accountNumber, "TacticCreateUnit9",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(9, "TacticCreateUnit9");
		dataDB.CreateUnit(accountNumber, "TacticDeleteUnit10",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(10, "TacticDeleteUnit10");
		dataDB.CreateUnit(accountNumber, "TacticGetUnit11",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(11, "TacticGetUnit11");
		dataDB.CreateUnit(accountNumber, "TacticEditUnit12",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(12, "TacticEditUnit12");

		accountUnitCount = unitNumberMap.size();

		dataDB.CreateTactic(10);
		dataDB.EditTactic(1,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 1,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 10, 1);
		dataDB.CreateTactic(10);
		dataDB.EditTactic(2,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 2,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 10, 2);
		dataDB.CreateTactic(10);
		dataDB.EditTactic(3,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 3,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 10, 3);

		dataDB.CreateTactic(11);
		dataDB.EditTactic(1,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 10,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 11, 1);
		dataDB.CreateTactic(11);
		dataDB.EditTactic(2,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 20,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 11, 2);
		dataDB.CreateTactic(11);
		dataDB.EditTactic(3,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 30,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 11, 3);

		dataDB.CreateTactic(12);
		dataDB.EditTactic(1,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 15,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 12, 1);
		dataDB.CreateTactic(12);
		dataDB.EditTactic(2,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 25,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 12, 2);
		dataDB.CreateTactic(12);
		dataDB.EditTactic(3,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 35,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 12, 3);

//		dataDB.CreateTeam(accountNumber, "DummyTeamZero0");
//		teamNumberMap.put(0, "DummyTeamZero0");
		dataDB.CreateTeam(accountNumber, "TestTeam1");
		teamNumberMap.put(1, "TestTeam1");
		dataDB.CreateTeam(accountNumber, "TestTeam2");
		teamNumberMap.put(2, "TestTeam2");
		dataDB.CreateTeam(accountNumber, "DeleteTeam3");
		teamNumberMap.put(3, "DeleteTeam3");
		dataDB.CreateTeam(accountNumber, "EditTeam4");
		teamNumberMap.put(4, "EditTeam4");

		teamUnitCount = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

		dataDB.RegisterUnit(1, 1);
		regiments.put(1, 1);
		teamUnitCount[1]++;
		dataDB.RegisterUnit(1, 2);
		regiments.put(1, 2);
		teamUnitCount[2]++;
		dataDB.RegisterUnit(2, 1);
		regiments.put(2, 1);
		teamUnitCount[1]++;
		dataDB.RegisterUnit(3, 1);
		regiments.put(3, 1);
		teamUnitCount[1]++;
		dataDB.RegisterUnit(8, 2);
		regiments.put(8, 2);
		teamUnitCount[2]++;

		
		// dummy data
		int dummyAccount = 123;
		int dummyUnitNum = accountUnitCount + 1;
		dataDB.CreateUnit(dummyAccount, "DummyUnit" + (dummyUnitNum),
				TriangleValues.UnitClass.SOLDIER.getNumber());
		unitNumberMap.put(dummyUnitNum, "DummyUnit" + dummyUnitNum);

		int dummyTeamNum = teamNumberMap.size() + 1;
		dataDB.CreateTeam(dummyAccount, "DummyTeam" + dummyTeamNum);
		teamNumberMap.put(dummyTeamNum, "DummyTeam" + dummyTeamNum);

		dataDB.RegisterUnit(dummyUnitNum, dummyTeamNum);
		regiments.put(dummyUnitNum, dummyTeamNum);
		teamUnitCount[dummyTeamNum]++;

		lastUnitNumber = unitNumberMap.size()+1;
		lastTeamNumber = teamNumberMap.size()+1;

//		dataDB.SaveLastNumbers();
		util = new UtilityFunctions(dataDB);

		
		dbsvr = new DBServer(false);
		loginsvr = new LoginServer(false);
		gamesvr = new GameServer(false, 0);
		
		dbsvr.init();
		loginsvr.init();
		gamesvr.init();
		
	}

	@AfterClass
	public static void after() throws Exception {
		dataDB.close();
		gamesvr.shutdown();
		dbsvr.shutdown();
		loginsvr.shutdown();
	}

	@Before
	public void connect() throws Exception {
		socket_game = new Socket(TriangleConf.ADDRESS_GAMESERVER, TriangleConf.SERVER_PORTS.get(0));
		os_game = socket_game.getOutputStream();
		is_game = socket_game.getInputStream();
		
		Thread.sleep(100);
	}

	@After
	public void disconnect() throws Exception {
		is_game.close();
		os_game.close();
		socket_game.close();
	}

	@Test
	public void connectionTest() {
		String address = socket_game.getInetAddress().getHostAddress();
		assertTrue(address.equals(TriangleConf.ADDRESS_GAMESERVER));
	}

	@Test
	public void CreateUnitTest() {
		int unitNum = lastUnitNumber;
		try {
			GameMessage.newBuilder().setGameMessageType(GameMessageType.CreateUnit).setAccountNumber(
					accountNumber).setCreateUnit(
					CreateUnit.newBuilder().setName("CreateUnit" + unitNum).setCls(
							TriangleValues.UnitClass.SOLDIER.getNumber())).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			assertTrue(recv.getResult());
			if (recv.getResult()) {
				ProtocolUnit u = util.GetUnitByUnitNumber(unitNum);
				if (u != null) {
					assertTrue(u.getUnitNumber() == unitNum);
					unitNumberMap.put(unitNum, "CreateUnit" + unitNum);
					accountUnitCount++;
					lastUnitNumber++;
				} else {
					fail("CreateUnit0" + unitNum + " was not created");
				}
			}
		} catch (IOException e) {
			e.printStackTrace();
		}
	}

	@Test
	public void DeleteUnitTest() {
		try {
			int unitNum = 5;
			GameMessage.newBuilder().setGameMessageType(GameMessageType.DeleteUnit).setAccountNumber(
					accountNumber).setDeleteUnit(
					DeleteUnit.newBuilder().setUnitNumber(unitNum)).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			assertTrue(recv.getResult());
			if (recv.getResult()) {
				assertTrue(util.GetUnitByUnitNumber(unitNum) == null);
				if (util.GetUnitByUnitNumber(unitNum) == null) {
					unitNumberMap.remove(unitNum);
					accountUnitCount--;
				} else {
					fail("DeleteUnit5 was not deleted");
				}
			}
		} catch (IOException e) {
			e.printStackTrace();
		}
	}

	@Test
	public void EditUnitTest() {
		try {
			int unitNum = 6;
			ProtocolUnit protoUnit = util.GetUnitByUnitNumber(6).toBuilder().setUnitName(
					"ChnagedUnitName").build();
			
			GameMessage.newBuilder().setGameMessageType(GameMessageType.EditUnit).setAccountNumber(
					accountNumber).setEditUnit(
					EditUnit.newBuilder().setUnitNumber(unitNum).setUnit(protoUnit)).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);
			
			assertTrue(recv.getResult());
			if (recv.getResult()) {
				ProtocolUnit u = util.GetUnitByUnitNumber(unitNum);
				if (u != null) {
					assertTrue(u.getUnitName().equals("ChnagedUnitName"));
					unitNumberMap.put(unitNum, "ChnagedUnitName");
				}
			}
		} catch (IOException e) {
			e.printStackTrace();
		}
	}

	@Test
	public void CreateTeamTest() {
		boolean success = false;
		int teamNum = lastTeamNumber;
		try {
			GameMessage.newBuilder().setGameMessageType(GameMessageType.CreateTeam).setAccountNumber(
					accountNumber).setCreateTeam(
					CreateTeam.newBuilder().setName("CreateTeam" + teamNum)).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			assertTrue(recv.getResult());
			if (recv.getResult()) {
				List<ProtocolTeam> teams = util.GetTeam(accountNumber);
				for (ProtocolTeam t : teams) {
					if (t.getTeamNumber() == teamNum) {
						assertTrue(t.getTeamName().equals("CreateTeam" + teamNum));
						teamNumberMap.put(teamNum, "CreateTeam" + teamNum);
						lastTeamNumber++;
						success = true;
					}
				}
			}
			if (success == false) {
				fail("CreateTeam" + teamNum + " team was not created");
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@Test
	public void DeleteTeamTest() {
		boolean success = true;
		try {
			GameMessage.newBuilder().setGameMessageType(GameMessageType.DeleteTeam).setAccountNumber(
					accountNumber).setDeleteTeam(DeleteTeam.newBuilder().setTeamNumber(3)).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			assertTrue(recv.getResult());
			if (recv.getResult()) {
				for (ProtocolTeam t : util.GetTeam(accountNumber)) {
					if (t.getTeamNumber() == 3) {
						success = false;
						fail("DeleteTeam03 was not deleted");
					}
				}
				if (success == true) {
					teamNumberMap.remove(4);
				}
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@Test
	public void EditTeamTest() {
		boolean success = false;
		try {
			int teamNum = 4;
			ProtocolTeam protoTeam = util.GetTeam(accountNumber).get(teamNum - 1).toBuilder().setTeamName(
					"ChnagedTeamName").build();
			GameMessage.newBuilder().setGameMessageType(GameMessageType.EditTeam).setAccountNumber(
					accountNumber).setEditTeam(
					EditTeam.newBuilder().setTeamNumber(teamNum).setTeam(protoTeam)).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			assertTrue(recv.getResult());
			if (recv.getResult()) {
				for (ProtocolTeam t : util.GetTeam(accountNumber)) {
					if (t.getTeamNumber() == teamNum) {
						assertTrue(t.getTeamName().equals("ChnagedTeamName"));
						teamNumberMap.put(teamNum, "ChnagedTeamName");
						success = true;
					}
				}
				if (success == false) {
					fail("Team number " + teamNum + " was not existed");
				}
			}
		} catch (IOException e) {
			e.printStackTrace();
		}
	}

	@Test
	public void RegisterUnitTest() {
		boolean success = false;
		int teamNum = 2;
		int unitNum = 7;
		try {
			GameMessage.newBuilder().setGameMessageType(GameMessageType.RegisterUnit).setAccountNumber(
					accountNumber).setRegisterUnit(
					RegisterUnit.newBuilder().setUnitNumber(unitNum).setTeamNumber(
							teamNum)).build().writeDelimitedTo(os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			assertTrue(recv.getResult());
			if (recv.getResult()) {
				for (ProtocolUnit u : util.GetUnitByTeamNumber(teamNum)) {
					if (u.getUnitNumber() == unitNum) {
						assertTrue("" + u.getUnitName(), u.getUnitName().equals("RegisterUnit7"));
						teamUnitCount[teamNum]++;
						success = true;
					}
				}
				if (success == false) {
					fail("Register Unit Fail");
				}
			}
		} catch (IOException e) {
			e.printStackTrace();
		}
	}

	@Test
	public void UnregisterUnitTest() {
		boolean success = true;
		int teamNum = 2;
		int unitNum = 8;
		try {
			GameMessage.newBuilder().setGameMessageType(
					GameMessageType.UnregisterUnit).setAccountNumber(accountNumber).setUnregisterUnit(
					UnregisterUnit.newBuilder().setUnitNumber(unitNum).setTeamNumber(
							teamNum)).build().writeDelimitedTo(os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			assertTrue(recv.getResult());
			if (recv.getResult()) {
				for (ProtocolUnit u : util.GetUnitByTeamNumber(teamNum)) {
					if (u.getUnitNumber() == unitNum
							&& u.getUnitName().equals("UnregisterUnit8")) {
						success = false;
						System.out.println("UnregisterUnitTest : " + u.getUnitName() + " "
								+ u.getUnitNumber());
					}
				}
				if (success == false) {
					fail("Register Unit Fail");
				} else {
					teamUnitCount[teamNum]--;
				}
			}
		} catch (IOException e) {
			e.printStackTrace();
		}
	}

	@Test
	public void GetTeamTest() {
		try {
			GameMessage.newBuilder().setGameMessageType(GameMessageType.GetTeamByAccountNumber).setAccountNumber(
					accountNumber).setGetTeamByAccountNumber(GetTeamByAccountNumber.newBuilder()).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			assertTrue(recv.getResult());
			if (recv.getResult()) {
				for (int i = 0; i < recv.getGetTeamByAccountNumber().getTeamCount(); i++) {
					ProtocolTeam protoTeam = recv.getGetTeamByAccountNumber().getTeam(i);
					assertTrue("TeamNumberMap does not have TeamNum : "
							+ protoTeam.getTeamNumber(),
							teamNumberMap.containsKey(protoTeam.getTeamNumber()));
					assertTrue("TeamNumberMap Num: " + protoTeam.getTeamNumber()
							+ " Name: " + teamNumberMap.get(protoTeam.getTeamNumber())
							+ " but received " + protoTeam.getTeamName(), teamNumberMap.get(
							protoTeam.getTeamNumber()).equals(protoTeam.getTeamName()));
				}
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@Test
	public void GetUnitByAccountNumberTest() {
		try {
			GameMessage.newBuilder().setGameMessageType(
					GameMessageType.GetUnitByAccountNumber).setAccountNumber(
					accountNumber).setGetUnitByAccountNumber(
					GetUnitByAccountNumber.newBuilder()).build().writeDelimitedTo(os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			int unitCount = recv.getGetUnitByAccountNumber().getUnitCount();
			assertTrue("Expected " + accountUnitCount + " but was " + unitCount,
					unitCount == accountUnitCount);

			for (int i = 0; i < recv.getGetUnitByAccountNumber().getUnitCount(); i++) {
				ProtocolUnit protoUnit = recv.getGetUnitByAccountNumber().getUnit(i);
				int unitNumber = protoUnit.getUnitNumber();

				String unitName = protoUnit.getUnitName();
				assertTrue("unitNumberMap does not contain " + unitNumber,
						unitNumberMap.containsKey(unitNumber));
				assertTrue("unitNum: " + unitNumber + ", expected unitName: "
						+ unitNumberMap.get(unitNumber) + " but was " + unitName,
						unitNumberMap.get(unitNumber).equals(unitName));
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@Test
	public void GetUnitByTeamNumberTest() {
		int teamNum = 1;
		try {
			GameMessage.newBuilder().setGameMessageType(
					GameMessageType.GetUnitByTeamNumber).setAccountNumber(accountNumber).setGetUnitByTeamNumber(
					GetUnitByTeamNumber.newBuilder().setTeamNumber(teamNum)).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			int unitCount = recv.getGetUnitByTeamNumber().getUnitCount();
			assertTrue(
					"Expected " + teamUnitCount[teamNum] + " but was " + unitCount,
					unitCount == teamUnitCount[teamNum]);

			for (int i = 0; i < recv.getGetUnitByTeamNumber().getUnitCount(); i++) {
				ProtocolUnit protoUnit = recv.getGetUnitByTeamNumber().getUnit(i);
				int unitNumber = protoUnit.getUnitNumber();

				String unitName = protoUnit.getUnitName();
				assertTrue("unitNumberMap does not contain " + unitNumber,
						unitNumberMap.containsKey(unitNumber));
				assertTrue("unitName is not " + unitNumberMap.get(unitNumber),
						unitNumberMap.get(unitNumber).equals(unitName));
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@Test
	public void GetUnitByUnitNumberTest() {
		try {
			int unitNum = 2;
			GameMessage.newBuilder().setGameMessageType(
					GameMessageType.GetUnitByUnitNumber).setAccountNumber(accountNumber).setGetUnitByUnitNumber(
					GetUnitByUnitNumber.newBuilder().setUnitNumber(unitNum)).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			ProtocolUnit protoUnit = recv.getGetUnitByUnitNumber().getUnit();
			int unitNumber = protoUnit.getUnitNumber();

			String unitName = protoUnit.getUnitName();
			assertTrue("unitNumberMap does not contain " + unitNumber,
					unitNumberMap.containsKey(unitNumber));
			assertTrue("unitName is not " + unitNumberMap.get(unitNumber),
					unitNumberMap.get(unitNumber).equals(unitName));

		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@Test
	public void CreateTacticTest() {
		boolean success1 = false;
		boolean success2 = false;
		try {
			int unitNum = 9;
			GameMessage.newBuilder().setGameMessageType(GameMessageType.CreateTactic).setAccountNumber(
					accountNumber).setCreateTactic(
					CreateTactic.newBuilder().setUnitNumber(unitNum)).build().writeDelimitedTo(os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);
			assertTrue(recv.getResult());

			GameMessage.newBuilder().setGameMessageType(GameMessageType.CreateTactic).setAccountNumber(
					accountNumber).setCreateTactic(
					CreateTactic.newBuilder().setUnitNumber(unitNum)).build().writeDelimitedTo(os_game);
			recv = GameMessage.parseDelimitedFrom(is_game);
			assertTrue(recv.getResult());

			if (recv.getResult()) {
				assertTrue( "Expected 2 but was " + util.GetTactic(unitNum).size(), util.GetTactic(unitNum).size() == 2);
				for (ProtocolTactic t : util.GetTactic(unitNum)) {
					if (t.getPriority() == 1) {
						assertTrue(t.getCondition() == TriangleValues.Conditions.valueOf(0).getNumber()
								&& t.getValue() == 0
								&& t.getAction() == TriangleValues.Action.valueOf(0).getNumber()
								&& t.getActionLevel() == 0);
						success1 = true;
					}
					if (t.getPriority() == 2) {
						assertTrue(t.getCondition() == TriangleValues.Conditions.valueOf(0).getNumber()
								&& t.getValue() == 0
								&& t.getAction() == TriangleValues.Action.valueOf(0).getNumber()
								&& t.getActionLevel() == 0);
						success2 = true;
					}
				}
				if (success1 == false || success2 == false) {
					fail(success1 + " " + success2);
				}
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@Test
	public void DeleteTacticTest() {
		boolean success1 = false;
		boolean success2 = false;
		try {
			int unitNum = 10;
			int to_del_priority = 2;
			GameMessage.newBuilder().setGameMessageType(GameMessageType.DeleteTactic).setAccountNumber(
					accountNumber).setDeleteTactic(
					DeleteTactic.newBuilder().setUnitNumber(unitNum).setPriority(to_del_priority)).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);
			assertTrue(recv.getResult());

			if (recv.getResult()) {
				for (ProtocolTactic t : util.GetTactic(unitNum)) {
					if (t.getPriority() == 1) {
						assertTrue(""+t.getValue(), t.getValue() == 1);
						success1 = true;
					}
					if (t.getPriority() == 2) {
						assertTrue(t.getValue() == 3);
						success2 = true;
					}
				}
			}
			if (success1 == false || success2 == false) {
				fail(success1 + " " + success2);
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@Test
	public void GetTacticTest() {
		boolean success1 = false;
		boolean success2 = false;
		boolean success3 = false;
		try {
			int unitNum = 11;
			GameMessage.newBuilder().setGameMessageType(GameMessageType.GetTactic).setAccountNumber(
					accountNumber).setGetTactic(
					GetTactic.newBuilder().setUnitNumber(unitNum)).build().writeDelimitedTo(
					os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);
			assertTrue(recv.getResult());

			if (recv.getResult()) {
				for (ProtocolTactic t : recv.getGetTactic().getTacticsList()) {
					if (t.getPriority() == 1) {
						assertTrue(""+t.getValue(),t.getValue() == 10);
						success1 = true;
					}
					if (t.getPriority() == 2) {
						assertTrue(t.getValue() == 20);
						success2 = true;
					}
					if (t.getPriority() == 3) {
						assertTrue(t.getValue() == 30);
						success3 = true;
					}
				}
				if (success1 == false || success2 == false || success3 == false) {
					fail(success1 + " " + success2 + " " + success3);
				}
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@Test
	public void EditTacticTest() {
		boolean success1 = false;
		boolean success2 = false;
		boolean success3 = false;
		try {
			int unitNum = 12;

			ProtocolTactic.Builder tacticBuilder = ProtocolTactic.newBuilder();
			tacticBuilder.setPriority(1).setCondition(
					TriangleValues.Conditions.COND_HP_LOWER_THAN.getNumber()).setValue(77).setAction(
					TriangleValues.Action.FireArrow.getNumber()).setActionLevel(1);
			GameMessage.newBuilder().setGameMessageType(GameMessageType.EditTactic).setAccountNumber(
					accountNumber).setEditTactic(
					EditTactic.newBuilder().setUnitNumber(unitNum).setPriority(1).setTactic(
							tacticBuilder)).build().writeDelimitedTo(os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);
			assertTrue(recv.getResult());

			if (recv.getResult()) {
				for (ProtocolTactic t : util.GetTactic(unitNum)) {
					if (t.getPriority() == 1) {
						assertTrue(t.getCondition() == TriangleValues.Conditions.COND_HP_LOWER_THAN.getNumber()
								&& t.getValue() == 77
								&& t.getAction() == TriangleValues.Action.FireArrow.getNumber()
								&& t.getActionLevel() == 1);
						success1 = true;
					}
					if (t.getPriority() == 2) {
						assertTrue(t.getValue() == 25);
						success2 = true;
					}
					if (t.getPriority() == 3) {
						assertTrue(t.getValue() == 35);
						success3 = true;
					}
				}
				if (success1 == false || success2 == false || success3 == false) {
					fail(success1 + " " + success2 + " " + success3);
				}
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}
}
