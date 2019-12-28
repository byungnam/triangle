package Triangle.Unit;

import java.util.List;
import java.util.PriorityQueue;

import Triangle.TriangleValues.TriangleValues.UnitClass;
import Triangle.Unit.Tactic.Tactic;



public class UnitBase {

	private String unitName;
	private int unitNumber;
	private int userNumber;
	private UnitSpec spec;
	private UnitDescriptor descriptor;
	private PriorityQueue<Tactic> tactics;


	public UnitBase() {
		this.unitName = new String("NoName");
		this.unitNumber = -1;
		this.userNumber = -1;
		this.spec = new UnitSpec();
		this.descriptor = new UnitDescriptor();
		this.tactics = new PriorityQueue<Tactic>();
	}

	public UnitBase(int unitNumber, String name, int str, int dex, int vital, int intel, int speed) {
		this.unitNumber = unitNumber;
		this.unitName = name;
		this.spec = new UnitSpec(str, dex, vital, intel, speed);
		this.descriptor = new UnitDescriptor();
		this.tactics = new PriorityQueue<Tactic>();
	}
	
	public UnitBase(int unitNumber, String name, int str, int dex, int vital, int intel, int speed, int cls, int exp, int level, int point) {
		this.unitNumber = unitNumber;
		this.unitName = name;
		this.spec = new UnitSpec(str, dex, vital, intel, speed);
		this.descriptor = new UnitDescriptor(UnitClass.valueOf(cls), exp, level, point);
		this.tactics = new PriorityQueue<Tactic>();
	}
	
	public UnitBase(int unitNumber, String name, int str, int dex, int vital, int intel, int speed, int level, int cls, int exp, int point, PriorityQueue<Tactic> tactics) {
		this.unitNumber = unitNumber;
		this.unitName = name;
		this.spec = new UnitSpec(str, dex, vital, intel, speed);
		this.descriptor = new UnitDescriptor(UnitClass.valueOf(cls), exp, level, point);
		this.tactics = tactics;
	}
	
	
	
	public UnitSpec getSpec() {
		return spec;
	}

	public void setSpec(UnitSpec spec) {
		this.spec = spec;
	}

	public UnitDescriptor getDescriptor() {
		return descriptor;
	}

	public void setDescriptor(UnitDescriptor descriptor) {
		this.descriptor = descriptor;
	}

	public String getUnitName() {
		return this.unitName;
	}

	public void setUnitName(String unitName) {
		this.unitName = unitName;
	}

	public int getUnitNumber() {
		return unitNumber;
	}

	public void setUnitNumber(int unitNumber) {
		this.unitNumber = unitNumber;
	}

	public int getUserNumber() {
		return userNumber;
	}

	public void setUserNumber(int userNumber) {
		this.userNumber = userNumber;
	}

	public void addTactics(Tactic tactic) {
		this.tactics.add(tactic);
	}

	public void setTactics(PriorityQueue<Tactic> tactics) {
		this.tactics.addAll(tactics);
	}
	
	public void setTactics(List<Tactic> tactics) {
		this.tactics.addAll(tactics);
	}

	public PriorityQueue<Tactic> getTactics() {
		return tactics;
	}


	@Override
	public String toString() {
		return new String("Unit Info : " + this.unitName + " " + this.descriptor.getCls() + " Lv."
				+ this.descriptor.getLevel() + ">");
	}

}
