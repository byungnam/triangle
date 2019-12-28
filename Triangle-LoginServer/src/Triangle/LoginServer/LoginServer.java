package Triangle.LoginServer;

import io.netty.bootstrap.ServerBootstrap;
import io.netty.channel.Channel;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelInitializer;
import io.netty.channel.ChannelPipeline;
import io.netty.channel.EventLoopGroup;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.channel.nio.NioEventLoopGroup;
import io.netty.channel.socket.nio.NioServerSocketChannel;
import io.netty.handler.codec.protobuf.ProtobufDecoder;
import io.netty.handler.codec.protobuf.ProtobufEncoder;
import io.netty.handler.codec.protobuf.ProtobufVarint32FrameDecoder;
import io.netty.handler.codec.protobuf.ProtobufVarint32LengthFieldPrepender;

import java.io.IOException;
import java.sql.SQLException;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

import org.apache.log4j.Logger;

import Triangle.Interfaces.IServerInterface;
import Triangle.LoginServerTask.LoginTask;
import Triangle.Protocol.LoginProtocol.LoginMessage;
import Triangle.TriangleConfigure.TriangleConf;

public class LoginServer implements IServerInterface {
	private Logger Log = Logger.getLogger(this.getClass());

	public static boolean isOperation;
	public static boolean serverLive;

	private Channel ch;

	// private List<Integer> numberOfUsers;

	private ExecutorService executor;
	private int NUM_OF_MAX_THREAD = 100;

	private EventLoopGroup bossGroup;
	private EventLoopGroup workerGroup;
	
	public static LoginTaskRunner loginTaskRunner;
	public static ServerCommunicator serverCommunicator;
	public static HeartBeatSender heartBeatSender;

	public void init() {
		try {
			Log.info("Starting Login Svr");

			serverLive = true;
//			numberOfUsers = new LinkedList<Integer>();
			
			executor = Executors.newFixedThreadPool(NUM_OF_MAX_THREAD);
			loginTaskRunner = new LoginTaskRunner();
			serverCommunicator = new ServerCommunicator();
			heartBeatSender = new HeartBeatSender();
			executor.submit(loginTaskRunner);
			executor.submit(serverCommunicator);
			executor.submit(heartBeatSender);

			bossGroup = new NioEventLoopGroup();
			workerGroup = new NioEventLoopGroup();
			ServerBootstrap b = new ServerBootstrap();
			b.group(bossGroup, workerGroup).channel(NioServerSocketChannel.class).childHandler(
					new ChannelInitializer<Channel>() {
						@Override
						protected void initChannel(Channel ch) throws Exception {
							ChannelPipeline p = ch.pipeline();
							p.addLast("frameDecoder", new ProtobufVarint32FrameDecoder());
							p.addLast("protobufDecoder", new ProtobufDecoder(
									LoginMessage.getDefaultInstance()));

							p.addLast("frameEncoder",
									new ProtobufVarint32LengthFieldPrepender());
							p.addLast("protobufEncoder", new ProtobufEncoder());

							p.addLast("handler", new MessageReceiver());
						}
					});
			
			try {
				ch = b.bind(TriangleConf.PORT_toClient_LOGINSERVER).sync().channel();
			} catch (InterruptedException e) {
				e.printStackTrace();
			}
			Log.info("LOGIN SERVER ONLINE...");
		} catch (SQLException e) {
			e.printStackTrace();
		}
	}

	private class MessageReceiver extends SimpleChannelInboundHandler<LoginMessage> {
		@Override
		public void messageReceived(ChannelHandlerContext ctx, LoginMessage msg) throws Exception {
			LoginTaskRunner.taskQ.put(new LoginTask(msg, ctx.channel()));
		}
	}
	
	public void run() throws ExecutionException {
		try {
			System.in.read();
		} catch (IOException e) {
			e.printStackTrace();
		}
	}

	public void shutdown() {
		Log.info("LOGIN SERVER SHUTDOWNING...");
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

		Log.info("LOGIN SERVER SHUTDOWN END...");
	}


	// public int tokenGen(int userNumber) {
	// Random r = new Random();
	// int key = r.nextInt();
	// while (TokenUserNumber.containsKey(key)) {
	// key++;
	// }
	// TokenUserNumber.put(key, userNumber);
	// return key;
	// }

	public LoginServer(boolean isOperation) throws ExecutionException {
		LoginServer.isOperation = isOperation;
		if (isOperation) {
			init();
			run();
			shutdown();
		}
	}

	public static void main(String[] args) throws ExecutionException {
		new LoginServer(true);
	}

}
