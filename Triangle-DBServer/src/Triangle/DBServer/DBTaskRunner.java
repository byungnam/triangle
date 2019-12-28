package Triangle.DBServer;

import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.List;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.LinkedBlockingQueue;

import org.apache.log4j.Logger;

import Triangle.DBProtoUtil.DBProtoUtil;
import Triangle.DataDB.DataDB;
import Triangle.Protocol.GameProtocol.CombatRequest;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GetTeamByAccountNumber;
import Triangle.Protocol.GameProtocol.GetTeamByUnitNumber;
import Triangle.Protocol.GameProtocol.GetUnitByAccountNumber;
import Triangle.Protocol.GameProtocol.ProtocolTactic;
import Triangle.Protocol.GameProtocol.ProtocolTeam;
import Triangle.Protocol.GameProtocol.ProtocolUnit;
import Triangle.Task.DBTask;

public class DBTaskRunner implements Runnable {
	private DataDB db;
	private DataDB npcdb;
	public static BlockingQueue<DBTask> taskQ;

	private Logger Log = Logger.getLogger(this.getClass());

	public DBTaskRunner() {
		db = DataDB.getUser();
		db.open(DBServer.isOperation);
		npcdb = DataDB.getNPC();
		npcdb.open(DBServer.isOperation);
//		npcdb = NPCDataDB.get(DBServer.isOperation);
//		npcdb.open();
		taskQ = new LinkedBlockingQueue<>();
	}

