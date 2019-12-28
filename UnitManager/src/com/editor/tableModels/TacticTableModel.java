package com.editor.tableModels;

import java.util.LinkedList;

import Triangle.TriangleValues.TriangleValues.Action;
import Triangle.TriangleValues.TriangleValues.Conditions;
import Triangle.Unit.Tactic.Tactic;

@SuppressWarnings("serial")
public class TacticTableModel extends RowTableModel<Tactic> {

	public TacticTableModel() {
		super(Tactic.class);
		LinkedList<String> col = new LinkedList<>();
		col.add("Priority");
		col.add("Condition");
		col.add("Value");
		col.add("Action");
		col.add("ActionLevel");
		setDataAndColumnNames(new LinkedList<Tactic>(), col);
	}

	@Override
	public Object getValueAt(int rowIndex, int columnIndex) {
		if (rowIndex >= 0) {
			Tactic t = getRow(rowIndex);
			switch (columnIndex) {
			case 0:
				return t.getPriority();
			case 1:
				return t.getCondition();
			case 2:
				return t.getValue();
			case 3:
				return t.getAction();
			case 4:
				return t.getActionLevel();
			}
		}
		return null;
	}

	@Override
	public void setValueAt(Object value, int row, int column) {
		Tactic t = getRow(row);

		switch (column) {
		case 0:
			t.setPriority((Integer) value);
			break;
		case 1:
			t.setCondition((Conditions) value);
			break;
		case 2:
			t.setValue((Integer) value);
			break;
		case 3:
			t.setAction((Action) value);
			break;
		case 4:
			t.setActionLevel((Integer) value);
			break;
		}
	}

	@Override
	public boolean isCellEditable(int row, int column) {
		return true;
	}

	@Override
	public Class<?> getColumnClass(int col) {
		if (col == 0 || col == 2) {
			return Integer.class;
		} else {
			return Object.class;
		}
	}

}
