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
import Triangle.TriangleValues.TriangleValues.UnitClass;
import Triangle.Unit.UnitBase;

import com.editor.global.Global;
import com.editor.popup.PopClickListener;
import com.editor.tableListeners.TableCellListener;
import com.editor.tableModels.UnitTableModel;
import com.editor.tools.DataDB;

@SuppressWarnings("serial")
public class UnitTableView extends TableView {
	private static UnitTableView singleton;
	private boolean isForUser;
	DataDB db;

	private UnitTableModel tm;
	private JComboBox<TriangleValues.UnitClass> classBox;

	public JComboBox<TriangleValues.UnitClass> getClassBox() {
		return classBox;
	}

	public void setClassBox(JComboBox<TriangleValues.UnitClass> classBox) {
		this.classBox = classBox;
	}

	private UnitTableView(JPanel parent, String title) {
		super(parent, title);
		table.setModel(new UnitTableModel());
		tm = (UnitTableModel) table.getModel();

		for (int i = 0; i < tm.getColumnCount(); i++) {
			if (tm.getColumnName(i).equals("Name")
					|| tm.getColumnName(i).equals("Class")) {
				setColumnWidth(i, 150);
			} else {
				setColumnWidth(i, 50);
			}
		}

		classBox = new JComboBox<>(TriangleValues.UnitClass.values());
		table.getColumnModel().getColumn(tm.findColumn("Class"))
				.setCellEditor(new DefaultCellEditor(classBox));

		Action action = new AbstractAction() {
			public void actionPerformed(ActionEvent e) {
				TableCellListener tcl = (TableCellListener) e.getSource();
				UnitBase _new = tm.getRow(tcl.getRow());
				// UnitBase _new = _old.clone();

				switch (tcl.getColumn()) {
				case 0: // unit number cannot be changed
					break;
				case 1: // Name
					_new.setUnitName((String) tcl.getNewValue());
					break;
				case 2: // Class
					_new.getDescriptor().setCls((UnitClass)tcl.getNewValue());
					break;
				case 3: // Level
					_new.getDescriptor().setLevel((int) tcl.getNewValue());
					break;
				case 4: // Str
					_new.getSpec().setStr((int) tcl.getNewValue());
					break;
				case 5: // Dex
					_new.getSpec().setDex((int) tcl.getNewValue());
					break;
				case 6: // Vital
					_new.getSpec().setVital((int) tcl.getNewValue());
					break;
				case 7: // Intel
					_new.getSpec().setIntel((int) tcl.getNewValue());
					break;
				case 8: // Speed
					_new.getSpec().setSpeed((int) tcl.getNewValue());
					break;
				case 9: // Exp
					_new.getDescriptor().setExp((int) tcl.getNewValue());
					break;

				}
				try {
					db
							.EditUnit(_new.getUnitName(),
									_new.getDescriptor().getLevel(),
									_new.getDescriptor().getCls().getNumber(),
									_new.getSpec().getStr(),
									_new.getSpec().getDex(),
									_new.getSpec().getVital(),
									_new.getSpec().getIntel(),
									_new.getSpec().getSpeed(),
									_new.getDescriptor().getPoint(),
									_new.getDescriptor().getExp(),
									_new.getUnitNumber());
				} catch (SQLException e1) {
					e1.printStackTrace();
				}
			}
		};

		new TableCellListener(table, action);
		table.addMouseListener(new PopClickListener(table));
	}

	public static synchronized void newInstance(JPanel parent, String title) {
		if (singleton == null) {
			singleton = new UnitTableView(parent, title);
		} else {
			singleton.setParent(parent);
			singleton.setTitle(title);
		}
	}
	
	public static UnitTableView getThis() {
		if (singleton == null) {
			newInstance(null, null);
		}
		return singleton;
	}

	@Override
	public void valueChanged(ListSelectionEvent e) {
		if (AccountTableView.getThis().table.isEditing()) {
			AccountTableView.getThis().table.getCellEditor().stopCellEditing();
		}
		if (TeamTableView.getThis().table.isEditing()) {
			TeamTableView.getThis().table.getCellEditor().stopCellEditing();
		}
//		if (UnitTableView.getThis().table.isEditing()) {
//			UnitTableView.getThis().table.getCellEditor().stopCellEditing();
//		}
		if (TacticTableView.getThis().table.isEditing()) {
			TacticTableView.getThis().table.getCellEditor().stopCellEditing();
		}
		if (e.getValueIsAdjusting()) {
			return;
		}
		if (e.getSource() == table.getSelectionModel()
				&& table.getSelectedRow() >= 0) {
			int unitNumber = tm.getRow(table.getSelectedRow()).getUnitNumber();
			Global.selectedUnitNumber = unitNumber;
			TacticTableView.getThis().setForUser(isForUser);
			TacticTableView.getThis().notifyDataSetChanged();
		}
	}

	public void addData(UnitBase u) {
		tm.addRow(u);
	}

	public void deleteData() {
		tm.removeRows(table.getSelectedRow());
	}

	@Override
	public void actionPerformed(ActionEvent e) {
		try {
			if (e.getActionCommand().equals("추가")) {
				int unitNumber = db.CreateUnit(
						Global.selectedAccountNumber, "Noname",
						UnitClass.BEGGINER.getNumber());
				if (Global.selectedTeamNumber >= 0) {
					db.RegisterUnit(unitNumber,
							Global.selectedTeamNumber);
				}
				if (table.isEditing()) {
					table.getCellEditor().stopCellEditing();
				}
				notifyDataSetChanged();
			} else if (e.getActionCommand().equals("삭제")) {
				if (table.getSelectedRow() >= 0) {
					db.DeleteUnit(Global.selectedUnitNumber);
					if (table.isEditing()) {
						table.getCellEditor().stopCellEditing();
					}
				}
				if (table.isEditing()) {
					table.getCellEditor().stopCellEditing();
				}
				notifyDataSetChanged();
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

		ResultSet resultUnit;
		
		// if TeamNumber is valid
		if (Global.selectedTeamNumber >= 0) {
			try {
				resultUnit = db.GetUnitByTeamNumber(
						Global.selectedTeamNumber);
				while (resultUnit.next()) {
					addData(new UnitBase(
							resultUnit.getInt("unitNumber"),
							resultUnit.getString("unitName"),
							resultUnit.getInt("str"), 
							resultUnit.getInt("dex"),
							resultUnit.getInt("vital"),
							resultUnit.getInt("intel"),
							resultUnit.getInt("speed"),
							resultUnit.getInt("cls"),
							resultUnit.getInt("exp"),
							resultUnit.getInt("level"),
							resultUnit.getInt("point")));
				}
			} catch (SQLException e1) {
				e1.printStackTrace();
			}
		// else if TeamNumber is -1, i.e. 전체
		} else if (Global.selectedAccountNumber >= 0) {
			try {
				resultUnit = db.GetUnitByAccountNumber(
						Global.selectedAccountNumber);
				while (resultUnit.next()) {
					addData(new UnitBase(
							resultUnit.getInt("unitNumber"),
							resultUnit.getString("unitName"),
							resultUnit.getInt("str"), 
							resultUnit.getInt("dex"),
							resultUnit.getInt("vital"),
							resultUnit.getInt("intel"),
							resultUnit.getInt("speed"),
							resultUnit.getInt("cls"),
							resultUnit.getInt("exp"),
							resultUnit.getInt("level"),
							resultUnit.getInt("point")));
				}
			} catch (SQLException e1) {
				e1.printStackTrace();
			}
		}
		Global.selectedUnitNumber = -1;
		TacticTableView.getThis().notifyDataSetChanged();
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
