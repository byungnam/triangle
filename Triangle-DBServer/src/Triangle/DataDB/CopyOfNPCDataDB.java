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

public class CopyOfNPCDataDB {

	private Logger Log = Logger.getLogger(this.getClass());
	private Connection connectionData;
	private Connection connectionAccount;
	private String DataFileName;
	private String AccountFileName;

	private boolean isOpened = false;
	private static int nextAccountNumber;
	private static int nextTeamNumber;
	private static int nextUnitNumber;

	String createAccount = "INSERT INTO NPCAccount (accountNumber) SELECT nextAccountNumber from NPCnextNumber;";
	String createAccountTrigger = "CREATE TRIGGER IF NOT EXISTS createNPCAcccountTrigger Insert on NPCAccount "
			+ "BEGIN "
			+ "UPDATE NPCnextNumber set nextAccountNumber = nextAccountNumber +1;"
			+ "END;";
	String getAccount = "SELECT * FROM NPCAccount;";
	String deleteAccount = "Delete from NPCAccount where accountNumber = ?;";
	
	String createUnit = "INSERT INTO NPCUnit (unitNumber, accountNumber, unitname, level, "
			+ "cls, str, dex, vital, intel, speed, point, exp)"
			+ " SELECT nextUnitNumber, ?, ?, 1, ?, ?, ?, ?, ?, ?, 0, 0 from NPCnextNumber;";
	String createUnitTrigger = "CREATE TRIGGER IF NOT EXISTS createNPCUnitTrigger Insert on NPCUnit "
			+ "BEGIN "
//			+ "Insert into NPCTactic(unitNumber, priority, condition, value, action, actionLevel)"
//			+ "VALUES (new.unitNumber, 0, 0, 0, 0, 0); "
//			+ "Insert into NPCTactic(unitNumber, priority, condition, value, action, actionLevel)"
//			+ "VALUES (new.unitNumber, 1, 0, 0, 0, 0); "
//			+ "Insert into NPCTactic(unitNumber, priority, condition, value, action, actionLevel)"
//			+ "VALUES (new.unitNumber, 2, 0, 0, 0, 0); "
			+ "Update NPCnextNumber set nextUnitNumber = nextUnitNumber + 1; "
			+ "END;";
	String deleteUnit = "DELETE from NPCUnit where unitNumber=?;";
	String deleteUnitTrigger = "CREATE TRIGGER IF NOT EXISTS deleteNPCUnitTrigger AFTER Delete on NPCUnit "
			+ "BEGIN "
			+ "DELETE from NPCTactic where unitNumber = old.unitNumber; "
			+ "DELETE from NPCRegiment where unitNumber = old.unitNumber; "
			+ "END;";
	String getUnitByAccountNumber = "SELECT * from NPCUnit where accountNumber = ?";
	String getUnitByTeamNumber = "SELECT u.unitNumber as unitNumber, u.unitname as unitname,"
			+ " u.cls as cls, u.str as str, u.dex as dex, u.intel as intel, u.vital as vital,"
			+ " u.speed as speed, u.level as level, u.exp as exp, point"
			+ " from NPCUnit as u, NPCRegiment as r where u.unitNumber = r.unitNumber and r.teamNumber = ?;";
	String getUnitByUnitNumber = "SELECT * from NPCUnit where unitNumber = ?;";
	String editUnit = "UPDATE NPCUnit set unitname = ?, level = ?, cls = ?, "
			+ "str = ?, dex = ?, vital = ?, intel = ?, speed = ?, point = ?, exp = ? "
			+ "where unitNumber = ?;";
	
	String createTeam = "INSERT INTO NPCTeam (accountNumber, teamNumber, teamName) SELECT ?, nextTeamNumber, ? from NPCnextNumber;";
	String createTeamTrigger = "CREATE TRIGGER IF NOT EXISTS createNPCTeamTrigger Insert on NPCTeam "
			+ "BEGIN "
			+ "UPDATE NPCnextNumber set nextTeamNumber = nextTeamNumber + 1;"
			+ "END;";
	String deleteTeam = "DELETE from NPCTeam where teamNumber = ?;";
	String deleteTeamTrigger = "CREATE TRIGGER IF NOT EXISTS deleteNPCTeamTrigger Delete on NPCTeam "
			+ "BEGIN "
			+ "DELETE from NPCRegiment where teamNumber = old.teamNumber; "
			+ "END;";
	String getTeamByAccountNumber = "SELECT * from NPCTeam where accountNumber = ?;";
	String getTeamByUnitNumber = "SELECT teamNumber from NPCRegiment where unitNumber = ?;";
	String editTeam = "UPDATE NPCTeam set teamname = ? where teamNumber = ?;";

