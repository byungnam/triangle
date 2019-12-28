package Triangle.Commons;

import io.netty.channel.Channel;

import java.util.HashMap;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.LinkedBlockingQueue;

import Triangle.GameServerTask.GameTask;

public class Global {
	public static BlockingQueue<GameTask> taskQueue = new LinkedBlockingQueue<>();
	public static HashMap<Integer, Channel> waitingTasks = new HashMap<>();
//	public static HashSet<Integer> waitingTasks = new HashSet<>();
	
	public static BlockingQueue<GameTask> combatTaskQueue = new LinkedBlockingQueue<>();
	public static HashMap<Integer, Channel> combatWaitingTasks = new HashMap<>();
	

	public static boolean isOperation; 
	public static boolean serverLive;
	public static String serverName;
	
}
