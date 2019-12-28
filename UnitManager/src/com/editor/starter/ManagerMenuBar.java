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
		// addMenus(new String[] { "파일", "찾기" });
		// addMenuItems(0, new String[] { "저장", "취소", "종료" });
		// addMenuItems(1, new String[] { "유저Idx로 찾기", "유저이름으로 찾기", "유닛Idx로 찾기",
		// "유닛이름으로 찾기" });
		add(new JMenu("메뉴"));
		add(new JMenu("편집"));

//		JMenuItem save = new JMenuItem("저장");
//		save.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_S, Event.CTRL_MASK));
//		getMenu(0).add(save);


		JMenuItem quit = new JMenuItem("종료");
		quit.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_Q, Event.CTRL_MASK));
		getMenu(0).add(quit);
		
		// JMenuItem undo = new JMenuItem("실행 취소");
		// undo.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_Z,
		// Event.CTRL_MASK));
		// getMenu(1).add(undo);
		//
		// JMenuItem redo = new JMenuItem("재 실행");
		// redo.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_R,
		// Event.CTRL_MASK));
		// getMenu(1).add(redo);

		JMenuItem cancel = new JMenuItem("취소");
		cancel.setAccelerator(KeyStroke.getKeyStroke(KeyEvent.VK_D, Event.CTRL_MASK));
		getMenu(1).add(cancel);

		addActionListener();
	}

	// getMenu(3).addSeparator();

	public class CloseFrameDialog extends Dialog implements ActionListener {
		Button yes = new Button("예"); 
		Button no = new Button("아니오");
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
			if (command.equals("예")) {
				DataDB.getUser().close();
				owner.dispose();
			} else if (command.equals("아니오")) {
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
		case "종료":
			Dialog d = new CloseFrameDialog(parent, "닫기", "닫으시겠습니까?");
			d.setSize(400, 100);
			d.setLocationRelativeTo(parent);
			d.setVisible(true);
			break;
		case "유저Idx로 찾기":

		case "유저이름으로 찾기":

		case "유닛Idx로 찾기":

		case "유닛이름으로 찾기":
			
		}
	}
}
