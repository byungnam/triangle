package Triangle.DataDB;

import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Statement;
import java.util.Random;

import org.apache.log4j.Logger;
import org.sqlite.SQLiteConfig;

import Triangle.TriangleConfigure.TriangleConf;

public class CopyOfDataDB {

	private Logger Log = Logger.getLogger(this.getClass());
	private Connection connection;
	private String dbFileName;

	private boolean isOpened = false;
	private static int nextTeamNumber;
	private static int nextUnitNumber;

	String createUnit = "INSERT INTO Unit (unitNumber, accountNumber, unitname, level, "
			+ "cls, str, dex, vital, intel, speed, point, exp)"
			+ " SELECT nextUnitNumber, ?, ?, 1, 0, ?, ?, ?, ?, ?, 0, 0 from NextNumber;";
	String createUnitTrigger = "CREATE TRIGGER IF NOT EXISTS createUnitTrigger Insert on Unit "
			+ "BEGIN "
			+ "Insert into Tactic(unitNumber, priority, condition, value, action, actionLevel)"
			+ "VALUES (new.unitNumber, 0, 0, 0, 0, 0); "
			+ "Insert into Tactic(unitNumber, priority, condition, value, action, actionLevel)"
			+ "VALUES (new.unitNumber, 1, 0, 0, 0, 0); "
			+ "Insert into Tactic(unitNumber, priority, condition, value, action, actionLevel)"
			+ "VALUES (new.unitNumber, 2, 0, 0, 0, 0); "
			+ "Update NextNumber set nextUnitNumber = nextUnitNumber + 1; "
			+ "END;";
	String deleteUnit = "DELETE from Unit where unitNumber=?;";
	String deleteUnitTrigger = "CREATE TRIGGER IF NOT EXISTS deleteUnitTrigger AFTER Delete on Unit "
			+ "BEGIN "
			+ "DELETE from Tactic where old.unitNumber = unitNumber; "
			+ "DELETE from Regiment where unitNumber = old.unitNumber; "
			+ "END;";
	String getUnitByAccountNumber = "SELECT * from Unit where accountNumber = ?";
	String getUnitByTeamNumber = "SELECT u.unitNumber as unitNumber, u.unitname as unitname,"
			+ " u.cls as cls, u.str as str, u.dex as dex, u.intel as intel, u.vital as vital,"
			+ " u.speed as speed, u.level as level, u.exp as exp, point"
			+ " from Unit as u, Regiment as r where u.unitNumber = r.unitNumber and r.teamNumber = ?;";
	String getUnitByUnitNumber = "SELECT * from Unit where unitNumber = ?;";
	String editUnit = "UPDATE Unit set unitname = ?, level = ?, cls = ?, "
			+ "str = ?, dex = ?, vital = ?, intel = ?, speed = ?, point = ?, exp = ? "
			+ "where unitNumber = ?;";

	String createTeam = "INSERT INTO Team (accountNumber, teamNumber, teamName) SELECT ?, nextTeamNumber, ? from NextNumber;";
	String createTeamTrigger = "CREATE TRIGGER IF NOT EXISTS createTeamTrigger Insert on Team "
			+ "BEGIN "
			+ "UPDATE NextNumber set nextTeamNumber = nextTeamNumber + 1;"
			+ "END;";
	String deleteTeam = "DELETE from Team where teamNumber = ?;";
	String deleteTeamTrigger = "CREATE TRIGGER IF NOT EXISTS deleteTeamTrigger Delete on Team "
			+ "BEGIN "
			+ "DELETE from Regiment where old.teamNumber = teamNumber; "
			+ "END;";
	String getTeamByAccountNumber = "SELECT * from Team where accountNumber = ?;";
	String getTeamByUnitNumber = "SELECT teamNumber from Regiment where unitNumber = ?;";
	String editTeam = "UPDATE Team set teamname = ? where teamNumber = ?;";

	String registerUnit = "INSERT INTO Regiment (unitNumber, teamNumber) VALUES (?, ?);";
	String unregisterUnit = "DELETE from Regiment where unitNumber = ? and teamNumber = ?;";

