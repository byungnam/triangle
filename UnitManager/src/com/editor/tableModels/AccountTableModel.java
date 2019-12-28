package com.editor.tableModels;

import java.util.LinkedList;


@SuppressWarnings("serial")
public class AccountTableModel extends RowTableModel<Integer> {
	public AccountTableModel() {
		super(Integer.class);
		LinkedList<String> col = new LinkedList<>();
		col.add("AccountNum");

		setDataAndColumnNames(new LinkedList<Integer>(), col);

	}

	@Override
	public Object getValueAt(int rowIndex, int columnIndex) {
		Integer accountNumber = getRow(rowIndex);
		return accountNumber;
	}

	@Override
	public void setValueAt(Object value, int row, int column) {
		
	}

	@Override
	public boolean isCellEditable(int row, int column) {
		return false;
	}

	@Override
	public Class<?> getColumnClass(int col) {
		return Integer.class;
	}

}
