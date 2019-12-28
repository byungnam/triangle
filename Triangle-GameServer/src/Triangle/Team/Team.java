package Triangle.Team;

public class Team implements Cloneable {
	private int userNumber;
	private int teamNumber;
	private String teamName;

	public Team(int teamNumber, String teamName) {
		this.teamNumber = teamNumber;
		this.teamName = teamName;
	}

	public Team(int userNumber, int teamNumber, String teamName) {
		this.userNumber = userNumber;
		this.teamNumber = teamNumber;
		this.teamName = teamName;
	}

	public int getUserNumber() {
		return userNumber;
	}

	public void setUserNumber(int userNumber) {
		this.userNumber = userNumber;
	}

	public int getTeamNumber() {
		return teamNumber;
	}

	public void setTeamNumber(int teamNumber) {
		this.teamNumber = teamNumber;
	}

	public String getTeamName() {
		return teamName;
	}

	public void setTeamName(String teamName) {
		this.teamName = teamName;
	}

	public String toString() {
		return new String("Team Info : userNumber : " + userNumber
				+ " teamNumber : " + teamNumber + " teamname : " + teamName);
	}

	public Team clone() {
		try {
			return (Team) super.clone();
		} catch (CloneNotSupportedException e) {
			e.printStackTrace();
			return null;
		}
	}
}
