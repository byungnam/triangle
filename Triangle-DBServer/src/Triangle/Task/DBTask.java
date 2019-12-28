package Triangle.Task;

import Triangle.Protocol.GameProtocol.GameMessage;
import io.netty.channel.ChannelHandlerContext;

public class DBTask {
	public ChannelHandlerContext ctx;
	public GameMessage message;
	
	public DBTask(ChannelHandlerContext ctx, GameMessage message) {
		this.ctx = ctx;
		this.message = message;
	}
}