	String createTactic = "INSERT INTO Tactic (unitNumber, priority, condition, value, action, actionLevel) "
			+ "SELECT unitNumber, count(1), 0, 0, 0, 0 from Tactic where unitNumber = ?;";
//	String createTacticTrigger = "CREATE TRIGGER IF NOT EXISTS createTacticTrigger Insert on Tactic "
//			+ "BEGIN "
//			+ "Update Tactic set priority = priority + 1 where new.priority <= priority and unitNumber = new.unitNumber; "
//			+ "END;";
	String deleteTactic = "DELETE from Tactic where unitNumber = ? and priority = ?;";
	String deleteTacticTrigger = "CREATE TRIGGER IF NOT EXISTS deleteTacticTrigger AFTER Delete on Tactic "
			+ "BEGIN Update Tactic set priority = priority - 1 where old.priority < priority and old.unitNumber = unitNumber; END;";
	String getTactic = "SELECT priority, condition, value, action, actionLevel FROM Tactic where unitNumber=?;";
	String editTactic = "UPDATE Tactic set priority = ?, condition = ?, value = ?, action = ?, actionLevel = ? "
			+ "where unitNumber = ? and priority = ?;";
	String editTacticTrigger = "CREATE TRIGGER IF NOT EXISTS updateTacticTrigger Update of priority on Tactic "
			+ "BEGIN "
			+ "Update Tactic set priority = priority - 1 where old.priority < priority and new.priority >= priority and unitNumber = old.unitNumber; "
			+ "Update Tactic set priority = priority + 1 where old.priority > priority and new.priority <= priority and unitNumber = old.unitNumber; "
			+ "END;";

	String learnSkill = "INSERT INTO Skill (unitNumber, skillNumber, level, exp) VALUES (?, ?, 0, 0);";
	String getSkill = "SELECT skillNumber, level, exp FROM Skill where unitNumber = ?;";
	String updateSkill = "UPDATE Skill set exp = ? where unitNumber = ? and skillNumber = ?;";

	String gainItem = "INSERT or REPLACE INTO Item (accountNumber, itemNumber, quantity) "
			+ "VALUES (?, ?, ifnull((select quantity from Item where accountNumber=? and itemNumber=?)+?, ?));";
	String dropItem = "Update Item set quantity = quantity - ? where accountNumber = ? and itemNumber = ?;";
	String dropItemTrigger = "CREATE TRIGGER IF NOT EXISTS dropItemTrigger "
			+ "AFTER Update of quantity on Item when new.quantity = 0 "
			+ "BEGIN DELETE from Item where quantity = 0; END;";
	String getItem = "SELECT itemNumber, quantity FROM Item where accountNumber = ?;";
	
	PreparedStatement createUnit_prep;
	PreparedStatement createUnitTrigger_prep;
	PreparedStatement deleteUnit_prep;
	PreparedStatement deleteUnitTrigger_prep;
	PreparedStatement getUnitByAccountNumber_prep;
	PreparedStatement getUnitByTeamNumber_prep;
	PreparedStatement getUnitByUnitNumber_prep;
	PreparedStatement editUnit_prep;

	PreparedStatement createTeam_prep;
	PreparedStatement deleteTeam_prep;
	PreparedStatement deleteTeamTrigger_prep;
	PreparedStatement getTeamByAccountNumber_prep;
	PreparedStatement getTeamByUnitNumber_prep;
	PreparedStatement editTeam_prep;

	PreparedStatement registerUnit_prep;
	PreparedStatement unregisterUnit_prep;

	PreparedStatement createTactic_prep;
//	PreparedStatement createTacticTrigger_prep;
	PreparedStatement deleteTactic_prep;
	PreparedStatement deleteTacticTrigger_prep;
	PreparedStatement getTactic_prep;
	PreparedStatement editTactic_prep;
	PreparedStatement editTacticTrigger_prep;

	PreparedStatement learnSkill_prep;
	PreparedStatement getSkill_prep;
	PreparedStatement updateSkill_prep;

	PreparedStatement gainItem_prep;
	PreparedStatement dropItem_prep;
	PreparedStatement dropItemTrigger_prep;
	PreparedStatement getItem_prep;

	private static CopyOfDataDB singleton;

	public static CopyOfDataDB get(boolean isOperation) {
		if (singleton == null) {
			singleton = new CopyOfDataDB(isOperation);
		}
		return singleton;
	}