	String registerUnit = "INSERT INTO NPCRegiment (unitNumber, teamNumber) VALUES (?, ?);";
	String unregisterUnit = "DELETE from NPCRegiment where unitNumber = ? and teamNumber = ?;";

	String createTactic = "INSERT INTO NPCTactic (unitNumber, priority, condition, value, action, actionLevel)"
			+ "SELECT unitNumber, count(*), 0, 0, 0, 0 from NPCTactic where unitNumber = ?;";
	String createTacticTrigger = "CREATE TRIGGER IF NOT EXISTS createNPCTacticTrigger Insert on NPCTactic "
			+ "BEGIN "
			+ "Update NPCTactic set priority = priority + 1 where new.priority <= priority and unitNumber = new.unitNumber; "
			+ "END;";
	String deleteTactic = "DELETE from NPCTactic where unitNumber = ? and priority = ?;";
	String deleteTacticTrigger = "CREATE TRIGGER IF NOT EXISTS deleteNPCTacticTrigger AFTER Delete on NPCTactic "
			+ "BEGIN Update NPCTactic set priority = priority - 1 where old.priority < priority and old.unitNumber = unitNumber; END;";
	String getTactic = "SELECT priority, condition, value, action, actionLevel FROM NPCTactic where unitNumber=?;";
	String editTactic = "UPDATE NPCTactic set priority = ?, condition = ?, value = ?, action = ?, actionLevel = ? "
			+ "where unitNumber = ? and priority = ?;";
	String editTacticTrigger = "CREATE TRIGGER IF NOT EXISTS updateNPCTacticTrigger Update of priority on NPCTactic "
			+ "BEGIN "
			+ "Update NPCTactic set priority = priority - 1 where old.priority < priority and new.priority >= priority and unitNumber = old.unitNumber; "
			+ "Update NPCTactic set priority = priority + 1 where old.priority > priority and new.priority <= priority and unitNumber = old.unitNumber; "
			+ "END;";

	String learnSkill = "INSERT INTO NPCSkill (unitNumber, skillNumber, level, exp) VALUES (?, ?, 0, 0);";
	String getSkill = "SELECT skillNumber, level, exp FROM NPCSkill where unitNumber = ?;";
	String updateSkill = "UPDATE NPCSkill set exp = ? where unitNumber = ? and skillNumber = ?;";

	String gainItem = "INSERT or REPLACE INTO NPCItem (accountNumber, itemNumber, quantity) "
			+ "VALUES (?, ?, ifnull((select quantity from NPCItem where accountNumber=? and itemNumber=?)+?, ?));";
	String dropItem = "Update NPCItem set quantity = quantity - ? where accountNumber = ? and itemNumber = ?;";
	String dropItemTrigger = "CREATE TRIGGER IF NOT EXISTS dropNPCItemTrigger "
			+ "AFTER Update of quantity on NPCItem when new.quantity = 0 "
			+ "BEGIN DELETE from NPCItem where quantity = 0; END;";
	String getItem = "SELECT itemNumber, quantity FROM NPCItem where accountNumber = ?;";
	
	PreparedStatement createAccount_prep;
	PreparedStatement createAccountTrigger_prep;
	PreparedStatement getAccount_prep;
	PreparedStatement deleteAccount_prep;
	
	PreparedStatement createUnit_prep;
	PreparedStatement createUnitTrigger_prep;
	PreparedStatement deleteUnit_prep;
	PreparedStatement deleteUnitTrigger_prep;
	PreparedStatement getUnitByAccountNumber_prep;
	PreparedStatement getUnitByTeamNumber_prep;
	PreparedStatement getUnitByUnitNumber_prep;
	PreparedStatement editUnit_prep;

	PreparedStatement createTeam_prep;
	PreparedStatement createTeamTrigger_prep;
	PreparedStatement deleteTeam_prep;
	PreparedStatement deleteTeamTrigger_prep;
	PreparedStatement getTeamByAccountNumber_prep;
	PreparedStatement getTeamByUnitNumber_prep;
	PreparedStatement editTeam_prep;

