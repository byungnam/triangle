package Triangle.GameServer;

import java.util.Iterator;
import java.util.LinkedList;
import java.util.List;
import java.util.PriorityQueue;

import org.apache.log4j.Logger;

import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.ProtocolUnit;
import Triangle.TriangleValues.TriangleValues.UnitClass;
import Triangle.Unit.Soldier;
import Triangle.Unit.UnitCombatable;
import Triangle.Unit.Tactic.Tactic;

public class CombatTaskRunner {
	private Logger Log = Logger.getLogger(this.getClass());

	public List<Integer> leftTeamIdx;
	public List<Integer> rightTeamIdx;

	private boolean result;
	
	private List<UnitCombatable> liveLeftTeam;
	private List<UnitCombatable> liveRightTeam;
	private List<UnitCombatable> deadLeftTeam;
	private List<UnitCombatable> deadRightTeam;
	private PriorityQueue<UnitCombatable> unitQueue;

	public void prepare(GameMessage gmsg) {

		liveLeftTeam = new LinkedList<>();
		liveRightTeam = new LinkedList<>();
		deadLeftTeam = new LinkedList<UnitCombatable>();
		deadRightTeam = new LinkedList<UnitCombatable>();
		unitQueue = new PriorityQueue<UnitCombatable>();

		for (ProtocolUnit protoUnit : gmsg.getCombatRequest().getAllyUnitList()) {
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

		for (ProtocolUnit protoUnit : gmsg.getCombatRequest().getEnemyUnitList()) {
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

	public void start() {
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

		result = liveRightTeam.isEmpty();
		return;
	}

	public void startupPhase(UnitCombatable unit) {
		unit.adaptEffects();
	}

	public void mainPhase(UnitCombatable unit) {
		Tactic tactic;
		if ((tactic = unit.selectTactic()) != null) {
			System.out.println(unit.getUnitName() + " is now doing : [" + tactic.getCondition()
					+ " " + tactic.getValue() + " " + tactic.getAction() + " "
					+ tactic.getActionLevel() + "]");
			if (unit.doAction(tactic.getAction(), tactic.getActionLevel()) == false) {
				System.out.println("Not enough resource to do that.");
			}
		} else {
			System.out.println(unit.getUnitName()
					+ " skips this turn. Cannot find any matching condition.");
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

	public boolean getResult() {
		return result;
	}
}
