package Triangle.dbms;

import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.SQLException;
import java.sql.Statement;
import java.util.Scanner;

import org.sqlite.SQLiteConfig;

import Triangle.TriangleConfigure.TriangleConf;

public class ResetTables {
	private Connection connection_User;
	private Connection connection_Data;
	public boolean isForReal;
	public boolean isUser;
	public String QUERY = "";

	public boolean open() {
		try {
			Class.forName("org.sqlite.JDBC");
			SQLiteConfig config = new SQLiteConfig();
			// config.setReadOnly(false);

			if (!isForReal) {
				this.connection_User = DriverManager.getConnection(
						"jdbc:sqlite:/D:/Triangle/"
								+ TriangleConf.TestAccountTable,
						config.toProperties());
				this.connection_Data = DriverManager.getConnection(
						"jdbc:sqlite:/D:/Triangle/"
								+ TriangleConf.TestDataTable,
						config.toProperties());
				// this.connection_Data = DriverManager.getConnection(
				// "jdbc:sqlite:/D:/Triangle/" + TriangleConf.TestNPCDataTable,
				// config.toProperties());
			} else {
				this.connection_User = DriverManager
						.getConnection("jdbc:sqlite:/D:/Triangle/"
								+ TriangleConf.AccountTable,
								config.toProperties());
				this.connection_Data = DriverManager.getConnection(
						"jdbc:sqlite:/D:/Triangle/" + TriangleConf.DataTable,
						config.toProperties());
				// this.connection_Data = DriverManager.getConnection(
				// "jdbc:sqlite:/D:/Triangle/" + TriangleConf.NPCDataTable,
				// config.toProperties());
			}
		} catch (SQLException e) {
			e.printStackTrace();
			return false;
		} catch (ClassNotFoundException e) {
			e.printStackTrace();
		}

		return true;
	}

	public boolean close() {
		try {
			this.connection_User.close();
			this.connection_Data.close();
		} catch (SQLException e) {
			e.printStackTrace();
			return false;
		}
		return true;
	}

