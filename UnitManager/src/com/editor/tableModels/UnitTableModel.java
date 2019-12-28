package com.editor.tableModels;

import java.util.LinkedList;

import Triangle.TriangleValues.TriangleValues;
import Triangle.TriangleValues.TriangleValues.UnitClass;
import Triangle.Unit.UnitBase;

@SuppressWarnings("serial")
public class UnitTableModel extends RowTableModel<UnitBase> {
	public UnitTableModel() {
		super(UnitBase.class);
		LinkedList<String> col = new LinkedList<>();

		col.add("Num");
		col.add("Name");
		col.add("Class");
		col.add("Level");
		col.add("Str");
		col.add("Dex");
		col.add("Vital");
		col.add("Intel");
		col.add("Speed");
		col.add("Exp");
		setDataAndColumnNames(new LinkedList<UnitBase>(), col);
	}

	@Override
	public Object getValueAt(int rowIndex, int columnIndex) {
		if (rowIndex >= 0) {
			UnitBase u = getRow(rowIndex);
			switch (columnIndex) {
			case 0:
				return u.getUnitNumber();
			case 1:
				return u.getUnitName();
			case 2:
				return u.getDescriptor().getCls();
			case 3:
				return u.getDescriptor().getLevel();
			case 4:
				return u.getSpec().getStr();
			case 5:
				return u.getSpec().getDex();
			case 6:
				return u.getSpec().getVital();
			case 7:
				return u.getSpec().getIntel();
			case 8:
				return u.getSpec().getSpeed();
			case 9:
				return u.getDescriptor().getExp();
			}
		}
		return null;
	}

	@Override
	public void setValueAt(Object value, int row, int column) {
		UnitBase u = getRow(row);

		switch (column) {
		case 1:
			u.setUnitName((String) value);
			break;
		case 2:
			u.getDescriptor().setCls((UnitClass) value);
			break;
		case 3:
			u.getDescriptor().setLevel((Integer) value);
			break;
		case 4:
			u.getSpec().setStr((Integer) value);
			break;
		case 5:
			u.getSpec().setDex((Integer) value);
			break;
		case 6:
			u.getSpec().setVital((Integer) value);
			break;
		case 7:
			u.getSpec().setIntel((Integer) value);
			break;
		case 8:
			u.getSpec().setSpeed((Integer) value);
			break;
		case 9:
			u.getDescriptor().setExp((Integer) value);
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
		if (col == 1) {
			return String.class;
		} else if (col == 2) {
			return TriangleValues.UnitClass.class;
		} else {
			return Integer.class;
		}
	}

}
