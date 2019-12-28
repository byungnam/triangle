package Triangle.GameServer;

import io.netty.bootstrap.ServerBootstrap;
import io.netty.channel.Channel;
import io.netty.channel.EventLoopGroup;
import io.netty.channel.nio.NioEventLoopGroup;
import io.netty.channel.socket.nio.NioServerSocketChannel;

import java.io.IOException;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

import org.apache.log4j.Logger;

import Triangle.Commons.Global;
import Triangle.Interfaces.IServerInterface;
import Triangle.MessageHandlers.ProtoGameServerFrontendChannelInitializer;
import Triangle.TriangleConfigure.TriangleConf;

public class GameServer implements IServerInterface {
	private Logger Log = Logger.getLogger(this.getClass());

	private Integer serverPort;

	private ExecutorService executor;
	private int NUM_OF_MAX_THREAD = 100;

	private Channel chUser;

	private EventLoopGroup bossGroup;
	private EventLoopGroup workerGroup;

	// private int taskNumber = 0;

	@Override
	public void init() {
		Log.info("Starting Game Svr");
		Log.info("Server Name : " + Global.serverName);

		Global.serverLive = true;
		executor = Executors.newFixedThreadPool(NUM_OF_MAX_THREAD);

		//executor.submit(new TaskRunner());
		//executor.submit(new CopyOfCombatTaskRunner());
		executor.submit(new ServerCommunicator());

		DataInitializer.initialize();

		openUserConnector();

		Log.info(Global.serverName + " : " + serverPort);
		Log.info("GAME SERVER ONLINE...");
	}

	// user connector should be a server side
	public void openUserConnector() {
		bossGroup = new NioEventLoopGroup();
		workerGroup = new NioEventLoopGroup();
		ServerBootstrap b = new ServerBootstrap();
		b.group(bossGroup, workerGroup).channel(NioServerSocketChannel.class).childHandler(
				new ProtoGameServerFrontendChannelInitializer());
		try {
			// save this channel to close when shutdown
			chUser = b.bind(serverPort).sync().channel();
		} catch (InterruptedException e) {
			e.printStackTrace();
		}
	}

	@Override
	public void run() throws ExecutionException {
		try {
			System.in.read();
		} catch (IOException e) {
			e.printStackTrace();
		}
	}

	@Override
	public void shutdown() {
		Log.info("GAME SERVER SHUTDOWNING...");
		Global.serverLive = false;

		chUser.close();
		bossGroup.shutdownGracefully();
		workerGroup.shutdownGracefully();

		executor.shutdownNow();

		try {
			executor.awaitTermination(1, TimeUnit.SECONDS);
		} catch (InterruptedException e) {
			e.printStackTrace();
		}

		Log.info("GAME SERVER SHUTDOWN END...");
	}

	public GameServer(boolean isOperation, Integer serverNumber)
			throws ExecutionException {
		Global.isOperation = isOperation;
		Global.serverName = TriangleConf.SERVER_NAMES.get(serverNumber);
		this.serverPort = TriangleConf.SERVER_PORTS.get(serverNumber);
		if (isOperation) {
			init();
			run();
			shutdown();
		}
	}

	public static void main(String[] args) throws ExecutionException {
		// new GameServer(true, Integer.parseInt(args[0]));
		new GameServer(true, 0);
	}

}
