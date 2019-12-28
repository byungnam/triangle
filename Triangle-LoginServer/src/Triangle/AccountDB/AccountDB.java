package Triangle.AccountDB;


import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Statement;

import org.apache.log4j.Logger;
import org.sqlite.SQLiteConfig;

import Triangle.TriangleConfigure.TriangleConf;

public class AccountDB {
	private Logger Log = Logger.getLogger(AccountDB.class);
	private Connection connection;
	private String dbFileName;
	private boolean isOpened = false;

	private int nextAccountNumber;

	private PreparedStatement checkAccount_prep;
	private PreparedStatement createAccount_prep;
	private PreparedStatement createAccountTrigger_prep;
	private PreparedStatement authentication_prep;
	private PreparedStatement deleteAccount_prep;

	static {
		try {
			Class.forName("org.sqlite.JDBC");
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	public AccountDB(boolean isOperation) {
		if (isOperation) {
			this.dbFileName = TriangleConf.AccountTable;
		} else {
			this.dbFileName = TriangleConf.TestAccountTable;
		}
	}

	public boolean open() {
		try {
			SQLiteConfig config = new SQLiteConfig();
			this.connection = DriverManager.getConnection("jdbc:sqlite:/D:/Triangle/"
					+ this.dbFileName, config.toProperties());

			checkAccount_prep = this.connection.prepareStatement("SELECT count(*) from Account where id = ?;");
			
			createAccount_prep = this.connection.prepareStatement("INSERT INTO Account (accountNumber, id, password) SELECT nextAccountNumber, ?, ? from nextNumber;");
			createAccountTrigger_prep = this.connection.prepareStatement("CREATE TRIGGER IF NOT EXISTS createAcccountTrigger Insert on Account "
					+ "BEGIN "
					+ "UPDATE nextNumber set nextAccountNumber = nextAccountNumber +1;"
					+ "END;");
			authentication_prep = this.connection.prepareStatement("Select password, accountNumber from Account where id=?;");
			deleteAccount_prep = this.connection.prepareStatement("Delete from Account where id=? and password=?;");
			
			createAccountTrigger_prep.execute();
			
			ResultSet result;
			Statement stat = connection.createStatement();
			Log.info("AccountDB INITIATING REPORT..");

			result = stat.executeQuery("Select * from NextNumber;");
			if (result.next()) {
				nextAccountNumber = result.getInt("nextAccountNumber");
				if (result.next()) {
					throw new SQLException("NextNumber has >1 rows");
				}
				Log.info("NextAccountNumber = " + nextAccountNumber);
			} else {
				throw new SQLException("NextNumber has 0 row");
			}

			result = stat.executeQuery("Select count(*) as cAccount from Account;");
			result.next();
			Log.info("USER COUNT : " + result.getInt("cAccount"));

			result = stat.executeQuery("Select * from Account");
			Log.info("USER TABLE");
			while (result.next()) {
				Log.info(result.getInt("accountNumber") + " " + result.getString("id")
						+ " " + result.getString("password"));
			}

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

	public boolean CheckForIdConflict(String id){
		ResultSet result;
		boolean ret = false;
		try{
			checkAccount_prep.setString(1, id);
			result = checkAccount_prep.executeQuery();
			
			if(result.getInt(1) != 0){
				ret = false;
			} else {
				ret = true;
			}
		} catch(SQLException e){
			e.printStackTrace();
		}
		
		return ret;
	}
	
	public boolean CreateAccount(String id, String password) {
		try {
			createAccount_prep.setString(1, id);
			createAccount_prep.setString(2, password);
			createAccount_prep.execute();

			return true;
		} catch (SQLException e) {
			e.printStackTrace();
		}
		return false;
	}

	public int Authentication(String id, String password) {
		ResultSet rs;
		try {
			authentication_prep.setString(1, id);
			rs = authentication_prep.executeQuery();

			if (rs.next()) {
				if (password.equals(rs.getString("password"))) {
					return rs.getInt("accountNumber");
				}
			}
			return -1; // wrong id or pw
		} catch (SQLException e) {
			e.printStackTrace();
		}
		return -3; // exception error
	}

	public boolean DeleteAccount(String id, String password) {
		try {
			deleteAccount_prep.setString(1, id);
			deleteAccount_prep.setString(2, password);
			deleteAccount_prep.execute();
			return true;
		} catch (SQLException e) {
			e.printStackTrace();
		}
		return false;
	}

//	public void SaveNextNumber() {
//		try {
//			this.connection.createStatement().execute(
//					"Update NextNumber set nextAccountNumber=" + nextAccountNumber + ";");
//			Log.info("Saving nextAccountNumber = " + nextAccountNumber);
//		} catch (SQLException e) {
//			e.printStackTrace();
//		}
//	}
}
