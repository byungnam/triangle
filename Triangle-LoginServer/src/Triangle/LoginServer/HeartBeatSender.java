package Triangle.LoginServer;

import java.util.HashMap;
import java.util.Map;

import org.apache.log4j.Logger;

import Triangle.Global.GameServerList;
import Triangle.Protocol.InterServerProtocol.InterServerMessage;
import Triangle.Protocol.InterServerProtocol.InterServerMessage.InterServerMessageType;

public class HeartBeatSender implements Runnable {
	private Logger Log = Logger.getLogger(this.getClass());

	private static Map<String, Integer> lastServerResponseTime;

	public HeartBeatSender() {
		lastServerResponseTime = new HashMap<>();
	}

	public void run() {
		while (LoginServer.serverLive) {
			try {
				Thread.sleep(1000);
				for (String svr : GameServerList.getGameServerList()) {
					if (!lastServerResponseTime.containsKey(svr)) {
						resetServerResponseTime(svr);
					}
					
					GameServerList.getGameServerChannel(svr).writeAndFlush(
							InterServerMessage.newBuilder().setInterServerMessageType(
									InterServerMessageType.HeartBeat).build());

//					Log.info("HeartBeat Send to ServerName:" + svr);

					addLastServerResponseTime(svr);
					if (lastServerResponseTime.get(svr) > 5
							&& lastServerResponseTime.get(svr) % 2 == 0) {
						Log.info("Server Dead : " + svr);
					}
				}
			} catch (InterruptedException e) {
				Log.info("Shutdown Interrupt Detected");
			}
		}
	}

	public static void resetServerResponseTime(String serverName) {
		lastServerResponseTime.put(serverName, 0);
	}

	public static void addLastServerResponseTime(String serverName) {
		lastServerResponseTime.put(serverName,
				lastServerResponseTime.get(serverName) + 1);
	}
}