package com.editor.starter;

import java.awt.Button;
import java.awt.Dialog;
import java.awt.Event;
import java.awt.Frame;
import java.awt.GridLayout;
import java.awt.Label;
import java.awt.Panel;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.awt.event.KeyEvent;

import javax.swing.JMenu;
import javax.swing.JMenuBar;
import javax.swing.JMenuItem;
import javax.swing.KeyStroke;

import com.editor.tools.DataDB;

@SuppressWarnings("serial")
class ManagerMenuBar extends JMenuBar implements ActionListener {
	Frame parent;

	public ManagerMenuBar(Frame parent) {
		this.parent = parent;
		// addMenus(new String[] { "����", "ã��" });
		// addMenuItems(0, new String[] { "����", "���", "����" });
		// addMenuItems(1, new String[] { "����Idx�� ã��", "�����̸����� ã��", "����Idx�� ã��",
		// "�����̸����� ã��" });
		add(new JMenu("�޴�"));
		add(new JMenu("����"));

//		JMenuItem save = new JMenuItem("����");
//		save.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_S, Event.CTRL_MASK));
//		getMenu(0).add(save);


		JMenuItem quit = new JMenuItem("����");
		quit.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_Q, Event.CTRL_MASK));
		getMenu(0).add(quit);
		
		// JMenuItem undo = new JMenuItem("���� ���");
		// undo.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_Z,
		// Event.CTRL_MASK));
		// getMenu(1).add(undo);
		//
		// JMenuItem redo = new JMenuItem("�� ����");
		// redo.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_R,
		// Event.CTRL_MASK));
		// getMenu(1).add(redo);

		JMenuItem cancel = new JMenuItem("���");
		cancel.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_D, Event.CTRL_MASK));
		getMenu(1).add(cancel);

		addActionListener();
	}

	// getMenu(3).addSeparator();

	public class CloseFrameDialog extends Dialog implements ActionListener {
		Button yes = new Button("��"); 
		Button no = new Button("�ƴϿ�");
		Frame owner;
		Label msgLabel;

		public CloseFrameDialog(Frame owner, String title, String msg) {
			super(owner, title, true); 
			this.owner = owner; 
			msgLabel = new Label(msg, Label.CENTER);
			Panel p = new Panel();
			p.setLayout(new GridLayout(1, 5));

			p.add(new Label());
			p.add(yes);
			p.add(new Label());
			p.add(no);
			p.add(new Label());
			// p.add(yes2 , 2);

			add(msgLabel, "Center");
			add(p, "South");

			yes.addActionListener(this);
			no.addActionListener(this);
		}

		public void actionPerformed(ActionEvent e) {
			String command = e.getActionCommand();
			if (command.equals("��")) {
				DataDB.getUser().close();
				owner.dispose();
			} else if (command.equals("�ƴϿ�")) {
				this.dispose();
			}
		}
	}
	
	public void addActionListener() {
		for (int i = 0; i < getMenuCount(); i++)
			for (int j = 0; j < getMenu(i).getItemCount(); j++)
				getMenu(i).getItem(j).addActionListener(this);
	}

	@Override
	public void actionPerformed(ActionEvent e) {
		switch (e.getActionCommand()) {
		case "����":
			Dialog d = new CloseFrameDialog(parent, "�ݱ�", "�����ðڽ��ϱ�?");
			d.setSize(400, 100);
			d.setLocationRelativeTo(parent);
			d.setVisible(true);
			break;
		case "����Idx�� ã��":

		case "�����̸����� ã��":

		case "����Idx�� ã��":

		case "�����̸����� ã��":
			
		}
	}
}
