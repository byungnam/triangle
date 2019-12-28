package JUnitTest;

import static org.junit.Assert.assertTrue;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;

import org.junit.After;
import org.junit.AfterClass;
import org.junit.Before;
import org.junit.BeforeClass;
import org.junit.Test;

import Triangle.DBServer.DBServer;
import Triangle.DataDB.DataDB;
import Triangle.GameServer.GameServer;
import Triangle.LoginServer.LoginServer;
import Triangle.Protocol.GameProtocol.CombatRequest;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.TriangleConfigure.TriangleConf;
import Triangle.TriangleValues.TriangleValues;
import Triangle.dbms.ResetTables;

public class CombatTest {

	Socket socket_game;
	OutputStream os_game;
	InputStream is_game;

	static int accountNumber = 1;
	static int accountNumber2 = 2;
	static int allyTeamNum = 1;
	static int enemyTeamNum = 2;
	static GameServer gamesvr;
	static DBServer dbsvr;
	static LoginServer loginsvr;

	static DataDB dataDB;

	@BeforeClass
	public static void before() throws Exception {
		new ResetTables(false, true);

		dataDB = DataDB.getUser();
		dataDB.open(TriangleValues.FOR_TEST);
		dataDB.CreateUnit(accountNumber, "TestUnit01",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		dataDB.CreateUnit(accountNumber, "TestUnit02",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		dataDB.CreateUnit(accountNumber, "TestUnit03",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		dataDB.CreateUnit(accountNumber, "TestUnit04",
				TriangleValues.UnitClass.SOLDIER.getNumber());

		dataDB.CreateTactic(1);
		dataDB.CreateTactic(1);
		dataDB.CreateTactic(2);
		dataDB.CreateTactic(2);
		dataDB.CreateTactic(3);
		dataDB.CreateTactic(4);
		
		dataDB.EditTactic(1,
				TriangleValues.Conditions.COND_AT_MOST.getNumber(), 2,
				TriangleValues.Action.StrUp.getNumber(), 0, 1, 1);
		dataDB.EditTactic(2,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 1,
				TriangleValues.Action.FireArrow.getNumber(), 0, 1, 2);
		dataDB.EditTactic(1,
				TriangleValues.Conditions.COND_FOR_EACH_TURN.getNumber(), 2,
				TriangleValues.Action.Heal.getNumber(), 0, 2, 1);
		dataDB.EditTactic(2,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 2,
				TriangleValues.Action.BasicAttack.getNumber(), 0, 2, 2);
		dataDB.EditTactic(1,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 3,
				TriangleValues.Action.FireArrow.getNumber(), 0, 3, 1);
		dataDB.EditTactic(1,
				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 3,
				TriangleValues.Action.FireArrow.getNumber(), 0, 4, 1);

		dataDB.CreateUnit(accountNumber2, "Enemy01",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		dataDB.CreateUnit(accountNumber2, "Enemy02",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		dataDB.CreateUnit(accountNumber2, "Enemy03",
				TriangleValues.UnitClass.SOLDIER.getNumber());
		dataDB.CreateUnit(accountNumber2, "Enemy04",
				TriangleValues.UnitClass.SOLDIER.getNumber());

		
		dataDB.CreateTactic(5);
		dataDB.CreateTactic(6);
		dataDB.CreateTactic(7);
		dataDB.CreateTactic(8);
		
//		dataDB.CreateTactic(5, 1,
//				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 1,
//				TriangleValues.Action.BasicAttack.getNumber(), 0);
//		dataDB.CreateTactic(6, 1,
//				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 2,
//				TriangleValues.Action.BasicAttack.getNumber(), 0);
//		dataDB.CreateTactic(7, 1,
//				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 3,
//				TriangleValues.Action.BasicAttack.getNumber(), 0);
//		dataDB.CreateTactic(8, 1,
//				TriangleValues.Conditions.COND_ALWAYS.getNumber(), 4,
//				TriangleValues.Action.BasicAttack.getNumber(), 0);

		dataDB.CreateTeam(accountNumber, "TestTeam01");
		dataDB.CreateTeam(accountNumber, "TestTeam02");

		dataDB.RegisterUnit(1, 1);
		dataDB.RegisterUnit(2, 1);
		dataDB.RegisterUnit(3, 1);
		dataDB.RegisterUnit(4, 1);

		dataDB.RegisterUnit(5, 2);
		dataDB.RegisterUnit(6, 2);
		dataDB.RegisterUnit(7, 2);
		dataDB.RegisterUnit(8, 2);

//		dataDB.SaveLastNumbers();
		dataDB.close();

		dbsvr = new DBServer(false);
		loginsvr = new LoginServer(false);
		gamesvr = new GameServer(false, 0);

		dbsvr.init();
		loginsvr.init();
		gamesvr.init();

	}

	@AfterClass
	public static void after() throws Exception {
		gamesvr.shutdown();
		dbsvr.shutdown();
		loginsvr.shutdown();
	}

	@Before
	public void connect() throws Exception {
		socket_game = new Socket("127.0.0.1", TriangleConf.SERVER_PORTS.get(0));
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
	public void combatTest() {
		try {
			GameMessage.newBuilder().setGameMessageType(GameMessageType.CombatRequest).setAccountNumber(
					accountNumber).setCombatRequest(
					CombatRequest.newBuilder().setAllyTeamNumber(allyTeamNum).setEnemyTeamNumber(
							enemyTeamNum)).build().writeDelimitedTo(os_game);
			GameMessage recv = GameMessage.parseDelimitedFrom(is_game);

			assertTrue(recv.getResult());
			assertTrue(recv.getGameMessageType().equals(GameMessageType.CombatResult));

			
			
		} catch (IOException e) {
			e.printStackTrace();
		}
	}
}
