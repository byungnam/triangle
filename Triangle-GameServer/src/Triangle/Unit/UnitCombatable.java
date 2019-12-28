package Triangle.Unit;

import java.util.Iterator;
import java.util.LinkedList;
import java.util.List;
import java.util.Random;

import Triangle.Actions.ActionBase;
import Triangle.Actions.ActionDescriptor;
import Triangle.Actions.Artillery;
import Triangle.Actions.BasicAttack;
import Triangle.Actions.FireArrow;
import Triangle.Actions.Heal;
import Triangle.Actions.StrUp;
import Triangle.CombatField.Dictionary;
import Triangle.CombatField.Give;
import Triangle.Effects.EffectBase;
import Triangle.Protocol.GameProtocol.ProtocolTactic;
import Triangle.Protocol.GameProtocol.ProtocolUnit;
import Triangle.TriangleValues.TriangleValues;
import Triangle.TriangleValues.TriangleValues.Action;
import Triangle.Unit.Tactic.Tactic;

public abstract class UnitCombatable extends UnitBase implements
		Comparable<UnitCombatable> {

	private List<UnitCombatable> allies;
	private List<UnitCombatable> enemies;
	private int timer;
	private List<EffectBase> effect;
	private UnitSpec modifiedSpec;

	private int hp;
	private int mp;
	private int ki;
	private int ammo;
	
	private int maxHp;
	private int maxMp;
	private int maxKi;
	private int maxAmmo;

	private int def;
	private int mdef;

	private int hpDamageReduce;
	private int mpDamageReduce;

	private int elapsedTurn;
	private int executedNumberOfTactic[];

	public abstract void initiateStatus();

	public UnitCombatable(ProtocolUnit protoUnit) {
		setUnitName(protoUnit.getUnitName());
		getSpec().setAttribute(protoUnit.getStr(), protoUnit.getDex(), protoUnit.getVital(),
				protoUnit.getIntel(), protoUnit.getSpeed());
		for (ProtocolTactic protoTactic : protoUnit.getTacticsList()) {
			addTactics(new Tactic(protoTactic.getPriority(),
					protoTactic.getCondition(), protoTactic.getValue(),
					protoTactic.getAction(), protoTactic.getActionLevel()));
		}
		getDescriptor().setLevel(protoUnit.getLevel());
		initiateStatus();
		effect = new LinkedList<EffectBase>();

		modifiedSpec = new UnitSpec();
		elapsedTurn = 0;
		executedNumberOfTactic = new int[getTactics().size()];
	}

	public UnitSpec getModifiedSpec() {
		return modifiedSpec;
	}

	public void setmodifiedSpec(UnitSpec modifiedSpec) {
		this.modifiedSpec = modifiedSpec;
	}

	public int getHp() {
		return this.hp;
	}

	public int getMp() {
		return this.mp;
	}

	public int getKi() {
		return ki;
	}

	public int getAmmo() {
		return ammo;
	}

	public int getDef() {
		return this.def;
	}

	public int getMdef() {
		return this.mdef;
	}

	public void setHp(int hp) {
		this.hp = hp;
	}

	public void setMp(int mp) {
		this.mp = mp;
	}

	public void setKi(int ki) {
		this.ki = ki;
	}

	public void setAmmo(int ammo) {
		this.ammo = ammo;
	}

	public int getMaxHp() {
		return this.maxHp;
	}

	public int getMaxMp() {
		return this.maxMp;
	}

	public void setMaxHp(int maxHp) {
		this.maxHp = maxHp;
	}

	public void setMaxMp(int maxMp) {
		this.maxMp = maxMp;
	}

	public int getMaxKi() {
		return maxKi;
	}

	public void setMaxKi(int maxKi) {
		this.maxKi = maxKi;
	}

	public int getMaxAmmo() {
		return maxAmmo;
	}

	public void setMaxAmmo(int maxAmmo) {
		this.maxAmmo = maxAmmo;
	}
	
	public void setDef(int def) {
		this.def = def;
	}

	public void setMdef(int mdef) {
		this.mdef = mdef;
	}

	public List<EffectBase> getEffects() {
		return effect;
	}

	public void addEffect(EffectBase effect) {
		this.effect.add(effect);
	}

	public void removeEffect(EffectBase effect) {
		this.effect.remove(effect);
	}

	// public void removeLastRemovableEffect() {
	// for (int i = effect.size(); i > 0; i--) {
	// if (this.effect.get(i).descriptor.isRemovable()) {
	// this.effect.remove(i);
	// }
	// }
	// }

	public int compareTo(UnitCombatable o) {
		if (this.timer < o.timer) {
			return -1;
		} else if (this.timer > o.timer) {
			return 1;
		} else {
			return (new Random().nextBoolean() ? 1 : -1);
		}
	}

	public int getTimer() {
		return this.timer;
	}

	public void addTimer() {
		switch (getDescriptor().getCls()) {
		case SOLDIER:
			this.timer += (int) (TriangleValues.TIME / (1.10 * getSpec().getSpeed()));
			break;

		case MAGE:
			this.timer += (int) (TriangleValues.TIME / (0.90 * getSpec().getSpeed()));
			break;

		case MEDIC:
			this.timer += (int) (TriangleValues.TIME / (1.23 * getSpec().getSpeed()));
			break;

		case PRIEST:
			this.timer += (int) (TriangleValues.TIME / (1.10 * getSpec().getSpeed()));
			break;
			
		case BEGGINER:
			this.timer += (int) (TriangleValues.TIME / (1.00 * getSpec().getSpeed()));
			break;
		default:
			break;

		}
	}
	
	public void reduceTimer(int time) {
		this.timer -= time;
	}



	public List<UnitCombatable> getAllies() {
		return this.allies;
	}

	public void setAllies(List<UnitCombatable> allies) {
		this.allies = allies;
	}

	public List<UnitCombatable> getEnemies() {
		return enemies;
	}

	public void setEnemies(List<UnitCombatable> enemies) {
		this.enemies = enemies;
	}

	public Tactic selectTactic() {
		float sum = 0;
		int live = 0;
		float mean = 0;
		boolean selected = false;
		int tNum = -1;

		elapsedTurn++;
		Iterator<Tactic> iter = getTactics().iterator();

		while (iter.hasNext()) {
			Tactic tactic = iter.next();
			tNum++;
			switch (tactic.getCondition()) {
			case COND_HP_UPPER_THAN:
				if (((float) this.getHp() / this.getMaxHp()) >= (tactic.getValue() / 100.0)) {
					selected = true;
				}
				break;
			case COND_HP_LOWER_THAN:
				if ((float) this.getHp() / this.getMaxHp() <= (tactic.getValue() / 100.0)) {
					selected = true;
				}
				break;
			case COND_MP_UPPER_THAN:
				if ((float) this.getMp() / this.getMaxMp() >= (tactic.getValue() / 100.0)) {
					selected = true;
				}
				break;
			case COND_MP_LOWER_THAN:
				if ((float) this.getMp() / this.getMaxMp() <= (tactic.getValue() / 100.0)) {
					selected = true;
				}
				break;
			case COND_TEAM_HP_UPPER_THAN:
				for (UnitCombatable u : allies) {
					if (((float) u.getHp() / u.getMaxHp()) >= (tactic.getValue() / 100.0)) {
						selected = true;
					}
				}
				break;
			case COND_TEAM_HP_LOWER_THAN:
				for (UnitCombatable u : allies) {
					if (((float) u.getHp() / u.getMaxHp()) <= (tactic.getValue() / 100.0)) {
						selected = true;
					}
				}
				break;
			case COND_TEAM_MP_UPPER_THAN:
				for (UnitCombatable u : allies) {
					if (((float) u.getMp() / u.getMaxMp()) >= (tactic.getValue() / 100.0)) {
						selected = true;
					}
				}
				break;
			case COND_TEAM_MP_LOWER_THAN:
				for (UnitCombatable u : allies) {
					if (((float) u.getMp() / u.getMaxMp()) <= (tactic.getValue() / 100.0)) {
						selected = true;
					}
				}
				break;
			case COND_TEAM_MEAN_HP_UPPER_THAN:
				for (UnitCombatable u : allies) {
					if (u.getHp() > 0) {
						sum += (float) u.getHp() / u.getMaxHp();
						live++;
					}
				}
				mean = (float) (sum / live);
				if (mean >= (float) (tactic.getValue() / 100.0)) {
					selected = true;
				}
				break;
			case COND_TEAM_MEAN_HP_LOWER_THAN:
				for (UnitCombatable u : allies) {
					if (u.getHp() > 0) {
						sum += (float) u.getHp() / u.getMaxHp();
						live++;
					}
				}
				mean = (float) (sum / live);
				if (mean <= (tactic.getValue() / 100.0)) {
					selected = true;
				}
				break;
			case COND_TEAM_MEAN_MP_UPPER_THAN:
				for (UnitCombatable u : allies) {
					if (u.getHp() > 0) {
						sum += (float) u.getMp() / u.getMaxMp();
						live++;
					}
				}
				mean = (float) sum / live;
				if (mean >= (tactic.getValue() / 100.0)) {
					selected = true;
				}
				break;
			case COND_TEAM_MEAN_MP_LOWER_THAN:
				for (UnitCombatable u : allies) {
					if (u.getHp() > 0) {
						sum += (float) u.getMp() / u.getMaxMp();
						live++;
					}
				}
				mean = (float) sum / live;
				if (mean <= (tactic.getValue() / 100.0)) {
					selected = true;
				}
				break;

			// case UConsts.COND_AT_FRONT:
			// case UConsts.COND_AT_BEHIND:
			// case UConsts.COND_NUM_OURTEAM_AT_FRONT_UPPER:
			// case UConsts.COND_NUM_OURTEAM_AT_FRONT_LOWER:
			// case UConsts.COND_NUM_OURTEAM_AT_BEHIND_UPPER:
			// case UConsts.COND_NUM_OURTEAM_AT_BEHIND_LOWER:
			// case UConsts.COND_NUM_ENEMYTEAM_AT_FRONT_UPPER:
			// case UConsts.COND_NUM_ENEMYTEAM_AT_FRONT_LOWER:
			// case UConsts.COND_NUM_ENEMYTEAM_AT_BEHIND_UPPER:
			// case UConsts.COND_NUM_ENEMYTEAM_AT_BEHIND_LOWER:
			// case UConsts.COND_DO_WHILE_NTH:
			case COND_ALWAYS:
				selected = true;

			case COND_AT_MOST:
				if (executedNumberOfTactic[tNum] < tactic.getValue()) {
					executedNumberOfTactic[tNum]++;
					selected = true;
				}
				break;
			case COND_AT_TURN:
				if (elapsedTurn == tactic.getValue()) {
					selected = true;
				}
				break;
			case COND_BEFORE_TURN:
				if (elapsedTurn <= tactic.getValue()) {
					selected = true;
				}
				break;
			case COND_AFTER_TURN:
				if (elapsedTurn >= tactic.getValue()) {
					selected = true;
				}
				break;
			case COND_FOR_EACH_TURN:
				if (elapsedTurn % tactic.getValue() == 0) {
					selected = true;
				}
				break;
			default:
				break;
			}

			if (selected) {
				return tactic;
			}
		}
		return null;
	}
	
	public void adaptEffects(){
		UnitSpec modifiedSpec = new UnitSpec();
		for (EffectBase e : this.getEffects()){
			modifiedSpec.addAttribute(e.getModification());
		}
		this.setmodifiedSpec(modifiedSpec);
	}

	public boolean doAction(Action action, int level) {
		ActionBase base = null;
		if (payCost(Dictionary.skillDictionary.get(action.toString()))) {
			switch(action){
			case Artillery:
				base = new Artillery();
				break;
			case BasicAttack:
				base = new BasicAttack();
				break;
			case FireArrow:
				base = new FireArrow();
				break;
			case Heal:
				base = new Heal();
				break;
			case StrUp:
				base = new StrUp();
				break;
			default:
				break;
			
			}
			if(base != null){
				base.setCaster(this).execute();
				System.out.println(base.toString());
				return true;
			}
		}
		return false;
	}

	public boolean payCost(ActionDescriptor skillDescriptor) {
		boolean flag = true;
		if (this.getHp() < skillDescriptor.getHpCost()) {
			flag = false;
		} else {
			this.setHp(this.getHp() - skillDescriptor.getHpCost());
		}

		if (this.getMp() < skillDescriptor.getMpCost()) {
			flag = false;
		} else {
			this.setMp(this.getMp() - skillDescriptor.getMpCost());
		}

		/*
		 * if (this.getMp() < skillDescriptor.getMpCost()) { flag = false; } else {
		 * this.setMp(this.getMp() - skillDescriptor.getMpCost()); }
		 * 
		 * if (this.getMp() < skillDescriptor.getMpCost()) { flag = false; } else {
		 * this.setMp(this.getMp() - skillDescriptor.getMpCost()); }
		 */

		return flag;
	}
	
//	public EffectBase effectGenerator(String effect) {
//		try {
//			return (EffectBase) Class.forName("Triangle.Effects." + effect).getConstructor(
//					UnitCombatable.class).newInstance(this);
//
//		} catch (InstantiationException | IllegalAccessException
//				| IllegalArgumentException | InvocationTargetException
//				| NoSuchMethodException | SecurityException | ClassNotFoundException e) {
//			e.printStackTrace();
//		}
//		return null;
//	}

	public void take(Give give) {
//		if (give.effect != null) {
//			for (String e : give.effect) {
//				EffectBase effect = effectGenerator(e);
//				this.effect.add(effect);
//			}
//		}

		int effectiveValue;
		int remain;
		if (give.hpMod < 0) {
			effectiveValue = (int) (give.hpMod / (1 + 0.02 * this.getDef()) * (1 - this.getHpDamageReduce() * 0.01));
		} else {
			effectiveValue = give.hpMod;
		}
		System.out.println("Effective value = "	+	effectiveValue);
		remain = this.getHp() + effectiveValue;
		this.setHp(remain > 0 ? remain : 0);
		this.setHp(remain > this.getMaxHp() ? this.getMaxHp() : remain);

		if (give.mpMod < 0) {
			effectiveValue = (int) (give.mpMod / (1 + 0.02 * this.getMdef()) * (1 - this.getMpDamageReduce() * 0.01));
		} else {
			effectiveValue = give.mpMod;
		}
		System.out.println("Effective value = "	+	effectiveValue);
		remain = this.getMp() + effectiveValue;
		this.setMp(remain > 0 ? remain : 0);
		this.setMp(remain > this.getMaxMp() ? this.getMaxMp() : remain);

		// remain = this.getKi() + give.kiMod;
		// this.setKi(remain > 0 ? remain : 0);
		// this.setKi(remain > this.getMaxKi() ? this.getMaxKi() : remain);
		//
		// remain = this.getAmmo() + give.ammoMod;
		// this.setAmmo(remain > 0 ? remain : 0);
		// this.setAmmo(remain > this.getMaxAmmo() ? this.getMaxAmmo() : remain);

		// System.out.println("give : "+give);
	}

	@Override
	public String toString() {
		return new String("\n[" + this.getHp() + "/" + this.getMaxHp() + "\t"
				+ this.getMp() + "/" + this.getMaxMp() + "]  Time:" + this.getTimer() + "\t"
				+ this.getUnitName() + " SPEC: " + getSpec());
	}

	public int getHpDamageReduce() {
		return hpDamageReduce;
	}

	public void setHpDamageReduce(int hpDamageReduce) {
		this.hpDamageReduce = hpDamageReduce;
	}

	public int getMpDamageReduce() {
		return mpDamageReduce;
	}

	public void setMpDamageReduce(int mpDamageReduce) {
		this.mpDamageReduce = mpDamageReduce;
	}
}
