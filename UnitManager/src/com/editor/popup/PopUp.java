package com.editor.popup;

import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.LinkedList;

import javax.swing.JMenu;
import javax.swing.JMenuItem;
import javax.swing.JPopupMenu;

import Triangle.TriangleValues.TriangleValues.UnitClass;

import com.editor.global.Global;
import com.editor.tableViews.UnitTableView;
import com.editor.tools.DataDB;

@SuppressWarnings("serial")
public class PopUp extends JPopupMenu implements ActionListener {
	JMenuItem item;
	JMenu addToTeam;

	public PopUp() {
		item = new JMenuItem("New nnit");
		item.addActionListener(this);
		add(item);
		// ---------------------

		item = new JMenuItem("Remove this unit");
		item.addActionListener(this);
		if (Global.selectedUnitNumber < 0) {
			item.setEnabled(false);
		}
		add(item);
		// ---------------------

		addToTeam = new JMenu("Add this unit to team");
		if (Global.selectedUnitNumber < 0) {
			addToTeam.setEnabled(false);
		}
		add(addToTeam);
		// ---------------------

		item = new JMenuItem("Remove this unit from this team");
		item.addActionListener(this);
		if (Global.selectedUnitNumber < 0) {
			item.setEnabled(false);
		}
		add(item);

		set(Global.selectedAccountNumber, Global.selectedUnitNumber);

//		System.out.println("Popup, accountNum: " + Global.selectedAccountNumber
//				+ ", unitNum:" + Global.selectedUnitNumber);

	}

	@Override
	public void actionPerformed(ActionEvent e) {
		switch (e.getActionCommand()) {
		case "New unit":
			int unitNumber;
			try {
				unitNumber = DataDB.getUser().CreateUnit(
						Global.selectedAccountNumber, "Noname",
						UnitClass.BEGGINER.getNumber());
				if (Global.selectedTeamNumber >= 0) {
					DataDB.getUser().RegisterUnit(unitNumber,
							Global.selectedTeamNumber);
				}
				UnitTableView.getThis().notifyDataSetChanged();
			} catch (SQLException e1) {
				e1.printStackTrace();
			}

			UnitTableView.getThis().notifyDataSetChanged();
			break;
		case "Remove this unit":
			try {
				DataDB.getUser().DeleteUnit(Global.selectedUnitNumber);
				UnitTableView.getThis().notifyDataSetChanged();
			} catch (SQLException e1) {
				e1.printStackTrace();
			}
			break;
		case "Add this unit to team":
			break;
		case "Remove this unit from this team":
			try {
				DataDB.getUser().UnregisterUnit(Global.selectedUnitNumber,
						Global.selectedTeamNumber);
				UnitTableView.getThis().notifyDataSetChanged();
			} catch (SQLException e1) {
				e1.printStackTrace();
			}
			break;
		default:
			break;
		}
	}

	public void set(int accountNumber, int unitNumber) {
		ResultSet resultTeamByAccount;
		ResultSet resultTeamByUnit;
		try {
			resultTeamByAccount = DataDB.getUser().GetTeamByAccountNumber(
					accountNumber);
			resultTeamByUnit = DataDB.getUser().GetTeamByUnitNumber(
					unitNumber);
			LinkedList<Integer> inTeamList = new LinkedList<Integer>();

			while (resultTeamByUnit.next()) {
				int inTeamNumber = resultTeamByUnit.getInt("teamNumber");
				inTeamList.add(inTeamNumber);
			}

			while (resultTeamByAccount.next()) {
				int teamNumber = resultTeamByAccount.getInt("teamNumber");
				String teamName = resultTeamByAccount.getString("teamName");

				if (!inTeamList.contains(teamNumber)) {
					JMenuItem teamItem = new JMenuItem(teamNumber + " "
							+ teamName);
					teamItem.addActionListener(new ActionListener() {
						@Override
						public void actionPerformed(ActionEvent e) {
							try {
								DataDB.getUser().RegisterUnit(
										Global.selectedUnitNumber,
										Integer.parseInt(e.getActionCommand()
												.split(" ")[0]));
								UnitTableView.getThis().notifyDataSetChanged();
							} catch (NumberFormatException e1) {
								e1.printStackTrace();
							} catch (SQLException e1) {
								e1.printStackTrace();
							}
						}
					});
					addToTeam.add(teamItem);
				}
			}
			addToTeam.revalidate();
			addToTeam.repaint();

		} catch (SQLException e) {
			e.printStackTrace();
		}
	}
}