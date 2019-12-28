package com.editor.tableViews;

import java.awt.BorderLayout;
import java.awt.Color;
import java.awt.Dimension;
import java.awt.event.ActionListener;

import javax.swing.DefaultCellEditor;
import javax.swing.JButton;
import javax.swing.JLabel;
import javax.swing.JPanel;
import javax.swing.JScrollPane;
import javax.swing.JTable;
import javax.swing.ListSelectionModel;
import javax.swing.event.ListSelectionEvent;
import javax.swing.event.ListSelectionListener;
import javax.swing.table.TableColumn;

public abstract class TableView implements ListSelectionListener,
		ActionListener {

	public JTable table;
	JPanel p;
	JScrollPane scrollp;

	JPanel parent;
	String title;

	private JButton addButton;
	private JButton delButton;

	private int selectedIdOfParent = -1;

	public TableView(JPanel parent, String title) {
		this.parent = parent;
		this.title = title;
		p = new JPanel();
		p.setPreferredSize(new Dimension((int) parent.getPreferredSize()
				.getWidth(), 40));
		parent.setLayout(new BorderLayout());
		parent.add(p, "North");

		parent.setBackground(new Color(255, 0, 0));

		p.add(new JLabel(title), "West");
		delButton = new JButton("삭제");
		addButton = new JButton("추가");
		p.add(delButton, "East");
		p.add(addButton, "East");
		addButton.addActionListener(this);
		delButton.addActionListener(this);

		table = new JTable();
		table.setSelectionMode(ListSelectionModel.SINGLE_SELECTION);
		table.setAutoCreateRowSorter(false);
		table.getSelectionModel().addListSelectionListener(this);
		table.setPreferredScrollableViewportSize(table.getPreferredSize());
		table.setFillsViewportHeight(true);
		table.getTableHeader().setReorderingAllowed(false);

		scrollp = new JScrollPane(table);
		// System.out.println(parent.getPreferredSize().getHeight());

		parent.add(scrollp, "Center");
	}

	public void setColumnWidth(int colNum, int width) {
		TableColumn column;
		column = table.getColumnModel().getColumn(colNum);
		column.setMinWidth(width);
		column.setPreferredWidth(width);
	}

	public void setTitle(String title) {
		this.title = title;
		p.add(new JLabel(title), "West");
	}

	public void setParent(JPanel parent) {
		this.parent = parent;
		p.setPreferredSize(new Dimension((int) parent.getPreferredSize()
				.getWidth(), 40));
		parent.setLayout(new BorderLayout());
		parent.add(p, "North");

		scrollp.setPreferredSize(new Dimension((int) parent.getPreferredSize()
				.getWidth(), (int) parent.getPreferredSize().getHeight() - 40));
		parent.add(scrollp, "Center");

	}

	public abstract void clearTable();

	public void setColumnEditor(int colNum, DefaultCellEditor editor) {
		table.getColumnModel().getColumn(colNum).setCellEditor(editor);
	}

	public void setSelectedIdOfParent(int id) {
		selectedIdOfParent = id;
	}

	public int getSelectedIdOfParent() {
		return selectedIdOfParent;
	}

	@Override
	public void valueChanged(ListSelectionEvent e) {
		if (table.isEditing()) {
			table.getCellEditor().stopCellEditing();
		}
	}
}