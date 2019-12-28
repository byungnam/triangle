package JUnitTest;

import io.netty.bootstrap.Bootstrap;
import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelHandlerAdapter;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelInitializer;
import io.netty.channel.ChannelPipeline;
import io.netty.channel.nio.NioEventLoopGroup;
import io.netty.channel.socket.nio.NioSocketChannel;
import io.netty.handler.codec.protobuf.ProtobufDecoder;
import io.netty.handler.codec.protobuf.ProtobufEncoder;
import io.netty.handler.codec.protobuf.ProtobufVarint32FrameDecoder;
import io.netty.handler.codec.protobuf.ProtobufVarint32LengthFieldPrepender;

import org.junit.AfterClass;
import org.junit.BeforeClass;
import org.junit.Test;

import Triangle.DBServer.DBServer;
import Triangle.GameServer.GameServer;
import Triangle.LoginServer.LoginServer;
import Triangle.Protocol.GameProtocol.CreateUnit;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.TriangleConfigure.TriangleConf;
import Triangle.TriangleValues.TriangleValues;

public class NettyTest {

	static DBServer dbsvr;
	static GameServer gamesvr;
	static LoginServer loginsvr;

	@BeforeClass
	public static void before() throws Exception {
		dbsvr = new DBServer(false);
		dbsvr.init();
		loginsvr = new LoginServer(false);
		loginsvr.init();
		gamesvr = new GameServer(false, 0);
		gamesvr.init();

		Thread.sleep(100);
	}

	@AfterClass
	public static void after() throws Exception {
		dbsvr.shutdown();
		loginsvr.shutdown();
		gamesvr.shutdown();

	}

	@Test
	public void connectToGameServer() {
		Bootstrap b = new Bootstrap();
		b.group(new NioEventLoopGroup()).channel(NioSocketChannel.class)
				.handler(new ChannelInitializer<Channel>() {
					@Override
					protected void initChannel(Channel ch) throws Exception {
						ChannelPipeline p = ch.pipeline();
						p.addLast("frameDecoder",
								new ProtobufVarint32FrameDecoder());
						p.addLast("protobufDecoder", new ProtobufDecoder(
								GameMessage.getDefaultInstance()));

						p.addLast("frameEncoder",
								new ProtobufVarint32LengthFieldPrepender());
						p.addLast("protobufEncoder", new ProtobufEncoder());

						p.addLast("handler", new ChannelHandlerAdapter() {
							@Override
							public void channelRead(
									final ChannelHandlerContext ctx, Object msg)
									throws Exception {
								System.out.println(((GameMessage) msg)
										.getGameMessageType());
							}
						});
					}
				});

		ChannelFuture f = b.connect(TriangleConf.ADDRESS_DBSERVER,
				TriangleConf.PORT_DBSERVER_COMMUNICATOR);
		Channel ch = null;
		try {
			ch = f.sync().channel();
			ch.writeAndFlush(GameMessage
					.newBuilder()
					.setGameMessageType(GameMessageType.CreateUnit)
					.setAccountNumber(7071)
					.setCreateUnit(
							CreateUnit
									.newBuilder()
									.setName("A_TEST_FOR_NETTY")
									.setCls(TriangleValues.UnitClass.SOLDIER
											.getNumber())).build());
			ch.close();
			System.out.println("DONE");
		} catch (InterruptedException e1) {
			e1.printStackTrace();
		}

		try {
			Thread.sleep(1000);
		} catch (InterruptedException e) {
			e.printStackTrace();
		}

	}
}
