package Triangle.MessageHandlers;

import io.netty.channel.Channel;
import io.netty.channel.ChannelInitializer;
import io.netty.channel.ChannelPipeline;
import io.netty.handler.codec.protobuf.ProtobufDecoder;
import io.netty.handler.codec.protobuf.ProtobufEncoder;
import io.netty.handler.codec.protobuf.ProtobufVarint32FrameDecoder;
import io.netty.handler.codec.protobuf.ProtobufVarint32LengthFieldPrepender;

import org.apache.log4j.Logger;

import Triangle.Protocol.GameProtocol.GameMessage;

public class ProtoDBServerChannelInitializer extends ChannelInitializer<Channel> {
	private Logger Log = Logger.getLogger(this.getClass());
	
	@Override
	protected void initChannel(Channel ch) throws Exception {
		ChannelPipeline p = ch.pipeline();
		p.addLast("frameDecoder", new ProtobufVarint32FrameDecoder());
		p.addLast("protobufDecoder", new ProtobufDecoder(
				GameMessage.getDefaultInstance()));

		p.addLast("frameEncoder", new ProtobufVarint32LengthFieldPrepender());
		p.addLast("protobufEncoder", new ProtobufEncoder());

		p.addLast("handler", new DBServerHandler());
	}
}
