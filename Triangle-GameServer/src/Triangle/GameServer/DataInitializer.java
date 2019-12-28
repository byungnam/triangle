package Triangle.GameServer;

import java.io.File;
import java.util.HashMap;

import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;

import org.w3c.dom.Document;
import org.w3c.dom.Element;
import org.w3c.dom.Node;
import org.w3c.dom.NodeList;

import Triangle.Actions.ActionDescriptor;
import Triangle.CombatField.Dictionary;
import Triangle.TriangleValues.TriangleValues.SkillType;

public class DataInitializer {

	private DataInitializer() {
		skillDescriptorReader();
//		effectDescriptorReader();
	}
	
	public static void initialize(){
		new DataInitializer();
	}

	public void skillDescriptorReader() {
		Dictionary.skillDictionary = new HashMap<String, ActionDescriptor>();

		Document doc = null;
		try {
			File file = new File("D:\\Triangle\\Skills.xml");
			DocumentBuilderFactory docBuildFact = DocumentBuilderFactory.newInstance();
			DocumentBuilder docBuild = docBuildFact.newDocumentBuilder();
			doc = docBuild.parse(file);
			doc.getDocumentElement().normalize();

			NodeList skillList = doc.getElementsByTagName("skillData");

		
			for (int i = 0; i < skillList.getLength(); i++) {
				Node skillNode = skillList.item(i);

				if (skillNode.getNodeType() == Node.ELEMENT_NODE) {
					Element sElement = (Element) skillNode;

					ActionDescriptor skillDescriptor = new ActionDescriptor();

					skillDescriptor.setId(Integer.parseInt(sElement.getAttribute("id")));
					skillDescriptor.setSkillName(sElement.getElementsByTagName("skillName").item(0).getTextContent());
					skillDescriptor.setToolTip(sElement.getElementsByTagName("toolTip").item(0).getTextContent());
					skillDescriptor.setHpCost(Integer.parseInt(sElement.getElementsByTagName("hpCost").item(0).getTextContent()));
					skillDescriptor.setMpCost(Integer.parseInt(sElement.getElementsByTagName("mpCost").item(0).getTextContent()));
					skillDescriptor.setKiCost(Integer.parseInt(sElement.getElementsByTagName("kiCost").item(0).getTextContent()));
					skillDescriptor.setAmmoCost(Integer.parseInt(sElement.getElementsByTagName("ammoCost").item(0).getTextContent()));
					skillDescriptor.setHpMod(Integer.parseInt(sElement.getElementsByTagName("hpMod").item(0).getTextContent()));
					skillDescriptor.setMpMod(Integer.parseInt(sElement.getElementsByTagName("mpMod").item(0).getTextContent()));
					skillDescriptor.setKiMod(Integer.parseInt(sElement.getElementsByTagName("kiMod").item(0).getTextContent()));
					skillDescriptor.setAmmoMod(Integer.parseInt(sElement.getElementsByTagName("ammoMod").item(0).getTextContent()));
					skillDescriptor.setCastingDelay(Integer.parseInt(sElement.getElementsByTagName("castingDelay").item(0).getTextContent()));
					skillDescriptor.setParentSkill(Integer.parseInt(sElement.getElementsByTagName("parentSkill").item(0).getTextContent()));
					skillDescriptor.setAfterEffect(Integer.parseInt(sElement.getElementsByTagName("afterEffect").item(0).getTextContent()));
					skillDescriptor.setDuration(Integer.parseInt(sElement.getElementsByTagName("duration").item(0).getTextContent()));

					NodeList effect = sElement.getElementsByTagName("effect");
					String[] effects = new String[effect.getLength()];
					for (int j = 0; j < effect.getLength(); j++) {
						effects[j] = effect.item(j).getTextContent();
					}
					skillDescriptor.setEffect(effects);
					
					skillDescriptor.setSkillType(SkillType.valueOf(sElement.getElementsByTagName("skillType").item(0).getTextContent()));
					skillDescriptor.setNumberOfTargets(Integer.parseInt(sElement.getElementsByTagName("numberOfTargets").item(0).getTextContent()));
					
					NodeList targetable = sElement.getElementsByTagName("targetable");
					for (int j = 0; j < targetable.getLength(); j++) {
						//targetable.item(j).getTextContent();
					}

					Dictionary.skillDictionary.put(skillDescriptor.getSkillName(), skillDescriptor);
				}
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
	}
	
//	public void effectDescriptorReader() {
//		Dictionary.skillDictionary = new HashMap<Action, SkillDescriptor>();
//
//		Document doc = null;
//		try {
//			File file = new File("D:\\Triangle\\Skills.xml");
//			DocumentBuilderFactory docBuildFact = DocumentBuilderFactory.newInstance();
//			DocumentBuilder docBuild = docBuildFact.newDocumentBuilder();
//			doc = docBuild.parse(file);
//			doc.getDocumentElement().normalize();
//
//			NodeList skillList = doc.getElementsByTagName("skillData");
//
//			for (int i = 0; i < skillList.getLength(); i++) {
//				Node skillNode = skillList.item(i);
//
//				if (skillNode.getNodeType() == Node.ELEMENT_NODE) {
//					Element sElement = (Element) skillNode;
//
//					SkillDescriptor skillDescriptor = new SkillDescriptor();
//
//					skillDescriptor.setId(Integer.parseInt(sElement.getAttribute("id")));
//					skillDescriptor.setSkillName(sElement.getElementsByTagName("skillName").item(0).getTextContent());
//					skillDescriptor.setToolTip(sElement.getElementsByTagName("toolTip").item(0).getTextContent());
//					skillDescriptor.setHpCost(Integer.parseInt(sElement.getElementsByTagName("hpCost").item(0).getTextContent()));
//					skillDescriptor.setMpCost(Integer.parseInt(sElement.getElementsByTagName("mpCost").item(0).getTextContent()));
//					skillDescriptor.setKiCost(Integer.parseInt(sElement.getElementsByTagName("kiCost").item(0).getTextContent()));
//					skillDescriptor.setAmmoCost(Integer.parseInt(sElement.getElementsByTagName("ammoCost").item(0).getTextContent()));
//					skillDescriptor.setHpMod(Integer.parseInt(sElement.getElementsByTagName("hpMod").item(0).getTextContent()));
//					skillDescriptor.setMpMod(Integer.parseInt(sElement.getElementsByTagName("mpMod").item(0).getTextContent()));
//					skillDescriptor.setKiMod(Integer.parseInt(sElement.getElementsByTagName("kiMod").item(0).getTextContent()));
//					skillDescriptor.setAmmoMod(Integer.parseInt(sElement.getElementsByTagName("ammoMod").item(0).getTextContent()));
//					skillDescriptor.setCastingDelay(Integer.parseInt(sElement.getElementsByTagName("castingDelay").item(0).getTextContent()));
//					skillDescriptor.setParentSkill(Integer.parseInt(sElement.getElementsByTagName("parentSkill").item(0).getTextContent()));
//					skillDescriptor.setAfterEffect(Integer.parseInt(sElement.getElementsByTagName("afterEffect").item(0).getTextContent()));
//					skillDescriptor.setDuration(Integer.parseInt(sElement.getElementsByTagName("duration").item(0).getTextContent()));
//					NodeList effect = sElement.getElementsByTagName("effect");
//					for (int j = 0; j < effect.getLength(); j++) {
//						effect.item(j).getTextContent();
//					}
//					skillDescriptor.setSkillType(SkillType.valueOf(sElement.getElementsByTagName("skillType").item(0).getTextContent()));
//					skillDescriptor.setNumberOfTargets(Integer.parseInt(sElement.getElementsByTagName("numberOfTargets").item(0).getTextContent()));
//					NodeList targetable = sElement.getElementsByTagName("targetable");
//					for (int j = 0; j < targetable.getLength(); j++) {
//						targetable.item(j).getTextContent();
//					}
//
//					Dictionary.skillDictionary.put(Action.valueOf(skillDescriptor.getSkillName()), skillDescriptor);
//				}
//			}
//		} catch (Exception e) {
//			e.printStackTrace();
//		}
//	}
	
	
}
