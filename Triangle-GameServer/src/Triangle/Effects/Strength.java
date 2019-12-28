package Triangle.Effects;

import Triangle.Unit.UnitCombatable;

public class Strength extends EffectBase{

	public Strength(UnitCombatable owner) {
		super(owner);
		this.modification.setStr(owner.getSpec().getStr());
	}

}
