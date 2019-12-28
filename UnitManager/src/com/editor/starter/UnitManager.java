package com.editor.starter;

import java.awt.Dimension;
import java.awt.GridBagConstraints;
import java.awt.GridBagLayout;

import javax.swing.JFrame;
import javax.swing.JPanel;

import com.editor.tableViews.AccountTableView;
import com.editor.tableViews.NPCAccountTableView;
import com.editor.tableViews.TacticTableView;
import com.editor.tableViews.TeamTableView;
import com.editor.tableViews.UnitTableView;
import com.editor.tools.DataDB;

public class UnitManager {

	public UnitManager() {
		DataDB.getUser().open();
		DataDB.getNPC().open();

		JFrame f = new MainFrame("Data Manager");
		GridBagLayout layout = new GridBagLayout();

		f.setLayout(layout);
		f.setPreferredSize(new Dimension(1200, 600));

		f.setJMenuBar(new ManagerMenuBar(f));

		// NPC Account panel
		GridBagConstraints cNPCAccount = new GridBagConstraints();
		cNPCAccount.fill = GridBagConstraints.BOTH;
		cNPCAccount.gridx = 0;
		cNPCAccount.gridy = 0;
		cNPCAccount.weightx = 1;
		cNPCAccount.weighty = 1;

		JPanel pNPCAccount = new JPanel();
		NPCAccountTableView.newInstance(pNPCAccount, "NPC Account");

		f.add(pNPCAccount, cNPCAccount);

		// User Account panel
		GridBagConstraints cAccount = new GridBagConstraints();
		cAccount.fill = GridBagConstraints.BOTH;
		cAccount.gridx = 0;
		cAccount.gridy = 1;
		cAccount.weightx = 1;
		cAccount.weighty = 1;

		JPanel pAccount = new JPanel();
		AccountTableView.newInstance(pAccount, "User Account");

		f.add(pAccount, cAccount);

		// Team panel
		GridBagConstraints cTeamName = new GridBagConstraints();
		cTeamName.fill = GridBagConstraints.BOTH;
		cTeamName.gridx = 1;
		cTeamName.gridy = 0;
		cTeamName.weightx = 1;
		cTeamName.weighty = 1;
		cTeamName.gridheight = 2;

		JPanel pNPCTeam = new JPanel();
		TeamTableView.newInstance(pNPCTeam, "NPC Team");

		f.add(pNPCTeam, cTeamName);

		// Unit panel
		GridBagConstraints cUnitList = new GridBagConstraints();
		cUnitList.fill = GridBagConstraints.BOTH;
		cUnitList.gridx = 2;
		cUnitList.gridy = 0;
		cUnitList.weightx = 3;
		cUnitList.weighty = 1;

		JPanel pUnitList = new JPanel();
		UnitTableView.newInstance(pUnitList, "Unit List");

		f.add(pUnitList, cUnitList);

		// Tactic panel
		GridBagConstraints cUnitTactic = new GridBagConstraints();
		cUnitTactic.fill = GridBagConstraints.BOTH;
		cUnitTactic.gridx = 2;
		cUnitTactic.gridy = 1;
		cUnitTactic.weightx = 3;
		cUnitTactic.weighty = 1;

		JPanel pUnitTactic = new JPanel();
		TacticTableView.newInstance(pUnitTactic, "Unit Tactic");
		f.add(pUnitTactic, cUnitTactic);

		f.pack();
		f.setVisible(true);
		f.setLocationRelativeTo(null);

		AccountTableView.getThis().notifyDataSetChanged();
		NPCAccountTableView.getThis().notifyDataSetChanged();

	}

	public static void main(String[] args) {
		new UnitManager();
	}

}
