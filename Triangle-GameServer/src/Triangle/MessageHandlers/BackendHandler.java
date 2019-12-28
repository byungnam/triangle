package Triangle.MessageHandlers;

import io.netty.channel.Channel;
import io.netty.channel.ChannelHandlerAdapter;
import io.netty.channel.ChannelHandlerContext;
import Triangle.GameServer.CombatTaskRunner;
import Triangle.Protocol.GameProtocol.CombatResult;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;

public class BackendHandler extends ChannelHandlerAdapter {
	// private Logger Log = Logger.getLogger(this.getClass());

	private Channel inboundChannel;

	public BackendHandler(Channel inboundChannel) {
		this.inboundChannel = inboundChannel;
	}

	@Override
	public void channelRead(final ChannelHandlerContext ctx, Object msg) throws Exception {
		GameMessage gmsg = (GameMessage) msg;
		if (gmsg.getGameMessageType().equals(GameMessageType.CombatRequest)) {
			CombatTaskRunner combat = new CombatTaskRunner();
			combat.prepare(gmsg);
			combat.start();
			msg = GameMessage.newBuilder().setAccountNumber(gmsg.getAccountNumber()).setResult(true).setGameMessageType(
					GameMessageType.CombatResult).setCombatResult(
					CombatResult.newBuilder().setCombatResult(combat.getResult()));
		}
		inboundChannel.writeAndFlush(msg);
	}
}
