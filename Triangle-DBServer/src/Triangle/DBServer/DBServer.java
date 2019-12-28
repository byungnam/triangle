package Triangle.DBServer;

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

import Triangle.Interfaces.IServerInterface;
import Triangle.MessageHandlers.ProtoDBServerChannelInitializer;
import Triangle.TriangleConfigure.TriangleConf;

public class DBServer implements IServerInterface {
	private Logger Log = Logger.getLogger(this.getClass());

	public static boolean isOperation;
	public static boolean serverLive;

	private ExecutorService executor;
	private int NUM_OF_MAX_THREAD = 100;

	EventLoopGroup bossGroup;
	EventLoopGroup workerGroup;

	private Channel ch;

	@Override
	public void init() {

		Log.info("Starting DB SERVER");

		serverLive = true;
		executor = Executors.newFixedThreadPool(NUM_OF_MAX_THREAD);
		executor.submit(new DBTaskRunner());

		bossGroup = new NioEventLoopGroup();
		workerGroup = new NioEventLoopGroup();
		ServerBootstrap b = new ServerBootstrap();
		b.group(bossGroup, workerGroup)
		 .channel(NioServerSocketChannel.class)
		 .childHandler(new ProtoDBServerChannelInitializer());
		try {
			ch = b.bind(TriangleConf.PORT_DBSERVER_COMMUNICATOR).sync().channel();
			Log.info("DB SERVER ONLINE...");
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
		Log.info("DB SERVER SHUTDOWNING...");
		serverLive = false;

		ch.close();
		bossGroup.shutdownGracefully();
		workerGroup.shutdownGracefully();

		executor.shutdownNow();
		try {
			executor.awaitTermination(1, TimeUnit.SECONDS);
		} catch (InterruptedException e) {
			e.printStackTrace();
		}
		Log.info("DB SERVER SHUTDOWN END...");
	}

	public DBServer(boolean isOperation) throws ExecutionException {
		DBServer.isOperation = isOperation;
		if (isOperation) {
			init();
			run();
			shutdown();
		}
	}

	public static void main(String args[]) throws ExecutionException {
		new DBServer(true);
	}

}