	public void run() {
		while (DBServer.serverLive) {
			DBTask task = null;
			boolean result = false;
			ResultSet resultSet = null;
			List<ProtocolUnit> protoUnitList = null;
			List<ProtocolTeam> protoTeamList = null;
			List<ProtocolTactic> protoTacticList = null;
			GameMessage msg = null;
			try {
				task = taskQ.take();
				msg = task.message;
				GameMessage.Builder builder = msg.toBuilder();
				Log.info("Running task : " + msg.getGameMessageType());
				switch (msg.getGameMessageType()) {
				case GetNPCTeam:
					resultSet = npcdb.GetTeamByAccountNumber(msg.getGetNPCTeam().getNpcNumber());
					protoTeamList = DBProtoUtil.teamResultSetToProto(resultSet);
					if (protoTeamList != null) {
						builder.setGetNPCTeam(msg.getGetNPCTeam().toBuilder().addAllTeam(
								protoTeamList));
						result = true;
					}
					break;
				case CombatRequest:
					CombatRequest.Builder combatBuilder = msg.getCombatRequest().toBuilder();
					resultSet = db.GetUnitByTeamNumber(msg.getCombatRequest().getAllyTeamNumber());
					protoUnitList = DBProtoUtil.unitResultSetToProto(resultSet);
					if (protoUnitList != null) {
						combatBuilder.addAllAllyUnit(protoUnitList);
					} else {
						break;
					}

					resultSet = db.GetUnitByTeamNumber(msg.getCombatRequest().getEnemyTeamNumber());
					protoUnitList = DBProtoUtil.unitResultSetToProto(resultSet);
					if (protoUnitList != null) {
						combatBuilder.addAllEnemyUnit(protoUnitList);
					} else {
						break;
					}

					builder.setCombatRequest(combatBuilder);
					result = true;
					break;

				case CombatResult:
					break;

				case CreateUnit:
					result = db.CreateUnit(msg.getAccountNumber(),
							msg.getCreateUnit().getName(), msg.getCreateUnit().getCls()) >= 0 ;
					break;

				case DeleteUnit:
					result = db.DeleteUnit(msg.getDeleteUnit().getUnitNumber());
					break;

				case GetUnitByAccountNumber:
					resultSet = db.GetUnitByAccountNumber(msg.getAccountNumber());
					protoUnitList = DBProtoUtil.unitResultSetToProto(resultSet);
					if (protoUnitList != null) {
						builder.setGetUnitByAccountNumber(GetUnitByAccountNumber.newBuilder().addAllUnit(
								protoUnitList));
						result = true;
					}

					break;

				case GetUnitByTeamNumber:
					resultSet = db.GetUnitByTeamNumber(msg.getGetUnitByTeamNumber().getTeamNumber());
					protoUnitList = DBProtoUtil.unitResultSetToProto(resultSet);
					if (protoUnitList != null) {
						builder.setGetUnitByTeamNumber(msg.getGetUnitByTeamNumber().toBuilder().addAllUnit(
								protoUnitList));
						result = true;
					}
					break;

				case GetUnitByUnitNumber:
					resultSet = db.GetUnitByUnitNumber(msg.getGetUnitByUnitNumber().getUnitNumber());
					protoUnitList = DBProtoUtil.unitResultSetToProto(resultSet);
					if (protoUnitList != null) {
						builder.setGetUnitByUnitNumber(msg.getGetUnitByUnitNumber().toBuilder().setUnit(
								protoUnitList.get(0)));
						result = true;
					}
					break;

				case EditUnit:
					ProtocolUnit pu = msg.getEditUnit().getUnit();
					result = db.EditUnit(pu.getUnitName(), pu.getLevel(), pu.getCls(),
							pu.getStr(), pu.getDex(), pu.getVital(), pu.getIntel(),
							pu.getSpeed(), pu.getPoint(), pu.getExp(),
							msg.getEditUnit().getUnitNumber());
					break;

				case CreateTeam:
					result = db.CreateTeam(msg.getAccountNumber(),
							msg.getCreateTeam().getName());
					break;

				case DeleteTeam:
					result = db.DeleteTeam(msg.getDeleteTeam().getTeamNumber());
					break;

				case GetTeamByAccountNumber:
					resultSet = db.GetTeamByAccountNumber(msg.getAccountNumber());
					protoTeamList = DBProtoUtil.teamResultSetToProto(resultSet);
					if (protoTeamList != null) {
						builder.setGetTeamByAccountNumber(GetTeamByAccountNumber.newBuilder().addAllTeam(protoTeamList));
						result = true;
					}
					break;
					
				case GetTeamByUnitNumber:
					resultSet = db.GetTeamByUnitNumber(msg.getGetTeamByUnitNumber().getUnitNumber());
					protoTeamList = DBProtoUtil.teamResultSetToProto(resultSet);
					if (protoTeamList != null) {
						builder.setGetTeamByUnitNumber(GetTeamByUnitNumber.newBuilder().addAllTeam(protoTeamList));
						result = true;
					}
					break;

				case EditTeam:
					result = db.EditTeam(msg.getEditTeam().getTeam().getTeamName(),
							msg.getEditTeam().getTeamNumber());
					break;

				case CreateTactic:
					result = db.CreateTactic(msg.getCreateTactic().getUnitNumber()
//							msg.getCreateTactic().getTactic().getPriority(),
//							msg.getCreateTactic().getTactic().getCondition(),
//							msg.getCreateTactic().getTactic().getValue(),
//							msg.getCreateTactic().getTactic().getAction(),
//							msg.getCreateTactic().getTactic().getActionparam()
							);
					break;
				case DeleteTactic:
					result = db.DeleteTactic(msg.getDeleteTactic().getUnitNumber(),
							msg.getDeleteTactic().getPriority());
					break;
				case GetTactic:
					resultSet = db.GetTactic(msg.getGetTactic().getUnitNumber());
					protoTacticList = DBProtoUtil.tacticResultSetToProto(resultSet);
					if (protoTacticList != null) {
						builder.setGetTactic(msg.getGetTactic().toBuilder().addAllTactics(
								protoTacticList));
						result = true;
					}
					break;
				case EditTactic:
					result = db.EditTactic(msg.getEditTactic().getTactic().getPriority(),
							msg.getEditTactic().getTactic().getCondition(),
							msg.getEditTactic().getTactic().getValue(),
							msg.getEditTactic().getTactic().getAction(),
							msg.getEditTactic().getTactic().getActionLevel(),
							msg.getEditTactic().getUnitNumber(),
							msg.getEditTactic().getTactic().getPriority());
					break;

				case RegisterUnit:
					result = db.RegisterUnit(msg.getRegisterUnit().getUnitNumber(),
							msg.getRegisterUnit().getTeamNumber());
					Log.info("register : " + result);
					break;
				case UnregisterUnit:
					result = db.UnregisterUnit(msg.getUnregisterUnit().getUnitNumber(),
							msg.getUnregisterUnit().getTeamNumber());
					Log.info("unregister : " + result);
					break;

				default:
					throw new Exception("Wrong Message Received in DBTaskRunner: "
							+ msg.getGameMessageType());

				}
				task.ctx.writeAndFlush(builder.setResult(result));
			} catch (InterruptedException e) {
				Log.info("Shutdown Interrupt Detected");
			} catch (SQLException e) {
				e.printStackTrace();
			} catch (Exception e) {
				e.printStackTrace();
			}
		}
		db.close();
	}
	// public Message gainExp(List<Integer> unitNumberList, int amount)
	// throws Exception {
	// StringBuilder SQL = new StringBuilder();
	//
	// SQL.append("select * from Unit where unitNumber in (?");
	//
	// for (int i = 1; i < unitNumberList.size(); i++) {
	// SQL.append(",?");
	// }
	// SQL.append(")");
	//
	// Log.info("SQL : " + SQL.toString());
	//
	// ResultSet rs = db.executeSql(SQL.toString(), unitNumberList);
	//
	// while (rs.next()) {
	// Log.info("GainExp Unit : " + rs.getString("unitname"));
	// int exp = rs.getInt("exp");
	// int level = rs.getInt("level");
	// int point = rs.getInt("point");
	//
	// exp = exp + amount;
	//
	// if (exp > TriangleValues.expTable[level - 1]) {
	// level += 1;
	// exp -= TriangleValues.expTable[level - 1];
	// point += 5;
	// }
	//
	// db.executeNoResultSql("UPDATE Unit SET exp = ?, level = ?, point = ?",
	// exp, level, point);
	// }
	// return AccountPackets.newBuilder()
	// .setDatabasePacketType(DatabasePacketType.GainExp).setResult(true).build();
	// }
}
