package Triangle.DBProtoUtil;

import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.LinkedList;
import java.util.List;

import Triangle.DataDB.DataDB;
import Triangle.Protocol.GameProtocol.ProtocolItem;
import Triangle.Protocol.GameProtocol.ProtocolSkill;
import Triangle.Protocol.GameProtocol.ProtocolTactic;
import Triangle.Protocol.GameProtocol.ProtocolTeam;
import Triangle.Protocol.GameProtocol.ProtocolUnit;

public final class DBProtoUtil {
	final static DataDB db;
	static {
		db = DataDB.getUser();
	}

	public static List<ProtocolUnit> unitResultSetToProto(ResultSet rsUnit) throws SQLException {
		ResultSet rsTactic = null;
		ResultSet rsSkills = null;
		ResultSet rsItems = null;
		List<ProtocolUnit> list = new LinkedList<ProtocolUnit>();

		while (rsUnit.next()) {
//			System.out.println(rsUnit.getString("unitName"));
			int unitNumber = rsUnit.getInt("unitNumber");
			ProtocolUnit.Builder unitBuilder = ProtocolUnit.newBuilder().setUnitName(
					rsUnit.getString("unitname")).setCls(rsUnit.getInt("cls")).setStr(
					rsUnit.getInt("str")).setDex(rsUnit.getInt("dex")).setIntel(
					rsUnit.getInt("intel")).setVital(rsUnit.getInt("vital")).setSpeed(
					rsUnit.getInt("speed")).setLevel(rsUnit.getInt("level")).setUnitNumber(
					unitNumber).setExp(rsUnit.getInt("exp")).setPoint(
					rsUnit.getInt("point"));

			rsSkills = db.GetSkill(unitNumber);
			while (rsSkills.next()) {
				unitBuilder.addSkills(ProtocolSkill.newBuilder().setSkillNumber(
						rsSkills.getInt("skillNumber")).setSkillLevel(
						rsSkills.getInt("level")));
			}
			rsSkills.close();

			rsItems = db.GetItem(unitNumber);
			while (rsItems.next()) {
				unitBuilder.addItems(ProtocolItem.newBuilder().setItemNumber(
						rsItems.getInt("itemNumber")).setQuantity(
						rsItems.getInt("quantity")));
			}
			rsItems.close();

			rsTactic = db.GetTactic(unitNumber);
			while (rsTactic.next()) {
				ProtocolTactic tactic = ProtocolTactic.newBuilder().setPriority(
						rsTactic.getInt("priority")).setCondition(
						rsTactic.getInt("condition")).setValue(rsTactic.getInt("value")).setAction(
						rsTactic.getInt("action")).setActionLevel(
						rsTactic.getInt("actionLevel")).build();
				unitBuilder.addTactics(tactic);
			}
			rsTactic.close();
			list.add(unitBuilder.build());
		}
		rsUnit.close();
//		System.out.println(list);
		if (list.size() == 0) {
			return null;
		}
		return list;
	}

	public static List<ProtocolTeam> teamResultSetToProto(ResultSet rsTeam) throws SQLException {
		List<ProtocolTeam> list = new LinkedList<ProtocolTeam>();

		while (rsTeam.next()) {
			list.add(ProtocolTeam.newBuilder().setTeamName(
					rsTeam.getString("teamName")).setTeamNumber(
					rsTeam.getInt("teamNumber")).build());
		}
		rsTeam.close();
//		System.out.println(list);
		if (list.size() == 0) {
			return null;
		}
		return list;
	}

	public static List<ProtocolTactic> tacticResultSetToProto(ResultSet rsTactic) throws SQLException {
		List<ProtocolTactic> list = new LinkedList<ProtocolTactic>();
		while (rsTactic.next()) {
			list.add(ProtocolTactic.newBuilder().setPriority(
					rsTactic.getInt("priority")).setCondition(
					rsTactic.getInt("condition")).setValue(rsTactic.getInt("value")).setAction(
					rsTactic.getInt("action")).setActionLevel(
					rsTactic.getInt("actionLevel")).build());
		}
		rsTactic.close();
		if (list.size() == 0) {
			return null;
		}
		return list;
	}
}
