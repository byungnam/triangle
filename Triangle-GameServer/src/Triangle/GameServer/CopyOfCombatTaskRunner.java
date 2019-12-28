package Triangle.GameServer;

import java.util.Iterator;
import java.util.LinkedList;
import java.util.List;
import java.util.PriorityQueue;

import org.apache.log4j.Logger;

import Triangle.Commons.Global;
import Triangle.GameServerTask.GameTask;
import Triangle.Protocol.GameProtocol.CombatResult;
import Triangle.Protocol.GameProtocol.CombatReward;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.Protocol.GameProtocol.ProtocolUnit;
import Triangle.TriangleValues.TriangleValues.UnitClass;
import Triangle.Unit.Soldier;
import Triangle.Unit.UnitCombatable;
import Triangle.Unit.Tactic.Tactic;

public class CopyOfCombatTaskRunner implements Runnable {
	private Logger Log = Logger.getLogger(this.getClass());
	
	public List<Integer> leftTeamIdx;
	public List<Integer> rightTeamIdx;

	public List<UnitCombatable> liveLeftTeam;
	public List<UnitCombatable> liveRightTeam;
	public List<UnitCombatable> deadLeftTeam;
	public List<UnitCombatable> deadRightTeam;
	public PriorityQueue<UnitCombatable> unitQueue;

	public void run() {
		while (Global.serverLive) {
			try {
				GameTask task = Global.combatTaskQueue.take();
				Log.info("Combat task start");
				prepare(task);
				Log.info("Combat task prepared");
				boolean result = start(task);
				if (result) {
					Log.info("PLAYER WIN!! Gaining EXP...");
				} else {
					Log.info("PLAYER LOSE!!");
				}
				
				Global.taskQueue.put(new GameTask(task.taskNumber, task.message.toBuilder()
						.setGameMessageType(GameMessageType.CombatResult)
						.setCombatResult(CombatResult.newBuilder().setCombatResult(result)
						.setCombatReward(CombatReward.newBuilder().setExp(0))).build()));
				
			} catch (InterruptedException e) {
				Log.info("Shutdown Interrupt Detected");
			}
		}
	}

	public void prepare(GameTask task) {

		liveLeftTeam = new LinkedList<>();
		liveRightTeam = new LinkedList<>();
		deadLeftTeam = new LinkedList<UnitCombatable>();
		deadRightTeam = new LinkedList<UnitCombatable>();
		unitQueue = new PriorityQueue<UnitCombatable>();

		for (ProtocolUnit protoUnit : task.message.getCombatRequest().getAllyUnitList()) {
			UnitCombatable unit = null;
			switch (UnitClass.valueOf(protoUnit.getCls())) {
			case MAGE:
				unit = new Soldier(protoUnit);
				break;
			case MEDIC:
				unit = new Soldier(protoUnit);
				break;
			case PRIEST:
				unit = new Soldier(protoUnit);
				break;
			case SOLDIER:
				unit = new Soldier(protoUnit);
				break;
			case BEGGINER:
				unit = new Soldier(protoUnit);
				break;
			default:
				break;
			}

			liveLeftTeam.add(unit);
			unit.setAllies(liveLeftTeam);
			unit.setEnemies(liveRightTeam);
			unitQueue.add(unit);
		}
		
		Log.info("Ally prepared");

		for (ProtocolUnit protoUnit : task.message.getCombatRequest().getEnemyUnitList()) {
			UnitCombatable unit = null;
			switch (UnitClass.valueOf(protoUnit.getCls())) {
			case MAGE:
				unit = new Soldier(protoUnit);
				break;
			case MEDIC:
				unit = new Soldier(protoUnit);
				break;
			case PRIEST:
				unit = new Soldier(protoUnit);
				break;
			case SOLDIER:
				unit = new Soldier(protoUnit);
				break;
			case BEGGINER:
				unit = new Soldier(protoUnit);
				break;
			default:
				break;
			}

			liveRightTeam.add(unit);
			unit.setAllies(liveRightTeam);
			unit.setEnemies(liveLeftTeam);
			unitQueue.add(unit);
		}
		
		Log.info("Enemy prepared");
	}

	public boolean start(GameTask task) {
		for (int Turn = 0; Turn < 100; Turn++) {
			System.out.println("Turn " + Turn);
			// System.out.println("! UnitQueue : " + unitQueue);
			System.out.println("@ LiveLeft : " + liveLeftTeam);
			System.out.println("# LiveRight : " + liveRightTeam);
			// System.out.println("$ DeadLeft : " + deadLeftTeam);
			// System.out.println("% DeadRight : " + deadRightTeam);

			UnitCombatable unit = unitQueue.peek();
			startupPhase(unit);
			mainPhase(unit);
			endPhase(unit);
			turnEnd();
			if (isCombatEnd()) {
				break;
			}
		}

		return liveRightTeam.isEmpty();
	}

	public void startupPhase(UnitCombatable unit) {
		unit.adaptEffects();
	}

	public void mainPhase(UnitCombatable unit) {
		Tactic tactic;
		if ((tactic = unit.selectTactic()) != null) {
			System.out.println(unit.getUnitName() + "\t\t" + tactic.getCondition()
					+ " " + tactic.getValue() + " " + tactic.getAction() + " "
					+ tactic.getActionLevel());
			if (unit.doAction(tactic.getAction(), tactic.getActionLevel()) == false) {
				System.out.println("Cost not enough");
			}
		} else {
			System.out.println("Unit " + unit.getUnitName()
					+ " No Matched Condition. Ignoring this turn.");
		}
	}

	public void endPhase(UnitCombatable unit) {
		unit.adaptEffects();
		int time = unit.getTimer();
		for (UnitCombatable u : unitQueue) {
			u.reduceTimer(time);
		}
		unitQueue.remove();
		unit.addTimer();
		unitQueue.add(unit);

	}

	public void turnEnd() {
		Iterator<UnitCombatable> iter;

		iter = liveLeftTeam.iterator();
		while (iter.hasNext()) {
			UnitCombatable unit = iter.next();
			if (unit.getHp() <= 0) {
				System.out.println("dead : " + unit.getUnitName());
				iter.remove();
				deadLeftTeam.add(unit);
				unitQueue.remove(unit);
			}
		}

		iter = liveRightTeam.iterator();
		while (iter.hasNext()) {
			UnitCombatable unit = iter.next();
			if (unit.getHp() <= 0) {
				System.out.println("dead : " + unit.getUnitName());
				iter.remove();
				deadRightTeam.add(unit);
				unitQueue.remove(unit);
			}
		}
	}

	public boolean isCombatEnd() {
		return liveLeftTeam.isEmpty() || liveRightTeam.isEmpty();
	}
}
