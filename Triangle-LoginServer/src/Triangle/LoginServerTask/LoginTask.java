package Triangle.LoginServerTask;

import Triangle.Protocol.LoginProtocol.LoginMessage;
import io.netty.channel.Channel;

public class LoginTask {
	public LoginMessage loginMessage;
	public Channel channel;
	
	public LoginTask(LoginMessage loginMessage, Channel channel){
		this.loginMessage = loginMessage;
		this.channel = channel;
	}
}
