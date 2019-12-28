package com.editor.tableViews;

import java.awt.event.ActionEvent;
import java.sql.ResultSet;
import java.sql.SQLException;

import javax.swing.AbstractAction;
import javax.swing.Action;
import javax.swing.DefaultCellEditor;
import javax.swing.JComboBox;
import javax.swing.JPanel;
import javax.swing.event.ListSelectionEvent;

import Triangle.TriangleValues.TriangleValues;
import Triangle.TriangleValues.TriangleValues.Conditions;
import Triangle.Unit.Tactic.Tactic;

import com.editor.global.Global;
import com.editor.tableListeners.TableCellListener;
import com.editor.tableModels.TacticTableModel;
import com.editor.tools.DataDB;

@SuppressWarnings("serial")
public class TacticTableView extends TableView {
	private static TacticTableView singleton;
	private boolean isForUser;
	DataDB db;
	
	private TacticTableModel tm;

	private TacticTableView(JPanel parent, String title) {
		super(parent, title);
		table.setModel(new TacticTableModel());
		tm = (TacticTableModel) table.getModel();

		for (int i = 0; i < tm.getColumnCount(); i++) {
			if (tm.getColumnName(i).equals("TacticNum")
					|| tm.getColumnName(i).equals("Priority")
					|| tm.getColumnName(i).equals("Value")
					|| tm.getColumnName(i).equals("ActionLevel")) {
				setColumnWidth(i, 40);
			} else if (tm.getColumnName(i).equals("Action")) {
				setColumnWidth(i, 150);
			} else {
				setColumnWidth(i, 220);
			}
		}

		JComboBox<TriangleValues.Conditions> conditionBox = new JComboBox<>(
				TriangleValues.Conditions.values());
		JComboBox<TriangleValues.Action> actionBox = new JComboBox<>(
				TriangleValues.Action.values());
		// JComboBox<TriangleValues.ACTIONPARAMS> box = new
		// JComboBox<>(TriangleValues.ACTIONPARAMS.values());
		table.getColumnModel().getColumn(tm.findColumn("Condition"))
				.setCellEditor(new DefaultCellEditor(conditionBox));
		table.getColumnModel().getColumn(tm.findColumn("Action"))
				.setCellEditor(new DefaultCellEditor(actionBox));

		Action action = new AbstractAction() {
			public void actionPerformed(ActionEvent e) {
				TableCellListener tcl = (TableCellListener) e.getSource();
				Tactic _old = tm.getRow(tcl.getRow());
				System.out.println(_old);
				Tactic _new = _old.clone();

				switch (table.getSelectedColumn()) {
				case 0: // priority
					_new.setPriority((int) tcl.getNewValue());
					break;
				case 1: // Condition
					_new.setCondition((Conditions) tcl.getNewValue());
					break;
				case 2: // Value
					_new.setValue((int) tcl.getNewValue());
					break;
				case 3: // Action
					_new.setAction((TriangleValues.Action) tcl.getNewValue());
					break;
				case 4: // ActionParam
					_new.setActionLevel((int) tcl.getNewValue());
					break;
				}
				try {
					db.EditTactic(_new.getPriority(),
							_new.getCondition().getNumber(), _new.getValue(),
							_new.getAction().getNumber(),
							_new.getActionLevel(), 
							Global.selectedUnitNumber,
							_old.getPriority());
				} catch (SQLException e1) {
					e1.printStackTrace();
				}
			}
		};

		new TableCellListener(table, action);
	}

	public static synchronized void newInstance(JPanel parent, String title) {
		if (singleton == null) {
			singleton = new TacticTableView(parent, title);
		} else {
			singleton.setParent(parent);
			singleton.setTitle(title);
		}
	}

	public static TacticTableView getThis() {
		if (singleton == null) {
			newInstance(null, null);
		}
		return singleton;
	}

	@Override
	public void valueChanged(ListSelectionEvent e) {
		if (AccountTableView.getThis().table.isEditing()) {
//			AccountTableView.getThis().table.removeEditor();
			AccountTableView.getThis().table.getCellEditor().stopCellEditing();
		}
		if (TeamTableView.getThis().table.isEditing()) {
//			TeamTableView.getThis().table.removeEditor();
			TeamTableView.getThis().table.getCellEditor().stopCellEditing();
		}
		if (UnitTableView.getThis().table.isEditing()) {
//			UnitTableView.getThis().table.removeEditor();
			UnitTableView.getThis().table.getCellEditor().stopCellEditing();
		}
//		if (TacticTableView.getThis().table.isEditing()) {
//			TacticTableView.getThis().table.removeEditor();
//			TacticTableView.getThis().table.getCellEditor().stopCellEditing();
//		}
		if (e.getValueIsAdjusting()) {
			return;
		}
		if (e.getSource() == table.getSelectionModel()
				&& table.getSelectedRow() >= 0) {
			Integer TacticNumber = (Integer) tm.getValueAt(
					table.getSelectedRow(), 0);
			Global.selectedTacticNumber = TacticNumber;
		}
	}

	public void addData(Tactic t) {
		tm.addRow(t);
	}

	@Override
	public void actionPerformed(ActionEvent e) {
		try {
			if (e.getActionCommand().equals("추가")) {
				db.CreateTactic(Global.selectedUnitNumber);
				if (table.isEditing()) {
					table.getCellEditor().stopCellEditing();
				}
				notifyDataSetChanged();
			} else if (e.getActionCommand().equals("삭제")) {
				if (table.getSelectedRow() >= 0) {
					db.DeleteTactic(
							getSelectedIdOfParent(),
							(int) tm.getValueAt(table.getSelectedRow(),
									tm.findColumn("Priority")));
					if (table.isEditing()) {
						table.getCellEditor().stopCellEditing();
					}
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
		table.getSelectionModel().clearSelection();

		ResultSet resultTactic;
		if (Global.selectedUnitNumber >= 0) {
			try {
				resultTactic = db.GetTactic(
						Global.selectedUnitNumber);
				while (resultTactic.next()) {
					TacticTableView.getThis().addData(
							new Tactic(resultTactic.getInt("priority"),
									resultTactic.getInt("condition"),
									resultTactic.getInt("value"), resultTactic
											.getInt("action"), resultTactic
											.getInt("actionLevel")));
				}
			} catch (SQLException e1) {
				e1.printStackTrace();
			}
		}
		Global.selectedTacticNumber = -1;
	}

	public boolean isForUser() {
		return isForUser;
	}

	public void setForUser(boolean isForUser) {
		this.isForUser = isForUser;
		if(isForUser){
			db = DataDB.getUser();
		} else {
			db = DataDB.getNPC();
		}
	}
}
