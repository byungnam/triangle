package Triangle.Unit.Tactic;

import Triangle.TriangleValues.TriangleValues.Action;
import Triangle.TriangleValues.TriangleValues.Conditions;

public class Tactic implements Comparable<Tactic>, Cloneable {
	private int priority;
	private Conditions condition;
	private int value;
	private Action action;
	private int actionParam;

	public Tactic() {
		this.condition = Conditions.COND_ALWAYS;
		this.action = Action.BasicAttack;
	}

	public Tactic(int priority, Conditions condition, int value, Action action,
			int actionParam) {
		this.setPriority(priority);
		this.condition = condition;
		this.value = value;
		this.action = action;
		this.actionParam = actionParam;
	}

	public Tactic(int priority, int condition, int value, int action,
			int actionParam) {
		this.setPriority(priority);
		this.condition = Conditions.valueOf(condition);
		this.value = value;
		this.action = Action.valueOf(action);
		this.actionParam = actionParam;
	}

	public Tactic set(int priority, Conditions condition, int value, Action action, int actionParam) {
		this.setPriority(priority);
		this.condition = condition;
		this.value = value;
		this.action = action;
		this.actionParam = actionParam;
		return this;
	}

	public Tactic setPriority(int priority) {
		this.priority = priority;
		return this;
	}

	public int getPriority() {
		return priority;
	}

	public Tactic setCondition(Conditions condition) {
		this.condition = condition;
		return this;
	}

	public Tactic setCondition(int condition) {
		this.condition = Conditions.valueOf(condition);
		return this;
	}

	public Conditions getCondition() {
		return this.condition;
	}

	public Tactic setValue(int value) {
		this.value = value;
		return this;
	}

	public int getValue() {
		return this.value;
	}

	public Tactic setAction(Action action) {
		this.action = action;
		return this;
	}

	public Tactic setAction(int action) {
		this.action = Action.valueOf(action);
		return this;
	}

	public Action getAction() {
		return this.action;
	}

	public Tactic setActionLevel(int actionParam) {
		this.actionParam = actionParam;
		return this;
	}

	public int getActionLevel() {
		return actionParam;
	}

	public int compareTo(Tactic o) {
		return this.priority - o.priority;
	}

	public String toString() {
		return new String("Tactic Info. priority : " + priority + " " + condition
				+ " " + action);
	}

	public Tactic clone() {
		try {
			return (Tactic) super.clone();
		} catch (CloneNotSupportedException e) {
			e.printStackTrace();
			return null;
		}
	}

}
