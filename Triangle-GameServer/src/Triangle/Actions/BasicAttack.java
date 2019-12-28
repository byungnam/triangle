package Triangle.Actions;

import java.util.Random;

import Triangle.CombatField.Dictionary;
import Triangle.CombatField.Give;

public class BasicAttack extends ActionBase {

	public BasicAttack() {
		this.descriptor = Dictionary.skillDictionary.get("BasicAttack");
	}

	protected ActionBase setTarget() {
		int r = new Random().nextInt(this.caster.getEnemies().size());
		this.target = caster.getEnemies().get(r);
		return this;
	}

	protected ActionBase empower() {
		this.give = new Give((int) (descriptor.getHpMod()*(1 + caster.getModifiedSpec().getStr()*0.05)
				+ caster.getSpec().getStr()*0.05), descriptor.getMpMod(), descriptor.getKiMod(),
				descriptor.getAmmoMod(), descriptor.getEffect());
		return this;
	}

	public void execute() {
		setTarget();
		empower();
		target.take(give);
	}
}
