package Triangle.Unit;

public class UnitSpec {
	private int str;
	private int dex;
	private int vital;
	private int intel;
	private int speed;
	
	
	public UnitSpec(){
		this.str = 0;
		this.dex = 0;
		this.vital = 0;
		this.intel = 0;
		this.speed = 0;
	}
	
	public UnitSpec(int str, int dex, int vital, int intel, int speed){
		this.str = str;
		this.dex = dex;
		this.vital = vital;
		this.intel = intel;
		this.speed = speed;
	}
	
	public void addAttribute(UnitSpec spec){
		this.str += spec.getStr();
		this.dex += spec.getDex();
		this.vital += spec.getVital();
		this.intel += spec.getIntel();
		this.speed += spec.getSpeed();
	}
	
	public void addAttribute(int str, int dex, int vital, int intel, int speed){
		this.str += str;
		this.dex += dex;
		this.vital += vital;
		this.intel += intel;
		this.speed += speed;
	}
	
	public void setAttribute(int str, int dex, int vital, int intel, int speed){
		this.str = str;
		this.dex = dex;
		this.vital = vital;
		this.intel = intel;
		this.speed = speed;
	}
	
	public void setStr(int str) {
		this.str = str;
	}

	public int getStr() {
		return this.str;
	}

	public void setDex(int dex) {
		this.dex = dex;
	}

	public int getDex() {
		return this.dex;
	}

	public void setVital(int vital) {
		this.vital = vital;
	}

	public int getVital() {
		return this.vital;
	}

	public void setIntel(int intel) {
		this.intel = intel;
	}

	public int getIntel() {
		return this.intel;
	}

	public void setSpeed(int speed) {
		this.speed = speed;
	}

	public int getSpeed() {
		return this.speed;
	}
	
	public String toString(){
		return new String(this.str + " " + this.dex + " "+ this.vital +" "+ this.intel + " " + this.speed);
	}

}
