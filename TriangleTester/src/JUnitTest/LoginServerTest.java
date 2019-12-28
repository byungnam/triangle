package JUnitTest;

import static org.junit.Assert.assertTrue;
import static org.junit.Assert.fail;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;
import java.util.List;

import org.junit.After;
import org.junit.AfterClass;
import org.junit.Before;
import org.junit.BeforeClass;
import org.junit.Test;

import Triangle.AccountDB.AccountDB;
import Triangle.DBServer.DBServer;
import Triangle.GameServer.GameServer;
import Triangle.LoginServer.LoginServer;
import Triangle.Protocol.LoginProtocol.Authentication;
import Triangle.Protocol.LoginProtocol.CheckIdConflict;
import Triangle.Protocol.LoginProtocol.CreateAccount;
import Triangle.Protocol.LoginProtocol.DeleteAccount;
import Triangle.Protocol.LoginProtocol.Disconnect;
import Triangle.Protocol.LoginProtocol.JoinServer;
import Triangle.Protocol.LoginProtocol.LeaveServer;
import Triangle.Protocol.LoginProtocol.LoginMessage;
import Triangle.Protocol.LoginProtocol.LoginMessage.LoginMessageType;
import Triangle.TriangleConfigure.TriangleConf;
import Triangle.dbms.ResetTables;

public class LoginServerTest {

	Socket socket_login;
	OutputStream os_login;
	InputStream is_login;

	static LoginServer svr;
	static GameServer svrG;
	static DBServer svrD;

	@BeforeClass
	public static void before() throws Exception {
		new ResetTables(false, true);

		AccountDB accountDB = new AccountDB(false);
		accountDB.open();
		accountDB.CreateAccount("TestID", "TestPW");
		accountDB.CreateAccount("TestID_toDelete", "TestPW_toDelete");
		accountDB.CreateAccount("TestID_toJoin", "TestPW_toJoin");
		accountDB.close();

		svr = new LoginServer(false);
		svrD = new DBServer(false);
		svrG = new GameServer(false, 0);
		svr.init();
		svrD.init();
		svrG.init();
	}

	@AfterClass
	public static void after() throws Exception {
		svr.shutdown();
		svrG.shutdown();
		svrD.shutdown();
	}

	@Before
	public void connect() throws Exception {
		socket_login = new Socket("127.0.0.1",
				TriangleConf.PORT_toClient_LOGINSERVER);
		os_login = socket_login.getOutputStream();
		is_login = socket_login.getInputStream();
	}

	@After
	public void disconnect() throws Exception {
		is_login.close();
		os_login.close();
		socket_login.close();
	}

	@Test
	public void connectionTest() {
		String address = socket_login.getInetAddress().getHostAddress();
		assertTrue(address.equals("127.0.0.1"));
	}
	
