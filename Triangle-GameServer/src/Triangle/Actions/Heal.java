package Triangle.Actions;

import Triangle.CombatField.Dictionary;
import Triangle.CombatField.Give;
import Triangle.Unit.UnitCombatable;

public class Heal extends ActionBase {

	public Heal() {
		this.descriptor = Dictionary.skillDictionary.get("Heal");
	}

	protected ActionBase setTarget() {
		target = caster;
		for(UnitCombatable u : caster.getAllies()){
			if(u.getHp() < target.getHp()){
				target = u;
			}
		}
		return this;
	}

	protected ActionBase empower() {
		this.give = new Give((int) (descriptor.getHpMod()),
				descriptor.getMpMod(), descriptor.getKiMod(), descriptor.getAmmoMod(),
				descriptor.getEffect());
		return this;
	}

	public void execute() {
		setTarget();
		empower();
		target.take(give);
	}
}
