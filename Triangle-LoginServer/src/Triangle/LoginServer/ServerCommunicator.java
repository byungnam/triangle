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

import org.apache.log4j.Logger;

import Triangle.Global.GameServerList;
import Triangle.Protocol.InterServerProtocol.InterServerMessage;
import Triangle.TriangleConfigure.TriangleConf;

class ServerCommunicator implements Runnable {
	private Logger Log = Logger.getLogger(this.getClass());

	private Channel ch;
	private EventLoopGroup bossGroup;
	private EventLoopGroup workerGroup;

	public void run() {

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
								InterServerMessage.getDefaultInstance()));

						p.addLast("frameEncoder",
								new ProtobufVarint32LengthFieldPrepender());
						p.addLast("protobufEncoder", new ProtobufEncoder());

						p.addLast("handler", new MessageReceiver());
					}
				});
		try {
			ch = b.bind(TriangleConf.PORT_LOGINSERVER_COMMUNICATOR).sync().channel();
			ch.closeFuture().sync();
			Log.info("LoginServer Communicator Start to Listen");
		} catch (InterruptedException e) {
//			e.printStackTrace();
			Log.info("Shutdown Interrupt Detected");
			workerGroup.shutdownGracefully();
			bossGroup.shutdownGracefully();
		} 
	}

	private class MessageReceiver extends
			SimpleChannelInboundHandler<InterServerMessage> {
		@Override
		public void messageReceived(ChannelHandlerContext ctx, InterServerMessage msg) throws Exception {
			switch (msg.getInterServerMessageType()) {
			case ServerUp:
				Log.info("Server <" + msg.getServerUp().getServerName() +"> Online");
				GameServerList.addGameServer(
						msg.getServerUp().getServerName(), ctx.channel());
				break;

			case HeartBeat:
				HeartBeatSender.resetServerResponseTime(msg.getHeartBeat().getServerName());
				break;
			default:
				break;
			}
			// LoginTaskRunner.taskQ.put(new LoginTask(msg, ctx.channel()));
		}
	}
}