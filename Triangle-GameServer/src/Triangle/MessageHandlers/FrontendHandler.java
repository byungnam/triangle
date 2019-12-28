package Triangle.MessageHandlers;

import io.netty.bootstrap.Bootstrap;
import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelHandlerAdapter;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.socket.nio.NioSocketChannel;

import org.apache.log4j.Logger;

import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.TriangleConfigure.TriangleConf;

public class FrontendHandler extends ChannelHandlerAdapter {
	private Logger Log = Logger.getLogger(this.getClass());

	private volatile Channel outboundChannel;

	@Override
	public void channelActive(ChannelHandlerContext ctx) {
		final Channel inboundChannel = ctx.channel();

		Bootstrap b = new Bootstrap();
		b.group(inboundChannel.eventLoop()).channel(NioSocketChannel.class).handler(
				new ProtoGameServerBackendChannelInitializer(inboundChannel));

		ChannelFuture f = b.connect(TriangleConf.ADDRESS_DBSERVER,
				TriangleConf.PORT_DBSERVER_COMMUNICATOR);
		outboundChannel = f.channel();
	}

	@Override
	public void channelRead(final ChannelHandlerContext ctx, Object msg) throws Exception {
		if (outboundChannel.isActive()) {
			outboundChannel.writeAndFlush(msg);
//			.addListener(new ChannelFutureListener() {
//				@Override
//				public void operationComplete(ChannelFuture future) throws Exception {
//					if(future.isSuccess()){
//						Log.error("success2");
//						ctx.channel().read();
//					} else {
//						future.channel().close();
//						Log.error("fail " + future.cause());
//						
//					}
//				}
//			});
			Log.info("Sending message " + ((GameMessage) msg).getGameMessageType());
		}
	}
	
	@Override
	public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) throws Exception {
		cause.printStackTrace();
		ctx.close();
	}
}
