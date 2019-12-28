package Triangle.Unit;

import Triangle.TriangleValues.TriangleValues.UnitClass;

public class UnitDescriptor {
	
	private int level;
	private UnitClass cls;

	private int exp;
//	private int nextLevelExp;
	private int point;
	
	public UnitDescriptor(){
		this.cls = UnitClass.SOLDIER;
	
		this.exp = 0;
//		this.nextLevelExp = 0;
		this.level = 0;
		this.point = 0;
	}
	
	public UnitDescriptor(UnitClass unitClass, int exp, int level, int point){
		this.cls = unitClass;
		
		this.exp = exp;
//		this.nextLevelExp = nextLevelExp;
		this.level = level;
		this.point = point;
	}
	
	public void setLevel(int lv) {
		this.level = lv;
	}

	public void setExp(int exp) {
		this.exp = exp;
	}

	public void setPoint(int p) {
		this.point = p;
	}
	
	public int getLevel() {
		return level;
	}

	public int getExp() {
		return exp;
	}

	public int getPoint() {
		return point;
	}

	public UnitClass getCls() {
		return cls;
	}

	public void setCls(UnitClass cls) {
		this.cls = cls;
	}

	public void setCls(int cls) {
		this.cls = UnitClass.valueOf(cls);
	}
	
//	public int getNextLevelExp() {
//		return nextLevelExp;
//	}
//
//	public void setNextLevelExp(int nextLevelExp) {
//		this.nextLevelExp = nextLevelExp;
//	}
}
