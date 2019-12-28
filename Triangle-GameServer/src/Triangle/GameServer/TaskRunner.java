package Triangle.GameServer;

import io.netty.channel.Channel;

import org.apache.log4j.Logger;

import Triangle.Commons.Global;
import Triangle.GameServerTask.GameTask;

public class TaskRunner implements Runnable {
	private Logger Log = Logger.getLogger(this.getClass());

	public void run() {
		GameTask task;
		while (Global.serverLive) {
			try {
				task = Global.taskQueue.take();
				Log.info("TASK : " + task.message.getGameMessageType().toString());
				switch (task.message.getGameMessageType()) {
				case CombatRequest:
					Global.combatTaskQueue.put(task);
					break;
				case CombatResult:
				case CreateTeam:
				case CreateUnit:
				case DeleteTeam:
				case DeleteUnit:
				case EditUnit:
				case GetTeamByAccountNumber:
				case GetTeamByUnitNumber:
				case GetUnitByAccountNumber:
				case GetUnitByTeamNumber:
				case GetUnitByUnitNumber:
				case CreateTactic:
				case DeleteTactic:
				case EditTactic:
				case EditTeam:
				case GetTactic:
				case RegisterUnit:
				case UnregisterUnit:
				case GetNPCTeam:
					Channel ch = Global.waitingTasks.remove(task.taskNumber);
					if(ch == null) {
						throw new Exception("Task Number " + task.taskNumber
								+ " was not in waitingTasks");
					}
					ch.writeAndFlush(task.message);
					Log.info("Sending result " + task.message.getResult() + " "
							+ task.message.getGameMessageType());
					break;

				default:
					throw new Exception(
							"Wrong Message Received in GameServer TaskRunner: "
									+ task.message.getGameMessageType());
				}
			} catch (InterruptedException e) {
				Log.info("Shutdown Interrupt Detected");
			} catch (Exception e) {
				e.printStackTrace();
			}
		}
	}
}
