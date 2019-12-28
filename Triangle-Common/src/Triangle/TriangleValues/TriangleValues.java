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
		COND_ALWAYS("�׻�", 0),
		COND_HP_UPPER_THAN("�ڽ��� ������� x0% �̻�", 1),
		COND_HP_LOWER_THAN("�ڽ��� ������� x0% ����", 2),
		COND_MP_UPPER_THAN("�ڽ��� �������� x0% �̻�", 3),
		COND_MP_LOWER_THAN("�ڽ��� �������� x0% ����", 4),
		COND_TEAM_HP_UPPER_THAN("�Ʊ� �� ���� ������� x0% �̻�", 5),
		COND_TEAM_HP_LOWER_THAN("�Ʊ� �� ���� ������� x0% ����", 6),
		COND_TEAM_MP_UPPER_THAN("�Ʊ� �� ���� �������� x0% �̻�", 7),
		COND_TEAM_MP_LOWER_THAN("�Ʊ� �� ���� �������� x0% ����", 8),
		COND_TEAM_MEAN_HP_UPPER_THAN("�Ʊ��� ��� ������� x0% �̻�", 9),
		COND_TEAM_MEAN_HP_LOWER_THAN("�Ʊ��� ��� ������� x0% ����", 10),
		COND_TEAM_MEAN_MP_UPPER_THAN("�Ʊ��� ��� �������� x0% �̻�", 11),
		COND_TEAM_MEAN_MP_LOWER_THAN("�Ʊ��� ��� �������� x0% ����", 12),
		COND_AT_MOST("��� xȸ", 13),
		COND_AFTER_TURN("�ڽ��� x��° �ൿ ���ĺ���", 14),
		COND_BEFORE_TURN("�ڽ��� x��° �ൿ ��������", 15),
		COND_AT_TURN("�ڽ��� x° �ൿ �� ��", 16),
		COND_FOR_EACH_TURN("�� x° �ൿ ����", 17);

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
		BasicAttack("�⺻ ����", 0),
		FireArrow("ȭ��", 1),
		Artillery("���� ���", 2),
		Heal("ġ��", 3),
		StrUp("�� ����", 4);

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
		One("����1",0), Two("����2",1), Three("����3",2), Four("����4",3), Four1("����4",4), Four2("����4",5), Four3("����4",6);
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
