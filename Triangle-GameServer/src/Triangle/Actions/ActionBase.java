package Triangle.Actions;

import Triangle.CombatField.Give;
import Triangle.Unit.UnitCombatable;

public abstract class ActionBase {

	protected UnitCombatable caster;
	protected UnitCombatable target;
	protected Give give;
	protected ActionDescriptor descriptor;
	
	public abstract void execute();

	protected abstract ActionBase setTarget();

	public ActionBase(){
	}
	
	public ActionBase(ActionBase base){
		this.caster = base.caster;
		this.target = base.target;
		this.give = base.give;
		this.descriptor = base.descriptor;
	}
	
	public ActionDescriptor getDescriptor() {
		return descriptor;
	}
	
	public ActionBase setCaster(UnitCombatable caster) {
		this.caster = caster;
		return this;
	}

	@Override
	public String toString() {
		return new String(this.caster.getUnitName() + " gives "
				+ this.getClass().getSimpleName() + " to " + target.getUnitName() + " "
				+ give);
	}
}
