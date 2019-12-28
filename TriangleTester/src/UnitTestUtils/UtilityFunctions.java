package UnitTestUtils;

import java.sql.SQLException;
import java.util.List;

import Triangle.DBProtoUtil.DBProtoUtil;
import Triangle.DataDB.DataDB;
import Triangle.Protocol.GameProtocol.ProtocolTactic;
import Triangle.Protocol.GameProtocol.ProtocolTeam;
import Triangle.Protocol.GameProtocol.ProtocolUnit;

public class UtilityFunctions {
	DataDB dataDB;

	public UtilityFunctions(DataDB dataDB) {
		this.dataDB = dataDB;
	}

	public ProtocolUnit GetUnitByUnitNumber(int unitNum) {
		try {
			if (DBProtoUtil.unitResultSetToProto(dataDB.GetUnitByUnitNumber(unitNum)) == null) {
				return null;
			}
			return DBProtoUtil.unitResultSetToProto(dataDB.GetUnitByUnitNumber(unitNum)).get(
					0);
		} catch (SQLException e) {
			e.printStackTrace();
		}
		return null;
	}

	public List<ProtocolUnit> GetUnitByTeamNumber(int teamNum) {
		try {
			return DBProtoUtil.unitResultSetToProto(dataDB.GetUnitByTeamNumber(teamNum));
		} catch (SQLException e) {
			e.printStackTrace();
		}
		return null;
	}

	public List<ProtocolTeam> GetTeam(int accountNum) {
		try {
			return DBProtoUtil.teamResultSetToProto(dataDB.GetTeamByAccountNumber(accountNum));
		} catch (Exception e) {
			e.printStackTrace();
		}
		return null;
	}
	
	public List<ProtocolTactic> GetTactic(int unitNum) {
		try{
			return DBProtoUtil.tacticResultSetToProto(dataDB.GetTactic(unitNum));
		} catch(Exception e){
			e.printStackTrace();
		}
		return null;
	}

}
