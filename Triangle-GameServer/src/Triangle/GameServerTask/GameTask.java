package Triangle.GameServerTask;

import Triangle.Protocol.GameProtocol.GameMessage;

public class GameTask {
	public int taskNumber;
	public GameMessage message;

	public GameTask(int taskNumber, GameMessage message) {
		this.taskNumber = taskNumber;
		this.message = message;
	}
}