package Triangle.Effects;



public abstract class EffectDescriptor {
	private int effectId;
	private String effectName;
	private String toolTip;

	private boolean isBuff;
	private boolean isRemovable;
	private boolean isPreemptive;
	private boolean isModifier;
	private boolean isEoT;
	
	private int hpMod;
	private int mpMod;
	private int kiMod;
	private int ammoMod;
	private int timeMod;
	private int strMod;
	private int dexMod;
	private int intelMod;
	private int vitalMod;
	private int speedMod;

	private int elapsedTime;
	private int extinctTime;

	public int getEffectId() {
		return effectId;
	}

	public void setEffectId(int effectId) {
		this.effectId = effectId;
	}

	public String getEffectName() {
		return effectName;
	}

	public void setEffectName(String effectName) {
		this.effectName = effectName;
	}

	public String getToolTip() {
		return toolTip;
	}

	public void setToolTip(String toolTip) {
		this.toolTip = toolTip;
	}

	public boolean isBuff() {
		return isBuff;
	}

	public void setBuff(boolean isBuff) {
		this.isBuff = isBuff;
	}
	
	public boolean isPreemptive() {
		return isPreemptive;
	}

	public void setPreemptive(boolean isPreemptive) {
		this.isPreemptive = isPreemptive;
	}

	public int getHpMod() {
		return hpMod;
	}

	public void setHpMod(int hpMod) {
		this.hpMod = hpMod;
	}

	public int getMpMod() {
		return mpMod;
	}

	public void setMpMod(int mpMod) {
		this.mpMod = mpMod;
	}

	public int getKiMod() {
		return kiMod;
	}

	public void setKiMod(int kiMod) {
		this.kiMod = kiMod;
	}

	public int getAmmoMod() {
		return ammoMod;
	}

	public void setAmmoMod(int ammoMod) {
		this.ammoMod = ammoMod;
	}

	public int getTimeMod() {
		return timeMod;
	}

	public void setTimeMod(int timeMod) {
		this.timeMod = timeMod;
	}

	public int getStrMod() {
		return strMod;
	}

	public void setStrMod(int strMod) {
		this.strMod = strMod;
	}

	public int getDexMod() {
		return dexMod;
	}

	public void setDexMod(int dexMod) {
		this.dexMod = dexMod;
	}

	public int getIntelMod() {
		return intelMod;
	}

	public void setIntelMod(int intelMod) {
		this.intelMod = intelMod;
	}

	public int getVitalMod() {
		return vitalMod;
	}

	public void setVitalMod(int vitalMod) {
		this.vitalMod = vitalMod;
	}

	public int getSpeedMod() {
		return speedMod;
	}

	public void setSpeedMod(int speedMod) {
		this.speedMod = speedMod;
	}

	public int getElapsedTime() {
		return elapsedTime;
	}

	public void setElapsedTime(int elapsedTime) {
		this.elapsedTime = elapsedTime;
	}

	public int getExtinctTime() {
		return extinctTime;
	}

	public void setExtinctTime(int extinctTime) {
		this.extinctTime = extinctTime;
	}

	public boolean isRemovable() {
		return isRemovable;
	}

	public void setRemovable(boolean isRemovable) {
		this.isRemovable = isRemovable;
	}

	public boolean isModifier() {
		return isModifier;
	}

	public void setModifier(boolean isModifier) {
		this.isModifier = isModifier;
	}
	public boolean isEoT() {
		return isEoT;
	}
	public void setEoT(boolean isEoT) {
		this.isEoT = isEoT;
	}
}