	static {
		try {
			Class.forName("org.sqlite.JDBC");
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	private CopyOfDataDB(boolean isOperation) {
		if (isOperation) {
			this.dbFileName = TriangleConf.DataTable;
		} else {
			this.dbFileName = TriangleConf.TestDataTable;
		}
	}

	public boolean open() {
		try {
			SQLiteConfig config = new SQLiteConfig();
			this.connection = DriverManager.getConnection("jdbc:sqlite:/D:/Triangle/"
					+ this.dbFileName, config.toProperties());

			ResultSet result;
			Statement stat = connection.createStatement();

			Log.info("DataDB STARTUP REPORTING..");

			result = stat.executeQuery("Select * from NextNumber");
			result.next();

			result = stat.executeQuery("Select * from NextNumber;");
			if (result.next()) {
				nextTeamNumber = result.getInt("nextTeamNumber");
				nextUnitNumber = result.getInt("nextUnitNumber");
				if (result.next()) {
					throw new SQLException("NextNumber has >1 rows");
				}
				
				result = stat.executeQuery("Select max(teamNumber) as maxTeamNumber from Team;");
				if(result.next()){
					int currentMaxTeamNumber = result.getInt("maxTeamNumber") +1;
					if(currentMaxTeamNumber != nextTeamNumber){
						Log.warn("NextTeamNumber is not matched. currentMaxTeamNumber = "
								+currentMaxTeamNumber +", nextTeamNumber = "+ nextTeamNumber);
					}
				}
				
				result = stat.executeQuery("Select max(unitNumber) as maxUnitNumber from Unit;");
				if(result.next()){
					int currentMaxUnitNumber = result.getInt("maxUnitNumber") +1;
					if(currentMaxUnitNumber != nextUnitNumber){
						Log.warn("NextUnitNumber is not matched. currentMaxUnitNumber = "
								+currentMaxUnitNumber +", nextUnitNumber = "+ nextUnitNumber);
					}
				}
				
				Log.info("NextTeamNumber = " + nextTeamNumber);
				Log.info("NextUnitNumber = " + nextUnitNumber);
			} else {
				throw new SQLException("NextNumber has 0 row");
			}

			result = stat.executeQuery("Select count(*) as cTeam from Team");
			result.next();
			Log.info("TEAM COUNT : " + result.getInt("cTeam"));

			result = stat.executeQuery("Select count(*) as cUnit from Unit");
			result.next();
			Log.info("UNIT COUNT : " + result.getInt("cUnit"));

			result = stat.executeQuery("Select * from Unit");
			while (result.next()) {
				System.out.println(result.getString("unitName") + " "
						+ result.getInt("unitNumber"));
			}

			createUnit_prep = connection.prepareStatement(createUnit);
			createUnitTrigger_prep = connection.prepareStatement(createUnitTrigger);
			deleteUnit_prep = connection.prepareStatement(deleteUnit);
			deleteUnitTrigger_prep = connection.prepareStatement(deleteUnitTrigger);
			getUnitByAccountNumber_prep = connection.prepareStatement(getUnitByAccountNumber);
			getUnitByTeamNumber_prep = connection.prepareStatement(getUnitByTeamNumber);
			getUnitByUnitNumber_prep = connection.prepareStatement(getUnitByUnitNumber);
			editUnit_prep = connection.prepareStatement(editUnit);

			createTeam_prep = connection.prepareStatement(createTeam);
			deleteTeam_prep = connection.prepareStatement(deleteTeam);
			deleteTeamTrigger_prep = connection.prepareStatement(deleteTeamTrigger);
			getTeamByAccountNumber_prep = connection.prepareStatement(getTeamByAccountNumber);
			getTeamByUnitNumber_prep = connection.prepareStatement(getTeamByUnitNumber);
			editTeam_prep = connection.prepareStatement(editTeam);

			registerUnit_prep = connection.prepareStatement(registerUnit);
			unregisterUnit_prep = connection.prepareStatement(unregisterUnit);

			createTactic_prep = connection.prepareStatement(createTactic);
//			createTacticTrigger_prep = connection.prepareStatement(createTacticTrigger);
			deleteTactic_prep = connection.prepareStatement(deleteTactic);
			deleteTacticTrigger_prep = connection.prepareStatement(deleteTacticTrigger);
			getTactic_prep = connection.prepareStatement(getTactic);
			editTactic_prep = connection.prepareStatement(editTactic);
			editTacticTrigger_prep = connection.prepareStatement(editTacticTrigger);

			learnSkill_prep = connection.prepareStatement(learnSkill);
			getSkill_prep = connection.prepareStatement(getSkill);
			updateSkill_prep = connection.prepareStatement(updateSkill);

			gainItem_prep = connection.prepareStatement(gainItem);
			dropItem_prep = connection.prepareStatement(dropItem);
			dropItemTrigger_prep = connection.prepareStatement(dropItemTrigger);
			getItem_prep = connection.prepareStatement(getItem);

			// Trigger Making Execution
			createUnitTrigger_prep.execute();
			deleteUnitTrigger_prep.execute();
//			createTacticTrigger_prep.execute();
			deleteTacticTrigger_prep.execute();
			editTacticTrigger_prep.execute();
			dropItemTrigger_prep.execute();
			deleteTeamTrigger_prep.execute();

		} catch (SQLException e) {
			e.printStackTrace();
			return false;
		}

		isOpened = true;
		return true;
	}

	public boolean close() {
		if (this.isOpened == false) {
			return true;
		}

		try {
			this.connection.close();
		} catch (SQLException e) {
			e.printStackTrace();
			return false;
		}
		return true;
	}

	public boolean CreateUnit(int accountNumber, String unitname, int cls) throws SQLException {
		// System.out.println("New Unit Num : " + thisUnitNumber);
		Random r = new Random();
		
		createUnit_prep.setInt(1, accountNumber);
		createUnit_prep.setString(2, unitname);
//		createUnit_prep.setInt(3, cls);
		createUnit_prep.setInt(3, 10 + r.nextInt(7));
		createUnit_prep.setInt(4, 10 + r.nextInt(7));
		createUnit_prep.setInt(5, 20 + r.nextInt(7));
		createUnit_prep.setInt(6, 20 + r.nextInt(7));
		createUnit_prep.setInt(7, 10 + r.nextInt(7));
		
		createUnit_prep.execute();
		
		return true;
	}

	public boolean DeleteUnit(int unitNumber) throws SQLException {
		deleteUnit_prep.setInt(1, unitNumber);
		deleteUnit_prep.execute();
		return true;
	}

	public ResultSet GetUnitByAccountNumber(int accountNumber) throws SQLException {
		getUnitByAccountNumber_prep.setInt(1, accountNumber);
		return getUnitByAccountNumber_prep.executeQuery();
	}

	public ResultSet GetUnitByTeamNumber(int teamNumber) throws SQLException {
		getUnitByTeamNumber_prep.setInt(1, teamNumber);
		return getUnitByTeamNumber_prep.executeQuery();
	}

	public ResultSet GetUnitByUnitNumber(int unitNumber) throws SQLException {
		getUnitByUnitNumber_prep.setInt(1, unitNumber);
		return getUnitByUnitNumber_prep.executeQuery();
	}

	public boolean EditUnit(String unitname, int level, int cls, int str, int dex, int vital, int intel, int speed, int point, int exp, int unitNumber) throws SQLException {
		editUnit_prep.setString(1, unitname);
		editUnit_prep.setInt(2, level);
		editUnit_prep.setInt(3, cls);
		editUnit_prep.setInt(4, str);
		editUnit_prep.setInt(5, dex);
		editUnit_prep.setInt(6, vital);
		editUnit_prep.setInt(7, intel);
		editUnit_prep.setInt(8, speed);
		editUnit_prep.setInt(9, point);
		editUnit_prep.setInt(10, exp);
		editUnit_prep.setInt(11, unitNumber);
		editUnit_prep.execute();

		return true;
	}

	public boolean CreateTeam(int accountNumber, String teamname) throws SQLException {

		// Log.info("New Team Create. Number: " + thisTeamNumber);

		createTeam_prep.setInt(1, accountNumber);
		createTeam_prep.setString(2, teamname);
		createTeam_prep.execute();
		return true;
	}

	public boolean DeleteTeam(int teamNumber) throws SQLException {
		deleteTeam_prep.setInt(1, teamNumber);
		deleteTeam_prep.execute();
		return true;
	}

	public ResultSet GetTeamByAccountNumber(int accountNumber) throws SQLException {
		getTeamByAccountNumber_prep.setInt(1, accountNumber);
		return getTeamByAccountNumber_prep.executeQuery();
	}
	
	public ResultSet GetTeamByUnitNumber(int unitNumber) throws SQLException {
		getTeamByUnitNumber_prep.setInt(1, unitNumber);
		return getTeamByUnitNumber_prep.executeQuery();
	}

	public boolean EditTeam(String teamname, int teamNumber) throws SQLException {
		editTeam_prep.setString(1, teamname);
		editTeam_prep.setInt(2, teamNumber);
		editTeam_prep.execute();

		return true;
	}

	public boolean RegisterUnit(int unitNumber, int teamNumber) throws SQLException {
		registerUnit_prep.setInt(1, unitNumber);
		registerUnit_prep.setInt(2, teamNumber);
		registerUnit_prep.execute();

		// System.out.println(unitNumber + " " + teamNumber);
		// ResultSet rs2 = GetUnitByTeamNumber(teamNumber);
		// while(rs2.next()){
		// System.out.println("@ unitNum: "+ rs2.getInt("unitNumber") +
		// ", teamNumber: "+ teamNumber);
		// }

		return true;
	}

	public boolean UnregisterUnit(int unitNumber, int teamNumber) throws SQLException {
		unregisterUnit_prep.setInt(1, unitNumber);
		unregisterUnit_prep.setInt(2, teamNumber);
		unregisterUnit_prep.execute();

		// System.out.println(unitNumber + " " + teamNumber);
		// ResultSet rs2 = GetUnitByTeamNumber(teamNumber);
		// while(rs2.next()){
		// System.out.println("@ unitNum: "+ rs2.getInt("unitNumber") +
		// ", teamNumber: "+ teamNumber);
		// }

		return true;
	}

	public boolean CreateTactic(int unitNumber) throws SQLException {
		createTactic_prep.setInt(1, unitNumber);
//		createTactic_prep.setInt(2, priority);
//		createTactic_prep.setInt(3, condition);
//		createTactic_prep.setInt(4, value);
//		createTactic_prep.setInt(5, action);
//		createTactic_prep.setInt(6, actionLevel);
		createTactic_prep.execute();
//		ResultSet result = connection.createStatement().executeQuery("Select * from Tactic");
//		while (result.next()) {
//			System.out.println(result.getInt("unitNumber") + " "
//					+ result.getInt("priority") + " " + result.getString("condition") + " "
//					+ result.getInt("value") + " " + result.getInt("action")+ " "
//							+ result.getInt("actionLevel"));
//		}
//		System.out.println("-");
		return true;
	}

	public boolean DeleteTactic(int unitNumber, int priority) throws SQLException {
		deleteTactic_prep.setInt(1, unitNumber);
		deleteTactic_prep.setInt(2, priority);
		deleteTactic_prep.execute();

		return true;
	}

	public ResultSet GetTactic(int unitNumber) throws SQLException {
		getTactic_prep.setInt(1, unitNumber);
		return getTactic_prep.executeQuery();
	}

	public boolean EditTactic(int priority_new, int condition, int value, int action, int actionLevel, int unitNumber, int priority_old) throws SQLException {
		editTactic_prep.setInt(1, priority_new);
		editTactic_prep.setInt(2, condition);
		editTactic_prep.setInt(3, value);
		editTactic_prep.setInt(4, action);
		editTactic_prep.setInt(5, actionLevel);
		editTactic_prep.setInt(6, unitNumber);
		editTactic_prep.setInt(7, priority_old);
		editTactic_prep.execute();
		return true;
	}

	public ResultSet GetSkill(int unitNumber) throws SQLException {
		getTactic_prep.setInt(1, unitNumber);
		return getSkill_prep.executeQuery();
	}

	public ResultSet GetItem(int unitNumber) throws SQLException {
		getTactic_prep.setInt(1, unitNumber);
		return getItem_prep.executeQuery();
	}

//	public boolean SaveNextNumbers() throws SQLException {
//		PreparedStatement p = connection.prepareStatement("Update NextNumber set nextTeamNumber = ?, nextUnitNumber = ?;");
//		p.setInt(1, nextTeamNumber);
//		p.setInt(2, nextUnitNumber);
//		p.execute();
//
//		return true;
//	}

}
