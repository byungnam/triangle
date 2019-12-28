package Triangle.TriangleValues;

import java.util.HashMap;
import java.util.Map;

public class TriangleValues {
	
	public static final boolean FOR_TEST = false;
	public static final boolean FOR_SERVICE = true;

	public static enum StatusType {
		MANA, HEALTH, KI, AMMO, NONE;

		public static String[] toReadableArray() {
			StatusType[] arrayStatusType = StatusType.values();
			String[] array = new String[arrayStatusType.length];
			for (int i = 0; i < arrayStatusType.length; i++) {
				array[i] = arrayStatusType[i].name();
			}

			return array;
		}
	}

	public static enum Effect {
		NONE, SLOW, QUICK, SILENCE, WOUNDED, POISONED, UNARMORED, HEAVYARMORED;

		public static String[] toReadableArray() {
			Effect[] arrayEffect = Effect.values();
			String[] array = new String[arrayEffect.length];
			for (int i = 0; i < arrayEffect.length; i++) {
				array[i] = arrayEffect[i].name();
			}

			return array;
		}

	}

	public static enum Status {
		DEAD
	}

	public static enum SkillType {
		ACTIVE, AURA, PASSIVE;

		public static String[] toArray() {
			SkillType[] arraySkillType = SkillType.values();
			String[] array = new String[arraySkillType.length];
			for (int i = 0; i < arraySkillType.length; i++) {
				array[i] = arraySkillType[i].toString();
			}

			return array;
		}

	}

	public static enum Conditions {
		COND_ALWAYS("항상", 0),
		COND_HP_UPPER_THAN("자신의 생명력이 x0% 이상", 1),
		COND_HP_LOWER_THAN("자신의 생명력이 x0% 이하", 2),
		COND_MP_UPPER_THAN("자신의 마법력이 x0% 이상", 3),
		COND_MP_LOWER_THAN("자신의 마법력이 x0% 이하", 4),
		COND_TEAM_HP_UPPER_THAN("아군 한 명의 생명력이 x0% 이상", 5),
		COND_TEAM_HP_LOWER_THAN("아군 한 명의 생명력이 x0% 이하", 6),
		COND_TEAM_MP_UPPER_THAN("아군 한 명의 마법력이 x0% 이상", 7),
		COND_TEAM_MP_LOWER_THAN("아군 한 명의 마법력이 x0% 이하", 8),
		COND_TEAM_MEAN_HP_UPPER_THAN("아군의 평균 생명력이 x0% 이상", 9),
		COND_TEAM_MEAN_HP_LOWER_THAN("아군의 평균 생명력이 x0% 이하", 10),
		COND_TEAM_MEAN_MP_UPPER_THAN("아군의 평균 마법력이 x0% 이상", 11),
		COND_TEAM_MEAN_MP_LOWER_THAN("아군의 평균 마법력이 x0% 이하", 12),
		COND_AT_MOST("적어도 x회", 13),
		COND_AFTER_TURN("자신의 x번째 행동 이후부터", 14),
		COND_BEFORE_TURN("자신의 x번째 행동 이전까지", 15),
		COND_AT_TURN("자신의 x째 행동 일 때", 16),
		COND_FOR_EACH_TURN("매 x째 행동 마다", 17);

		private String value;
		private int number;
		private static Map<Integer, Conditions> map = new HashMap<>();

		static {
			for (Conditions c : Conditions.values()) {
				map.put(c.number, c);
			}
		}

		public static Conditions valueOf(int number) {
			return map.get(number);
		}

		private Conditions(String s, int i) {
			value = s;
			number = i;
		}

		public String getValue() {
			return value;
		}

		public int getNumber() {
			return number;
		}

		public static String[] toReadableArray() {
			Conditions[] arrayConditions = Conditions.values();
			String[] array = new String[arrayConditions.length];
			for (int i = 0; i < arrayConditions.length; i++) {
				array[i] = arrayConditions[i].getValue();
			}
			return array;
		}

	}

	public static int[] Values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
	public static String[] ValuesString = { "" + 1, "" + 2, "" + 3, "" + 4,
			"" + 5, "" + 6, "" + 7, "" + 8, "" + 9, "" + 10 };

	public static enum Action {
		BasicAttack("기본 공격", 0),
		FireArrow("화시", 1),
		Artillery("포대 사격", 2),
		Heal("치료", 3),
		StrUp("힘 증가", 4);

