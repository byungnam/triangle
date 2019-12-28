package Triangle.Effects;

import Triangle.CombatField.Give;
import Triangle.Unit.UnitCombatable;
import Triangle.Unit.UnitSpec;


public abstract class EffectBase {

	EffectDescriptor descriptor;
	Give give;
	UnitCombatable owner;
	UnitSpec modification;
	
	public EffectBase(UnitCombatable owner){
		this.owner = owner;
		this.modification = new UnitSpec();
	}

	public EffectDescriptor getDescriptor() {
		return descriptor;
	}

	public void setDescriptor(EffectDescriptor descriptor) {
		this.descriptor = descriptor;
	}

	public Give getGive() {
		return give;
	}

	public void setGive(Give give) {
		this.give = give;
	}

	public UnitCombatable getOwner() {
		return owner;
	}

	public void setOwner(UnitCombatable owner) {
		this.owner = owner;
	}

	public UnitSpec getModification() {
		return modification;
	}

	public void setModification(UnitSpec modification) {
		this.modification = modification;
	}
	
	

}
