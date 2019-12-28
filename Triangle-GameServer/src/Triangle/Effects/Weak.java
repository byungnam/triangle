package Triangle.Effects;

import Triangle.Unit.UnitCombatable;

public class Weak extends EffectBase{

	public Weak(UnitCombatable caster) {
		super(caster);
		this.modification.setStr(-10);
	}

//	@Override
//	public void execute(Give give) {
//		give.hpMod = (int) (give.hpMod * descriptor.getHpMod());
//	}
	
}
