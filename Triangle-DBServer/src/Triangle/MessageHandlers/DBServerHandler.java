package Triangle.MessageHandlers;

import io.netty.channel.ChannelHandlerAdapter;
import io.netty.channel.ChannelHandlerContext;

import org.apache.log4j.Logger;

import Triangle.DBServer.DBTaskRunner;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Task.DBTask;

public class DBServerHandler extends ChannelHandlerAdapter {
	private Logger Log = Logger.getLogger(this.getClass());

	@Override
	public void	channelActive(ChannelHandlerContext ctx){
//		Channel inboundChannel = ctx.channel();
	}
	
//	@Override
//	public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) throws Exception {
//		Log.info("DBserver exception");
//		cause.printStackTrace();
//		ctx.close();
//	}

	@Override
	public void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {
		Log.info("DBserver received message : " + ((GameMessage) msg).getGameMessageType());
		DBTaskRunner.taskQ.add(new DBTask(ctx, (GameMessage) msg));
	}
}
