package com.editor.tableViews;

import java.awt.event.ActionEvent;
import java.sql.ResultSet;
import java.sql.SQLException;

import javax.swing.AbstractAction;
import javax.swing.Action;
import javax.swing.JPanel;
import javax.swing.event.ListSelectionEvent;

import Triangle.Team.Team;

import com.editor.global.Global;
import com.editor.tableListeners.TableCellListener;
import com.editor.tableModels.TeamTableModel;
import com.editor.tools.DataDB;

@SuppressWarnings("serial")
public class TeamTableView extends TableView {
	private static TeamTableView singleton;
	private boolean isForUser;
	private TeamTableModel tm;
	private DataDB db;

	private TeamTableView(JPanel parent, String title) {
		super(parent, title);

		table.setModel(new TeamTableModel());
		tm = (TeamTableModel) table.getModel();

		setColumnWidth(0, 20);
		setColumnWidth(1, 20);

//		ResultSet resultAccount;
//		try {
//			resultAccount = db.GetAccount();
//			while (resultAccount.next()) {
//				int useridx = resultAccount.getInt("accountNumber");
//				ResultSet resultTeam;
//				resultTeam = db.GetTeamByAccountNumber(useridx);
//				while (resultTeam.next()) {
//					int teamidx = resultTeam.getInt("teamNumber");
//					String teamName = resultTeam.getString("teamName");
//					addData(new Team(useridx, teamidx, teamName));
//				}
//			}
//		} catch (SQLException e1) {
//			e1.printStackTrace();
//		}

		Action action = new AbstractAction() {
			public void actionPerformed(ActionEvent e) {
				TableCellListener tcl = (TableCellListener) e.getSource();
				Team _old = tm.getRow(tcl.getRow());
				Team _new = _old.clone();
				switch (tcl.getColumn()) {
				case 0: // user number cannot be changed
					break;
				case 1: // team number
					break;
				case 2: // team Name
					_new.setTeamName((String) tcl.getNewValue());
					break;
				}
				try {
					db.EditTeam(_new.getTeamName(), _new.getTeamNumber());
				} catch (SQLException e1) {
					e1.printStackTrace();
				}
			}
		};

		new TableCellListener(table, action);
	}

	public static synchronized void newInstance(JPanel parent, String title) {
		if (singleton == null) {
			singleton = new TeamTableView(parent, title);
		} else {
			singleton.setParent(parent);
			singleton.setTitle(title);
		}
	}

	public static TeamTableView getThis() {
		if (singleton == null) {
			newInstance(null, null);
		}
		return singleton;
	}

	public void addData(Team team) {
		tm.addRow(team);
	}

	public void deleteData() {
		tm.removeRows(table.getSelectedRow());
	}

	public int getSelectedTeamidx() {
		return (int) table.getModel().getValueAt(table.getSelectedRow(), 0);
	}

	@Override
	public void valueChanged(ListSelectionEvent e) { // table click listener
		if (AccountTableView.getThis().table.isEditing()) {
			AccountTableView.getThis().table.getCellEditor().stopCellEditing();
		}
		// if (TeamTableView.getThis().table.isEditing()) {
		// TeamTableView.getThis().table.getCellEditor().stopCellEditing();
		// }
		if (UnitTableView.getThis().table.isEditing()) {
			UnitTableView.getThis().table.getCellEditor().stopCellEditing();
		}
		if (TacticTableView.getThis().table.isEditing()) {
			TacticTableView.getThis().table.getCellEditor().stopCellEditing();
		}
		if (e.getValueIsAdjusting()) {
			return;
		}
		if (e.getSource() == table.getSelectionModel()
				&& table.getSelectedRow() >= 0) {
			Global.selectedTeamNumber = (Integer) tm.getValueAt(
					table.getSelectedRow(), 0);
			UnitTableView.getThis().notifyDataSetChanged();
		}
	}

	@Override
	public void actionPerformed(ActionEvent e) { // button listener
		try {
			if (e.getActionCommand().equals("추가")) {
				db.CreateTeam(Global.selectedAccountNumber, "Noname");
				if (table.isEditing()) {
					table.getCellEditor().stopCellEditing();
				}
				notifyDataSetChanged();
			} else if (e.getActionCommand().equals("삭제")) {
				if (table.getSelectedRow() >= 0) {
					db.DeleteTeam((int) tm.getValueAt(table.getSelectedRow(), 0));
					if (table.isEditing()) {
						table.getCellEditor().stopCellEditing();
					}
					if (UnitTableView.getThis().table.isEditing()) {
						UnitTableView.getThis().table.getCellEditor()
								.stopCellEditing();
					}
					if (TacticTableView.getThis().table.isEditing()) {
						TacticTableView.getThis().table.getCellEditor()
								.stopCellEditing();
					}
					UnitTableView.getThis().table.clearSelection();
					UnitTableView.getThis().clearTable();
					TacticTableView.getThis().table.clearSelection();
					TacticTableView.getThis().clearTable();
				}
			}
		} catch (SQLException e1) {
			e1.printStackTrace();
		}
	}

	@Override
	public void clearTable() {
		if (tm.getRowCount() > 0) {
			tm.removeRowRange(0, tm.getRowCount() - 1);
		}
	}

	public void notifyDataSetChanged() {
		clearTable();
		if (Global.selectedAccountNumber >= 0) {
			addData(new Team(-1, "전체"));
		}
		table.getSelectionModel().clearSelection();

		ResultSet resultTeam;
		if (Global.selectedAccountNumber >= 0) {
			try {
				resultTeam = db
						.GetTeamByAccountNumber(Global.selectedAccountNumber);
				while (resultTeam.next()) {
					addData(new Team(resultTeam.getInt("teamNumber"),
							resultTeam.getString("teamName")));
				}
			} catch (SQLException e1) {
				e1.printStackTrace();
			}
		}
		Global.selectedTeamNumber = -1;
		UnitTableView.getThis().setForUser(isForUser);
		UnitTableView.getThis().notifyDataSetChanged();
	}

	public boolean isForUser() {
		return isForUser;
	}

	public void setForUser(boolean isForUser) {
		this.isForUser = isForUser;
		if (isForUser) {
			db = DataDB.getUser();
		} else {
			db = DataDB.getNPC();
		}
	}
}