		private String value;
		private int number;
		private static Map<Integer, Action> map = new HashMap<>();

		static {
			for (Action a : Action.values()) {
				map.put(a.number, a);
			}
		}

		public static Action valueOf(int number) {
			return map.get(number);
		}

		private Action(String s, int i) {
			value = s;
			number = i;
		}

		public String getValue() {
			return value;
		}

		public int getNumber() {
			return number;
		}

		public static String[] toReadableArray() {
			Action[] arrayAction = Action.values();
			String[] array = new String[arrayAction.length];
			for (int i = 0; i < arrayAction.length; i++) {
				array[i] = arrayAction[i].getValue();
			}

			return array;
		}

	}

	public static final int TIME = 1000;

	public static final int[] expTable = new int[] { 200, 400, 800, 1600, 3200,
			6400, 12800, 25600, 51200, 102400 };

	public static enum UnitClass {
		BEGGINER(0), SOLDIER(1), MAGE(2), MEDIC(3), PRIEST(4);

		private int number;
		private static Map<Integer, UnitClass> map = new HashMap<>();

		static {
			for (UnitClass u : UnitClass.values()) {
				map.put(u.number, u);
			}
		}

		public static UnitClass valueOf(int number) {
			return map.get(number);
		}

		private UnitClass(int i) {
			number = i;
		}

		public int getNumber() {
			return number;
		}

		public static String[] toReadableArray() {
			UnitClass[] arrayUnitClass = UnitClass.values();
			String[] array = new String[arrayUnitClass.length];
			for (int i = 0; i < arrayUnitClass.length; i++) {
				array[i] = arrayUnitClass[i].name();
			}

			return array;
		}

	}

	public static enum AttackType {
		TYPE_NON, TYPE_ELECTROMAGNETIC, TYPE_THERMAL, TYPE_KINETIC, TYPE_EXPLOSION;
	}

	public static enum BattleField {
		One("레벨1",0), Two("레벨2",1), Three("레벨3",2), Four("레벨4",3), Four1("레벨4",4), Four2("레벨4",5), Four3("레벨4",6);
		String value;
		int number;

		private BattleField(String value, int number) {
			this.value = value;
			this.number = number;
		}
		
		public static String[] toReadableArray() {
			BattleField[] arrayBattleField = BattleField.values();
			String[] array = new String[arrayBattleField.length];
			for (int i = 0; i < arrayBattleField.length; i++) {
				array[i] = arrayBattleField[i].name();
			}

			return array;
		}
		
		public String getValue(){
			return value;
		}
		
		public int getNumber(){
			return number;
		}
	}
	
	/*
	 * public static enum ACTIONS { ATTACK, HEAL, MANAGEN, REINFORCE, CURSE,
	 * SUMMON, MOVE, REST;
	 * 
	 * public int getValue(){ switch(this){ case ATTACK: return 0; case CURSE:
	 * return 1; case HEAL: return 2; case MANAGEN: return 3; case MOVE: return 4;
	 * case REINFORCE: return 5; case REST: return 6; case SUMMON: return 7;
	 * default: return -1; } } }
	 * 
	 * public static enum WARRIOR_ATTACKS { BASICATTACK, STAB, DOUBLESLASH }
	 * 
	 * public static enum ARCHOR_ATTACKS { BASICATTACK, TRIPLESHOT, POISONARROW,
	 * AIMSHOT }
	 * 
	 * public static enum MAGE_ATTACKS { BASICATTACK, LIGHTNINGVOLT, FIREWALL,
	 * METHEOSTRIKE }
	 * 
	 * public static enum PRIEST_ATTACKS { BASICATTACK; }
	 * 
	 * public static enum WARRIOR_HEAL { SELFHEAL; }
	 * 
	 * public static enum ARCHOR_HEAL { }
	 * 
	 * public static enum MAGE_HEAL { }
	 * 
	 * public static enum PRIEST_HEAL { SELFHEAL, HEAL, DUALHEAL, HEAL2,
	 * PARTYHEAL; }
	 * 
	 * public static enum WARRIOR_MANAGEN {
	 * 
	 * }
	 * 
	 * public static enum ARCHOR_MANAGEN {
	 * 
	 * }
	 * 
	 * public static enum MAGE_MANAGEN { MANA_RESTORE, MANA_ABSORB, MANA_STEAL; }
	 * 
	 * public static enum PRIEST_MANAGEN { MEDITATION, BLESS; }
	 */

}