	@Test
	public void createAccountTest() {
		String id = "TestID_New";
		String pw = "TestPW_New";

		try {
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.CreateAccount).setCreateAccount(
					CreateAccount.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);
			LoginMessage result = LoginMessage.parseDelimitedFrom(is_login);

			assertTrue(result.hasCreateAccount());
			assertTrue(result.getCreateAccount().getSuccess());
		} catch (IOException e) {
			fail("Exception in create account : " + e.getMessage());
		}
	}

	@Test
	public void checkIdConflict() {
		String id = "TestID";

		try {
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.CheckIdConflict).setCheckIdConflict(
					CheckIdConflict.newBuilder().setId(id)).build().writeDelimitedTo(
					os_login);
			LoginMessage result = LoginMessage.parseDelimitedFrom(is_login);

			assertTrue(result.hasCheckIdConflict());
			assertTrue(!result.getCheckIdConflict().getSuccess());
		} catch (IOException e) {
			fail("Exception in CheckIdConflict Test : " + e.getMessage());
		}
	}

	@Test
	public void authenticationTest() {
		String id = "TestID";
		String pw = "TestPW";

		try {
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Authentication).setAuthentication(
					Authentication.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);

			LoginMessage result = LoginMessage.parseDelimitedFrom(is_login);

			assertTrue(result.hasAuthentication());

			int accountNumber = result.getAuthentication().getAccountNumber();
			assertTrue("accountNumber is less than 0 : "+ accountNumber , accountNumber >= 0);
			
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Disconnect).setDisconnect(
					Disconnect.newBuilder().setAccountNumber(accountNumber)).build().writeDelimitedTo(
					os_login);
			
		} catch (IOException e) {
			fail("Exception in authenticate account : " + e.getMessage());
		}
	}
	
	@Test
	public void multipleAuthenticationTest() {
		String id = "TestID";
		String pw = "TestPW";

		try {
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Authentication).setAuthentication(
					Authentication.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);

			LoginMessage result = LoginMessage.parseDelimitedFrom(is_login);

			assertTrue(result.hasAuthentication());

			int accountNumber = result.getAuthentication().getAccountNumber();
			assertTrue("accountNumber is less than 0 : "+ accountNumber, accountNumber >= 0);
			
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Disconnect).setDisconnect(
					Disconnect.newBuilder().setAccountNumber(accountNumber)).build().writeDelimitedTo(
					os_login);
			
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Authentication).setAuthentication(
					Authentication.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);

			result = LoginMessage.parseDelimitedFrom(is_login);

			assertTrue(result.hasAuthentication());

			accountNumber = result.getAuthentication().getAccountNumber();
			assertTrue(accountNumber >= 0);
			
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Disconnect).setDisconnect(
					Disconnect.newBuilder().setAccountNumber(accountNumber)).build().writeDelimitedTo(
					os_login);
			
		} catch (IOException e) {
			fail("Exception in authenticate account : " + e.getMessage());
		}
	}

	@Test
	public void authenticationFailTestById() {
		String id = "_NoId";
		String pw = "TestPW";

		try {
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Authentication).setAuthentication(
					Authentication.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);

			LoginMessage result = LoginMessage.parseDelimitedFrom(is_login);

			assertTrue(result.hasAuthentication());

			int accountNumber = result.getAuthentication().getAccountNumber();
			assertTrue("Expected accountNumber -1 but was " + accountNumber,
					accountNumber == -1);
			
		} catch (IOException e) {
			fail("Exception in authenticate account : " + e.getMessage());
		}
	}

	@Test
	public void authenticationFailTestByPw() {
		String id = "TestID";
		String pw = "_WrongPw";

		try {
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Authentication).setAuthentication(
					Authentication.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);

			LoginMessage result = LoginMessage.parseDelimitedFrom(is_login);

			assertTrue(result.hasAuthentication());

			int accountNumber = result.getAuthentication().getAccountNumber();
			assertTrue("Expected accountNumber -1 but was " + accountNumber,
					accountNumber == -1);
		} catch (IOException e) {
			fail("Exception in authenticate account : " + e.getMessage());
		}
	}

	//@Test
	public void authenticationFailTestByDuplicateLogin() {
		String id = "TestID";
		String pw = "TestPW";

		try {
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Authentication).setAuthentication(
					Authentication.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);

			LoginMessage result = LoginMessage.parseDelimitedFrom(is_login);

			int accountNumber = result.getAuthentication().getAccountNumber();
			
			assertTrue(result.hasAuthentication());
			assertTrue(accountNumber >= 0);
			
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Authentication).setAuthentication(
					Authentication.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);

			result = LoginMessage.parseDelimitedFrom(is_login);

			int accountNumber_fail = result.getAuthentication().getAccountNumber();
			assertTrue("Expected accountNumber -2 but was " + accountNumber_fail,
					accountNumber_fail == -2);
			
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Disconnect).setDisconnect(
					Disconnect.newBuilder().setAccountNumber(accountNumber)).build().writeDelimitedTo(
					os_login);
			
		} catch (IOException e) {
			fail("Exception in authenticate account : " + e.getMessage());
		}
	}

	@Test
	public void deleteAccountTest() {
		String id = "TestID_toDelete";
		String pw = "TestPW_toDelete";

		try {
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.DeleteAccount).setDeleteAccount(
					DeleteAccount.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);
			LoginMessage result = LoginMessage.parseDelimitedFrom(is_login);
			assertTrue(result.hasDeleteAccount());
			assertTrue(result.getDeleteAccount().getSuccess());
		} catch (IOException e) {
			fail("Exception in delete account : " + e.getMessage());
		}
	}

	@Test
	public void joinAndLeaveTest() {
		String id = "TestID_toJoin";
		String pw = "TestPW_toJoin";

		try {
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.Authentication).setAuthentication(
					Authentication.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
					os_login);

			LoginMessage result = LoginMessage.parseDelimitedFrom(is_login);
			int accountNumber = result.getAuthentication().getAccountNumber();

			List<String> serverNameList = result.getAuthentication().getServerNameList();

			String serverName = serverNameList.get(0);

			assertTrue(serverName.equals(TriangleConf.SERVER_NAMES.get(0)));

			LoginMessage.newBuilder().setLoginMessageType(LoginMessageType.JoinServer).setJoinServer(
					JoinServer.newBuilder().setAccountNumber(accountNumber).setServerName(
							serverName)).build().writeDelimitedTo(os_login);

			result = LoginMessage.parseDelimitedFrom(is_login);
			assertTrue(result.getJoinServer().getServerInformation().getServerAddress()
					.equals(TriangleConf.SERVER_ADDRESSES.get(0)));
			
			LoginMessage.newBuilder().setLoginMessageType(
					LoginMessageType.LeaveServer).setLeaveServer(
					LeaveServer.newBuilder().setAccountNumber(accountNumber)).build().writeDelimitedTo(
					os_login);

		} catch (IOException e) {
			fail("Exception in joinServerTest : " + e.getMessage());
		}
	}
}
