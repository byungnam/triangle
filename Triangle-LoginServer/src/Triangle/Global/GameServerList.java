package Triangle.Global;

import io.netty.channel.Channel;

import java.util.HashMap;
import java.util.Set;

public class GameServerList {

	private static HashMap<String, Channel> gameServerList = new HashMap<String, Channel>();

	public static void addGameServer(String serverName, Channel ch) {
		gameServerList.put(serverName, ch);
	}
	
	public static void removeGameServer(String serverName) {
		gameServerList.remove(serverName);
	}
	
	public static Channel getGameServerChannel(String serverName){
		return gameServerList.get(serverName);
	}
	
	public static Set<String> getGameServerList(){
		return gameServerList.keySet();
	}
	
}
