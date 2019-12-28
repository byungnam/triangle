package com.editor.tableModels;

import java.util.LinkedList;

import Triangle.Team.Team;

@SuppressWarnings("serial")
public class TeamTableModel extends RowTableModel<Team> {
	public TeamTableModel() {
		super(Team.class);
		LinkedList<String> col = new LinkedList<>();

		col.add("TeamNumber");
		col.add("TeamName");

		setDataAndColumnNames(new LinkedList<Team>(), col);

	}

	@Override
	public Object getValueAt(int rowIndex, int columnIndex) {
		if (rowIndex >= 0) {
			Team team = getRow(rowIndex);
			switch (columnIndex) {
			case 0:
				return team.getTeamNumber();
			case 1:
				return team.getTeamName();
			}
		}
		return null;
	}

	@Override
	public void setValueAt(Object value, int row, int column) {
		Team team = getRow(row);

		switch (column) {
		case 0:
			team.setTeamNumber((Integer) value);
			break;
		case 1:
			team.setTeamName((String) value);
			break;
		}
	}

	@Override
	public boolean isCellEditable(int row, int column) {
		if (column == 0) {
			return false;
		} else {
			return true;
		}
	}

	@Override
	public Class<?> getColumnClass(int col) {
		if (col == 0) {
			return Integer.class;
		} else {
			return String.class;
		}
	}

}
