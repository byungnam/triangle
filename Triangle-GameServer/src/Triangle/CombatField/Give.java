package Triangle.CombatField;


public class Give {
	public int hpMod;
	public int mpMod;
	public int kiMod;
	public int ammoMod;
	public String[] effect;
	
	public Give(int hpMod, int mpMod, int kiMod, int ammoMod, String[] effect){
		this.hpMod = hpMod;
		this.mpMod = mpMod;
		this.kiMod = kiMod;
		this.ammoMod = ammoMod;
		this.effect = effect;
	}
	
	public String toString(){
		StringBuilder builder = new StringBuilder();
		for(String e : effect){
			builder.append(e + " ");
		}
		return new String("Hp mod : "+hpMod + ", Mp mod : " + mpMod + ", " + builder.toString());
	}
}