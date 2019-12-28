package com.editor.tableViews;

import java.awt.event.ActionEvent;
import java.sql.ResultSet;
import java.sql.SQLException;

import javax.swing.JPanel;
import javax.swing.event.ListSelectionEvent;

import com.editor.global.Global;
import com.editor.tableModels.AccountTableModel;
import com.editor.tools.DataDB;

public class AccountTableView extends TableView {
	private static AccountTableView singleton;

	private AccountTableModel tm;

	private AccountTableView(JPanel parent, String title) {
		super(parent, title);

		table.setModel(new AccountTableModel());
		tm = (AccountTableModel) table.getModel();
		setColumnWidth(0, 20);
	}

	public static synchronized void newInstance(JPanel parent, String title) {
		if (singleton == null) {
			singleton = new AccountTableView(parent, title);
		} else {
			singleton.setParent(parent);
			singleton.setTitle(title);
		}
	}
	
	public static AccountTableView getThis() {
		if (singleton == null) {
			newInstance(null, null);
		}
		return singleton;
	}

	public void addData(int accountNumber) {
		tm.addRow(accountNumber);
	}

	public void deleteData() {
		tm.removeRows(table.getSelectedRow());
	}

	@Override
	public void valueChanged(ListSelectionEvent e) { // table click listener
		NPCAccountTableView.getThis().table.clearSelection();
//		if (AccountTableView.getThis().table.isEditing()) {
//			AccountTableView.getThis().table.getCellEditor().stopCellEditing();
//		}
		if (TeamTableView.getThis().table.isEditing()) {
			TeamTableView.getThis().table.getCellEditor().stopCellEditing();
		}
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
			Integer accountNumber = (Integer) tm.getValueAt(
					table.getSelectedRow(), 0);
			Global.selectedAccountNumber = accountNumber;
			TeamTableView.getThis().setForUser(true);
			TeamTableView.getThis().notifyDataSetChanged();
			UnitTableView.getThis().setForUser(true);
			UnitTableView.getThis().notifyDataSetChanged();
		}
	}

	@Override
	public void actionPerformed(ActionEvent e) { // button listener
		try {
			if (e.getActionCommand().equals("추가")) {
				DataDB.getUser().CreateAccount();
				if (table.isEditing()) {
					table.getCellEditor().stopCellEditing();
				}
			} else if (e.getActionCommand().equals("삭제")) {
				if (table.getSelectedRow() >= 0) {
					DataDB.getUser().DeleteAccount(
							(int) tm.getValueAt(table.getSelectedRow(), 0));
					if (table.isEditing()) {
						table.getCellEditor().stopCellEditing();
					}
					if (TeamTableView.getThis().table.isEditing()) {
						TeamTableView.getThis().table.getCellEditor()
								.stopCellEditing();
					}
					if (UnitTableView.getThis().table.isEditing()) {
						UnitTableView.getThis().table.getCellEditor()
								.stopCellEditing();
					}
					if (TacticTableView.getThis().table.isEditing()) {
						TacticTableView.getThis().table.getCellEditor()
								.stopCellEditing();
					}
					
					TeamTableView.getThis().table.clearSelection();
					UnitTableView.getThis().table.clearSelection();
					TacticTableView.getThis().table.clearSelection();
					Global.selectedAccountNumber = -1;
				}
			}
		} catch (SQLException e1) {
			// TODO Auto-generated catch block
			e1.printStackTrace();
		}
		notifyDataSetChanged();
	}

	@Override
	public void clearTable() {
		if (tm.getRowCount() > 0) {
			tm.removeRowRange(0, tm.getRowCount() - 1);
		}
	}

	public void notifyDataSetChanged() {
		NPCAccountTableView.getThis().table.clearSelection();
		clearTable();
		ResultSet resultAccount;
		try {
			resultAccount = DataDB.getUser().GetAccount();
			while (resultAccount.next()) {
				int useridx = resultAccount.getInt("accountNumber");
				addData(useridx);
			}
		} catch (SQLException e1) {
			e1.printStackTrace();
		}
		Global.selectedAccountNumber = -1;
		TeamTableView.getThis().notifyDataSetChanged();
	}
}
