package com.editor.popup;

import java.awt.event.MouseAdapter;
import java.awt.event.MouseEvent;

import javax.swing.JPopupMenu;
import javax.swing.JTable;

import com.editor.global.Global;

public class PopClickListener extends MouseAdapter {

	JTable table;

	public PopClickListener(JTable table) {
		this.table = table;
	}

	public void mousePressed(MouseEvent e) {
		return;
	}

	public void mouseReleased(MouseEvent e) {
		int r = table.rowAtPoint(e.getPoint());
		if (r >= 0 && r < table.getRowCount()) {
			table.setRowSelectionInterval(r, r);
		} else {
			table.clearSelection();
			Global.selectedUnitNumber = -1;
		}

//		int rowindex = table.getSelectedRow();
//		if (rowindex < 0){
//			return;
//		}
		if (e.isPopupTrigger() && e.getComponent() instanceof JTable) {
			JPopupMenu popup = new PopUp();
			popup.show(e.getComponent(), e.getX(), e.getY());
		}
	}
}