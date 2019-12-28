package Triangle.Actions;

import Triangle.CombatField.Dictionary;
import Triangle.CombatField.Give;

public class StrUp extends ActionBase {

	public StrUp() {
		this.descriptor = Dictionary.skillDictionary.get("StrUp");
	}

	protected ActionBase setTarget() {
		target = caster;
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
