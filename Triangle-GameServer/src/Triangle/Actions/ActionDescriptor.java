package Triangle.Actions;

import Triangle.TriangleValues.TriangleValues.SkillType;

public class ActionDescriptor {
	
	private int id;
	private String skillName;
	private String toolTip;

	private int hpCost;
	private int mpCost;
	private int kiCost;
	private int ammoCost;

	private int hpMod;
	private int mpMod;
	private int kiMod;
	private int ammoMod;
	private String[] effect;
	
	private int castingDelay;
	private int parentSkill;
	private int afterEffect;
	private int duration;
	private SkillType skillType;
	private int numberOfTargets;
	private int targetables;
	
	public int getId() {
		return id;
	}
	public void setId(int id) {
		this.id = id;
	}
	public String getSkillName() {
		return skillName;
	}
	public void setSkillName(String skillName) {
		this.skillName = skillName;
	}
	public String getToolTip() {
		return toolTip;
	}
	public void setToolTip(String toolTip) {
		this.toolTip = toolTip;
	}
	public int getHpCost() {
		return hpCost;
	}
	public void setHpCost(int hpCost) {
		this.hpCost = hpCost;
	}
	public int getMpCost() {
		return mpCost;
	}
	public void setMpCost(int mpCost) {
		this.mpCost = mpCost;
	}
	public int getKiCost() {
		return kiCost;
	}
	public void setKiCost(int kiCost) {
		this.kiCost = kiCost;
	}
	public int getAmmoCost() {
		return ammoCost;
	}
	public void setAmmoCost(int ammoCost) {
		this.ammoCost = ammoCost;
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
	public String[] getEffect() {
		return effect;
	}
	public void setEffect(String[] effect) {
		this.effect = effect;
	}
	public int getCastingDelay() {
		return castingDelay;
	}
	public void setCastingDelay(int castingDelay) {
		this.castingDelay = castingDelay;
	}
	public int getParentSkill() {
		return parentSkill;
	}
	public void setParentSkill(int parentSkill) {
		this.parentSkill = parentSkill;
	}
	public int getAfterEffect() {
		return afterEffect;
	}
	public void setAfterEffect(int afterEffect) {
		this.afterEffect = afterEffect;
	}
	public int getDuration() {
		return duration;
	}
	public void setDuration(int duration) {
		this.duration = duration;
	}
	public SkillType getSkillType() {
		return skillType;
	}
	public void setSkillType(SkillType skillType) {
		this.skillType = skillType;
	}
	public int getNumberOfTargets() {
		return numberOfTargets;
	}
	public void setNumberOfTargets(int numberOfTargets) {
		this.numberOfTargets = numberOfTargets;
	}
	public int getTargetables() {
		return targetables;
	}
	public void setTargetables(int targetables) {
		this.targetables = targetables;
	}
	public String toString() {
		return new String("skillName:" + this.skillName + ", id:" + this.id + ", damage:"
				+ this.hpMod + "/" + this.mpMod + "/" + this.kiMod + "/" + this.ammoMod
				+ ", skillType:" + this.skillType);
	}
}
