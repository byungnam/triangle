package Triangle.LoginServer;

import java.sql.SQLException;
import java.util.HashMap;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.LinkedBlockingQueue;

import org.apache.log4j.Logger;

import Triangle.AccountDB.AccountDB;
import Triangle.Global.GameServerList;
import Triangle.LoginServerTask.LoginTask;
import Triangle.Protocol.InterServerProtocol.InterServerMessage;
import Triangle.Protocol.InterServerProtocol.InterServerMessage.InterServerMessageType;
import Triangle.Protocol.InterServerProtocol.JoinLeave;
import Triangle.Protocol.LoginProtocol.Authentication;
import Triangle.Protocol.LoginProtocol.LoginMessage;
import Triangle.Protocol.LoginProtocol.ServerInformation;
import Triangle.TriangleConfigure.TriangleConf;

public class LoginTaskRunner implements Runnable {
	private Logger Log = Logger.getLogger(this.getClass());
	private AccountDB accountDB;

	public static BlockingQueue<LoginTask> taskQ;

	private HashMap<Integer, String> UserPlacement;

	// private HashMap<String, Channel> GameServerChannels;

	public LoginTaskRunner() throws SQLException {
		accountDB = new AccountDB(LoginServer.isOperation);
		accountDB.open();

		taskQ = new LinkedBlockingQueue<LoginTask>();

		UserPlacement = new HashMap<>();
		// GameServerChannels = new HashMap<>();

	}

	public void run() {
		boolean result;
		Integer accountNumber;
		Authentication.Builder authenticationMsg;
		String serverName;

		while (LoginServer.serverLive) {
			try {
				result = false;
				accountNumber = null;
				LoginTask task = taskQ.take();
				LoginMessage loginMessage = task.loginMessage;

				Log.info("Login message : " + loginMessage.getLoginMessageType());

				switch (loginMessage.getLoginMessageType()) {
				case CheckIdConflict:
					result = accountDB.CheckForIdConflict(loginMessage.getCheckIdConflict().getId());
					task.channel.writeAndFlush(loginMessage.toBuilder().setCheckIdConflict(
							loginMessage.getCheckIdConflict().toBuilder().setSuccess(result)));
					break;
					
				case CreateAccount:
					result = accountDB.CreateAccount(
							loginMessage.getCreateAccount().getId(),
							loginMessage.getCreateAccount().getPassword());
					task.channel.writeAndFlush(loginMessage.toBuilder().setCreateAccount(
							loginMessage.getCreateAccount().toBuilder().setSuccess(result)));
					break;

				case DeleteAccount:
					accountNumber = accountDB.Authentication(
							loginMessage.getDeleteAccount().getId(),
							loginMessage.getDeleteAccount().getPassword());
					if (accountNumber >= 0) {
						if (!UserPlacement.containsKey(accountNumber)) {
							result = accountDB.DeleteAccount(
									loginMessage.getCreateAccount().getId(),
									loginMessage.getCreateAccount().getPassword());
						}
					}
					task.channel.writeAndFlush(loginMessage.toBuilder().setDeleteAccount(
							loginMessage.getDeleteAccount().toBuilder().setSuccess(result)));
					break;

				case Authentication:
					authenticationMsg = loginMessage.getAuthentication().toBuilder();
					accountNumber = accountDB.Authentication(
							loginMessage.getAuthentication().getId(),
							loginMessage.getAuthentication().getPassword());
					if (accountNumber >= 0) {
						// if (UserPlacement.containsKey(accountNumber)) {
						// accountNumber = -2; // duplicate login
						// UserPlacement.put(accountNumber, "");
						// authenticationMsg.addAllServerName(TriangleConf.SERVER_NAMES);
						// } else {
						UserPlacement.put(accountNumber, "");
						authenticationMsg.addAllServerName(TriangleConf.SERVER_NAMES);
						// }
					}
					task.channel.writeAndFlush(loginMessage.toBuilder().setAuthentication(
							authenticationMsg.setAccountNumber(accountNumber)));
					break;

				case JoinServer:
					accountNumber = loginMessage.getJoinServer().getAccountNumber();
					serverName = UserPlacement.get(accountNumber);

					// situation : previous login exist. how to treat? login deny or
					// disconnect previous login
					if (serverName == null || !serverName.equals("")) {
						throw new Exception("Not Authorized Account Join Attempt");
					}
					serverName = loginMessage.getJoinServer().getServerName();
					UserPlacement.put(accountNumber,
							loginMessage.getJoinServer().getServerName());

					GameServerList.getGameServerChannel(serverName).writeAndFlush(
							InterServerMessage.newBuilder().setInterServerMessageType(
									InterServerMessageType.Join).setJoinleave(
									JoinLeave.newBuilder().setAccountNumber(accountNumber)));

					int serverNumber = TriangleConf.SERVER_NAMES.indexOf(serverName);

					task.channel.writeAndFlush(loginMessage.toBuilder().setJoinServer(
							loginMessage.getJoinServer().toBuilder().setServerInformation(
									ServerInformation.newBuilder().setServerAddress(
											TriangleConf.SERVER_ADDRESSES.get(serverNumber)).setServerPort(
											TriangleConf.SERVER_PORTS.get(serverNumber)))));
					break;

				case LeaveServer:
					accountNumber = loginMessage.getLeaveServer().getAccountNumber();
					UserPlacement.put(accountNumber, "");
					break;

				case Disconnect:
					accountNumber = loginMessage.getDisconnect().getAccountNumber();
					// Log.info("Disconn " + accountNumber);
					UserPlacement.remove(accountNumber);
					break;

				default:
					break;

				// case HeartBeat:
				// serverName = loginMessage.getHeartBeat().getServerName();
				// HeartBeatListener.serverStatus.put(serverName, 0);
				// GameServerChannels.put(serverName, task.channel);
				}
			} catch (InterruptedException e) {
				Log.info("Shutdown Interrupt Detected");
			} catch (Exception e) {
				// e.g. no auth connection trial. log to file?
			}
		}
		// accountDB.SaveLastNumber();
		accountDB.close();
	}
}
