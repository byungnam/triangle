package Triangle.dbms;

import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.SQLException;

import org.sqlite.SQLiteConfig;

import Triangle.TriangleConfigure.TriangleConf;

public class NextNumbersGenerator {
	private Connection connection_Account;
	private Connection connection_Data;

	static {
		try {
			Class.forName("org.sqlite.JDBC");
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	public NextNumbersGenerator(boolean isForReal){
		new NextNumbersGenerator(isForReal, true);
		new NextNumbersGenerator(isForReal, false);
	}
	
	public NextNumbersGenerator(boolean isForReal, boolean isUser) {
		try {
			SQLiteConfig config = new SQLiteConfig();

			if (!isForReal) {
				this.connection_Account = DriverManager.getConnection(
						"jdbc:sqlite:/D:/Triangle/"
								+ TriangleConf.TestAccountTable,
						config.toProperties());
				this.connection_Data = DriverManager.getConnection(
						"jdbc:sqlite:/D:/Triangle/"
								+ TriangleConf.TestDataTable,
						config.toProperties());
			} else {

				this.connection_Account = DriverManager
						.getConnection("jdbc:sqlite:/D:/Triangle/"
								+ TriangleConf.AccountTable,
								config.toProperties());
				this.connection_Data = DriverManager.getConnection(
						"jdbc:sqlite:/D:/Triangle/" + TriangleConf.DataTable,
						config.toProperties());
			}
		} catch (SQLException e) {
			e.printStackTrace();
		}

		try {
			if (isUser) {
				connection_Account.createStatement().execute(
						"DELETE from NextNumber;");
				connection_Account
						.createStatement()
						.execute(
								"INSERT INTO NextNumber (nextAccountNumber) values (1);");

				connection_Data.createStatement().execute(
						"DELETE from NextNumber;");
				connection_Data.createStatement().execute(
						"INSERT INTO NextNumber (nextTeamNumber, nextUnitNumber) "
								+ "VALUES(1, 1);");
			} else {
				connection_Account.createStatement().execute(
						"DELETE from NPCNextNumber;");
				connection_Account
						.createStatement()
						.execute(
								"INSERT INTO NPCNextNumber (nextAccountNumber) values (1);");

				connection_Data.createStatement().execute(
						"DELETE from NPCNextNumber;");
				connection_Data.createStatement().execute(
						"INSERT INTO NPCNextNumber (nextTeamNumber, nextUnitNumber) "
								+ "VALUES(1, 1);");
			}
			System.out.println("FINISH CREATE NEXTNUMBER");

			connection_Account.close();
			connection_Data.close();

		} catch (Exception e) {
			e.printStackTrace();
		}

	}

}