	PreparedStatement registerUnit_prep;
	PreparedStatement unregisterUnit_prep;

	PreparedStatement createTactic_prep;
	PreparedStatement createTacticTrigger_prep;
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

	private static CopyOfNPCDataDB singleton;

	public static CopyOfNPCDataDB get(boolean isOperation) {
		if (singleton == null) {
			singleton = new CopyOfNPCDataDB(isOperation);
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

	private CopyOfNPCDataDB(boolean isOperation) {
		if (isOperation) {
			this.DataFileName = TriangleConf.DataTable;
			this.AccountFileName = TriangleConf.AccountTable;
		} else {
			this.DataFileName = TriangleConf.TestDataTable;
			this.AccountFileName = TriangleConf.TestAccountTable;
		}
	}

	public boolean open() {
		try {
			SQLiteConfig config = new SQLiteConfig();
			this.connectionData = DriverManager.getConnection("jdbc:sqlite:/D:/Triangle/"
					+ this.DataFileName, config.toProperties());
			this.connectionAccount = DriverManager.getConnection("jdbc:sqlite:/D:/Triangle/"
					+ this.AccountFileName, config.toProperties());
			
			ResultSet result;
			Statement statData = connectionData.createStatement();
			Statement statAccount = connectionAccount.createStatement();

			Log.info("NPCDataDB STARTUP REPORTING..");


			result = statData.executeQuery("Select * from NPCnextNumber;");
			if (result.next()) {
				nextTeamNumber = result.getInt("nextTeamNumber");
				nextUnitNumber = result.getInt("nextUnitNumber");
				if (result.next()) {
					throw new SQLException("Data : NPCnextNumber has >1 rows");
				}
				Log.info("nextTeamNumber = " + nextTeamNumber);
				Log.info("nextUnitNumber = " + nextUnitNumber);
			} else {
				throw new SQLException("Data : NPCnextNumber has 0 row");
			}
			
			result = statAccount.executeQuery("Select * from NPCnextNumber;");
			if (result.next()) {
				nextTeamNumber = result.getInt("nextAccountNumber");
				if (result.next()) {
					throw new SQLException("Account : NPCnextNumber has >1 rows");
				}
				Log.info("nextAccountNumber = " + nextAccountNumber);
			} else {
				throw new SQLException("Account : NPCnextNumber has 0 row");
			}

			result = statData.executeQuery("Select count(*) as cTeam from NPCTeam");
			result.next();
			Log.info("TEAM COUNT : " + result.getInt("cTeam"));

			result = statData.executeQuery("Select count(*) as cUnit from NPCUnit");
			result.next();
			Log.info("UNIT COUNT : " + result.getInt("cUnit"));

			result = statData.executeQuery("Select * from NPCUnit");
			while (result.next()) {
				System.out.println(result.getString("unitName") + " "
						+ result.getInt("unitNumber"));
			}

			
			createAccount_prep = connectionAccount.prepareStatement(createAccount);
			createAccountTrigger_prep = connectionAccount.prepareStatement(createAccountTrigger);
			getAccount_prep = connectionAccount.prepareStatement(getAccount);
			deleteAccount_prep = connectionAccount.prepareStatement(deleteAccount);
			
			createUnit_prep = connectionData.prepareStatement(createUnit);
			createUnitTrigger_prep = connectionData.prepareStatement(createUnitTrigger);
			deleteUnit_prep = connectionData.prepareStatement(deleteUnit);
			deleteUnitTrigger_prep = connectionData.prepareStatement(deleteUnitTrigger);
			getUnitByAccountNumber_prep = connectionData.prepareStatement(getUnitByAccountNumber);
			getUnitByTeamNumber_prep = connectionData.prepareStatement(getUnitByTeamNumber);
			getUnitByUnitNumber_prep = connectionData.prepareStatement(getUnitByUnitNumber);
			editUnit_prep = connectionData.prepareStatement(editUnit);

			createTeam_prep = connectionData.prepareStatement(createTeam);
			createTeamTrigger_prep = connectionData.prepareStatement(createTeamTrigger);
			deleteTeam_prep = connectionData.prepareStatement(deleteTeam);
			deleteTeamTrigger_prep = connectionData.prepareStatement(deleteTeamTrigger);
			getTeamByAccountNumber_prep = connectionData.prepareStatement(getTeamByAccountNumber);
			getTeamByUnitNumber_prep = connectionData.prepareStatement(getTeamByUnitNumber);
			editTeam_prep = connectionData.prepareStatement(editTeam);

			registerUnit_prep = connectionData.prepareStatement(registerUnit);
			unregisterUnit_prep = connectionData.prepareStatement(unregisterUnit);

			createTactic_prep = connectionData.prepareStatement(createTactic);
			createTacticTrigger_prep = connectionData.prepareStatement(createTacticTrigger);
			deleteTactic_prep = connectionData.prepareStatement(deleteTactic);
			deleteTacticTrigger_prep = connectionData.prepareStatement(deleteTacticTrigger);
			getTactic_prep = connectionData.prepareStatement(getTactic);
			editTactic_prep = connectionData.prepareStatement(editTactic);
			editTacticTrigger_prep = connectionData.prepareStatement(editTacticTrigger);

			learnSkill_prep = connectionData.prepareStatement(learnSkill);
			getSkill_prep = connectionData.prepareStatement(getSkill);
			updateSkill_prep = connectionData.prepareStatement(updateSkill);

			gainItem_prep = connectionData.prepareStatement(gainItem);
			dropItem_prep = connectionData.prepareStatement(dropItem);
			dropItemTrigger_prep = connectionData.prepareStatement(dropItemTrigger);
			getItem_prep = connectionData.prepareStatement(getItem);

			// Trigger Making Execution
			createAccountTrigger_prep.execute();
			createUnitTrigger_prep.execute();
			deleteUnitTrigger_prep.execute();
			createTeamTrigger_prep.execute();
			createTacticTrigger_prep.execute();
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
			this.connectionData.close();
			this.connectionAccount.close();
		} catch (SQLException e) {
			e.printStackTrace();
			return false;
		}
		return true;
	}
	
	public boolean CreateAccount() throws SQLException{
		createAccount_prep.execute();
		return true;
	}
	
	public ResultSet GetAccount() throws SQLException{
		return getAccount_prep.executeQuery();
	}
	
	public boolean DeleteAccount(int accountNumber) throws SQLException{
		deleteAccount_prep.setInt(1, accountNumber);
		deleteAccount_prep.execute();
		return true;
	}

	public int CreateUnit(int accountNumber, String unitname, int cls) throws SQLException {
		int thisUnitNumber = nextUnitNumber;
		// System.out.println("New Unit Num : " + thisUnitNumber);
		Random r = new Random();
		
//		createUnit_prep.setInt(1, thisUnitNumber);
//		createUnit_prep.setInt(2, accountNumber);
//		createUnit_prep.setString(3, unitname);
//		createUnit_prep.setInt(4, cls);
//		createUnit_prep.setInt(5, 10 + r.nextInt(7));
//		createUnit_prep.setInt(6, 10 + r.nextInt(7));
//		createUnit_prep.setInt(7, 20 + r.nextInt(7));
//		createUnit_prep.setInt(8, 20 + r.nextInt(7));
//		createUnit_prep.setInt(9, 10 + r.nextInt(7));
		
		createUnit_prep.setInt(1, accountNumber);
		createUnit_prep.setString(2, unitname);
		createUnit_prep.setInt(3, cls);
		createUnit_prep.setInt(4, 10 + r.nextInt(7));
		createUnit_prep.setInt(5, 10 + r.nextInt(7));
		createUnit_prep.setInt(6, 20 + r.nextInt(7));
		createUnit_prep.setInt(7, 20 + r.nextInt(7));
		createUnit_prep.setInt(8, 10 + r.nextInt(7));
		
		
		createUnit_prep.execute();
		
		nextUnitNumber += 1;

		return thisUnitNumber;
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

//		int thisTeamNumber = nextTeamNumber + 1;

		createTeam_prep.setInt(1, accountNumber);
//		createTeam_prep.setInt(2, thisTeamNumber);
		createTeam_prep.setString(2, teamname);
		
		createTeam_prep.execute();

//		nextTeamNumber += 1;
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

	public boolean SavenextNumbers() throws SQLException {
		PreparedStatement p = connectionData.prepareStatement("Update nextNumber set nextTeamNumber = ?, nextUnitNumber = ?;");
		p.setInt(1, nextTeamNumber);
		p.setInt(2, nextUnitNumber);
		p.execute();

		return true;
	}

}
