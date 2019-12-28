package Triangle.GameServer;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;

import org.apache.log4j.Logger;

import Triangle.Commons.Global;
import Triangle.Protocol.InterServerProtocol.HeartBeat;
import Triangle.Protocol.InterServerProtocol.InterServerMessage;
import Triangle.Protocol.InterServerProtocol.InterServerMessage.InterServerMessageType;
import Triangle.Protocol.InterServerProtocol.ServerUp;
import Triangle.TriangleConfigure.TriangleConf;

public class ServerCommunicator implements Runnable {
	private Logger Log = Logger.getLogger(this.getClass());

	// public static BlockingQueue<GameMessage> userConnTaskQ;
	// private Set<Integer> authorizedUsers;

	private Socket socket;
	private OutputStream os;
	private InputStream is;

	public ServerCommunicator() {
		// authorizedUsers = new HashSet<>();
		try {
			socket = new Socket(TriangleConf.ADDRESS_LOGINSERVER,
					TriangleConf.PORT_LOGINSERVER_COMMUNICATOR);
			os = socket.getOutputStream();
			is = socket.getInputStream();
		} catch (IOException e) {
			e.printStackTrace();
		}
		Log.info("GameServer Communicator Start to Listen");
	}

	// private class MessageReceiver extends
	// SimpleChannelInboundHandler<GameMessage> {
	// @Override
	// public void messageReceived(ChannelHandlerContext ctx, GameMessage msg)
	// throws Exception {
	// switch (msg.getGameMessageType()) {
	// case Join:
	// authorizedUsers.add(msg.getAccountNumber());
	// break;
	// case Leave:
	// // if (msg.getFlag() == true) { // join
	// // if (!authorizedUsers.contains(msg.getJoinOrLeave().getAccountNumber()))
	// {
	// // throw new Exception("Non-authorized User Connection Detected");
	// // }
	// // } else { // leave
	// // if (!authorizedUsers.remove(msg.getJoinOrLeave().getAccountNumber())) {
	// // throw new Exception("Unknown User Leave Message Detected");
	// // }
	// // }
	// break;
	// default:
	// throw new Exception("Wrong Message Received");
	// }
	// }
	// }

	public void run() {
		Log.info("Send a ServerUp message");
		try {
			InterServerMessage.newBuilder().setInterServerMessageType(
					InterServerMessageType.ServerUp).setServerUp(
					ServerUp.newBuilder().setServerName(Global.serverName)).build().writeDelimitedTo(
					os);
		} catch (IOException e1) {
			e1.printStackTrace();
		}

		Log.info("Start to Receive HeartBeat");
		while (Global.serverLive) {
			try {
				InterServerMessage msg = InterServerMessage.parseDelimitedFrom(is);
				switch (msg.getInterServerMessageType()) {
				case HeartBeat:
//					 Log.info("HeartBeat Received");
					InterServerMessage.newBuilder().setInterServerMessageType(
							InterServerMessageType.HeartBeat).setHeartBeat(
							HeartBeat.newBuilder().setServerName(Global.serverName)).build().writeDelimitedTo(
							os);
//					Log.info("HeartBeat Returned");
					break;
				case Join:
					break;
				case Leave:
					break;

				default:
					break;
				}
			} catch (IOException e) {
				e.printStackTrace();
			}
		}
		try {
			os.close();
			is.close();
			socket.close();
		} catch (IOException e) {
			e.printStackTrace();
		}
	}
}