	public boolean CreateTables() {
		String query;
		Statement stat;
		try {
			stat = connection_User.createStatement();
			query = "CREATE TABLE IF NOT EXISTS Account ("
					+ "accountNumber INT primary key unique not null, id TEXT unique not null, password TEXT not null);";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS NextNumber("
					+ "nextAccountNumber INT);";
			stat.executeUpdate(query);
			stat.close();

			// -----------------------

			stat = connection_Data.createStatement();
			query = "CREATE TABLE IF NOT EXISTS Team ("
					+ "teamNumber INT primary key unique not null, accountNumber INT, teamname TEXT);";
			stat.executeUpdate(query);

			stat = connection_Data.createStatement();
			query = "CREATE TABLE IF NOT EXISTS Regiment ("
					+ "unitNumber INT, teamNumber INT)";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS Unit "
					+ "("
					+ "unitNumber INT primary key unique not null, accountNumber INT, unitname TEXT, level INT, cls INT, str INT, "
					+ "dex INT, vital INT, intel INT, speed INT, point INT, exp INT"
					+ " );";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS Tactic "
					+ "("
					+ "unitNumber INT, priority INT, condition INT, value INT, action INT, actionLevel INT, primary key (unitNumber, priority)"
					+ " );";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS Skill "
					+ "("
					+ "unitNumber INT not null, skillNumber INT not null, level INT, exp INT, primary Key (unitNumber, skillNumber)"
					+ " );";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS Item "
					+ "("
					+ "accountNumber INT not null, itemNumber INT not null, quantity INT, primary Key (accountNumber, itemNumber)"
					+ " );";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS NextNumber ( nextTeamNumber INT, nextUnitNumber INT );";
			stat.executeUpdate(query);
			stat.close();

			System.out.println("Finish Create Tables");
			return true;
		} catch (SQLException e) {
			e.printStackTrace();
			return false;
		}
	}

	public boolean CreateNPCTables() {
		String query;
		Statement stat;
		try {
			stat = connection_User.createStatement();
			query = "CREATE TABLE IF NOT EXISTS NPCAccount ("
					+ "accountNumber INT primary key unique not null);";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS NPCNextNumber("
					+ "nextAccountNumber INT);";
			stat.executeUpdate(query);
			stat.close();

			// -----------------------

			stat = connection_Data.createStatement();
			query = "CREATE TABLE IF NOT EXISTS NPCTeam ("
					+ "teamNumber INT primary key unique not null, accountNumber INT, teamname TEXT);";
			stat.executeUpdate(query);

			stat = connection_Data.createStatement();
			query = "CREATE TABLE IF NOT EXISTS NPCRegiment ("
					+ "unitNumber INT, teamNumber INT)";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS NPCUnit "
					+ "("
					+ "unitNumber INT primary key unique not null, accountNumber INT, unitname TEXT, level INT, cls INT, str INT, "
					+ "dex INT, vital INT, intel INT, speed INT, point INT, exp INT"
					+ " );";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS NPCTactic "
					+ "("
					+ "unitNumber INT, priority INT, condition INT, value INT, action INT, actionLevel INT, primary key (unitNumber, priority)"
					+ " );";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS NPCSkill "
					+ "("
					+ "unitNumber INT not null, skillNumber INT not null, level INT, exp INT, primary Key (unitNumber, skillNumber)"
					+ " );";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS NPCItem "
					+ "("
					+ "accountNumber INT not null, itemNumber INT not null, quantity INT, primary Key (accountNumber, itemNumber)"
					+ " );";
			stat.executeUpdate(query);

			query = "CREATE TABLE IF NOT EXISTS NPCNextNumber ( nextTeamNumber INT, nextUnitNumber INT );";
			stat.executeUpdate(query);
			stat.close();

			System.out.println("Finish Create NPCTables");
			return true;
		} catch (SQLException e) {
			e.printStackTrace();
			return false;
		}
	}

	public boolean DeleteTables() {
		String query;
		Statement stat;
		try {
			stat = connection_User.createStatement();
			query = "Drop Table if exists Account";
			stat.executeUpdate(query);

			query = "Drop Table if exists NextNumber";
			stat.executeUpdate(query);
			stat.close();

			stat = connection_Data.createStatement();
			query = "Drop Table if exists Team";
			stat.executeUpdate(query);

			stat = connection_Data.createStatement();
			query = "Drop Table if exists Regiment";
			stat.executeUpdate(query);

			query = "Drop Table if exists Unit";
			stat.executeUpdate(query);

			query = "Drop Table if exists Tactic";
			stat.executeUpdate(query);

			query = "Drop Table if exists Skill";
			stat.executeUpdate(query);

			query = "Drop Table if exists Item";
			stat.executeUpdate(query);

			query = "Drop Table if exists NextNumber";
			stat.executeUpdate(query);

			System.out.println("Finish Drop Tables");

			return true;
		} catch (Exception e) {
			e.printStackTrace();
			return false;
		}
	}

	public boolean DeleteNPCTables() {
		String query;
		Statement stat;
		try {
			stat = connection_User.createStatement();
			query = "Drop Table if exists NPCAccount";
			stat.executeUpdate(query);
			
			stat = connection_User.createStatement();
			query = "Drop Table if exists NPCNextNumber";
			stat.executeUpdate(query);
			stat.close();

			stat = connection_Data.createStatement();
			query = "Drop Table if exists NPCTeam";
			stat.executeUpdate(query);

			stat = connection_Data.createStatement();
			query = "Drop Table if exists NPCRegiment";
			stat.executeUpdate(query);

			query = "Drop Table if exists NPCUnit";
			stat.executeUpdate(query);

			query = "Drop Table if exists NPCTactic";
			stat.executeUpdate(query);

			query = "Drop Table if exists NPCSkill";
			stat.executeUpdate(query);

			query = "Drop Table if exists NPCItem";
			stat.executeUpdate(query);

			query = "Drop Table if exists NPCNextNumber";
			stat.executeUpdate(query);

			System.out.println("Finish Drop NPCTables");

			return true;
		} catch (Exception e) {
			e.printStackTrace();
			return false;
		}
	}
	
	private ResetTables() {
		System.out
				.println("1: TestData, 2: TestNPCData, 3: RealData, 4: RealNPCData, Else: Do nothing");
		Scanner scanIn = new Scanner(System.in);
		switch (scanIn.nextInt()) {
		case 1:
			isForReal = false;
			isUser = true;
			break;
		case 2:
			isForReal = false;
			isUser = false;
			break;
		case 3:
			isForReal = true;
			isUser = true;
			break;
		case 4:
			isForReal = true;
			isUser = false;
			break;
		default:
			System.out.println("Nothing Happened");
			scanIn.close();
			return;
		}
		scanIn.close();
	}
	
	public ResetTables(int number) {
		switch (number) {
		case 1:
			isForReal = false;
			isUser = true;
			break;
		case 2:
			isForReal = false;
			isUser = false;
			break;
		case 3:
			isForReal = true;
			isUser = true;
			break;
		case 4:
			isForReal = true;
			isUser = false;
			break;
		default:
			System.out.println("Nothing Happened");
			return;
		}
		doReset();
	}

	public ResetTables(boolean isForReal, boolean isUser) {
		this.isForReal = isForReal;
		this.isUser = isUser;
		doReset();
	}
	
	private void doReset(){
		open();
		if (isUser) {
			DeleteTables();
			CreateTables();
		} else {
			DeleteNPCTables();
			CreateNPCTables();
		}
		new NextNumbersGenerator(isForReal, isUser);
		close();
	}

	public static void main(String[] args) {
		new ResetTables();
	}
}
