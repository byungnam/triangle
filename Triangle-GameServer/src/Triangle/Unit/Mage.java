package Triangle.Unit;

import Triangle.Protocol.GameProtocol.ProtocolUnit;
import Triangle.TriangleValues.TriangleValues.UnitClass;

public class Mage extends UnitCombatable {

	
	public Mage(ProtocolUnit protoUnit) {
		super(protoUnit);
	}

	@Override
	public void initiateStatus() {
		UnitSpec spec = getSpec();
		setDef(spec.getStr() * 1);
		setMdef(spec.getIntel() * 1);
		setMaxHp(spec.getVital() * 28);
		setMaxMp(spec.getIntel() * 10);
		setHp(getMaxHp());
		setMp(getMaxMp());
		getDescriptor().setCls(UnitClass.MAGE);
	}

}
