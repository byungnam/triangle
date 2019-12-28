package com.editor.starter;

import java.awt.Button;
import java.awt.Dialog;
import java.awt.Frame;
import java.awt.GridLayout;
import java.awt.Label;
import java.awt.Panel;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.awt.event.WindowAdapter;
import java.awt.event.WindowEvent;
import java.awt.event.WindowListener;

import javax.swing.JFrame;

import com.editor.tools.DataDB;

@SuppressWarnings("serial")
public class MainFrame extends JFrame {

	Frame me;

	public MainFrame(String title) { // ������
		setDefaultCloseOperation(DO_NOTHING_ON_CLOSE);

		setTitle(title);
		this.me = this;
		WindowListener winHandler = new WindowAdapter() {
			public void windowClosing(WindowEvent e) {
				CloseFrameDialog d = new CloseFrameDialog(MainFrame.this, "�ݱ�",
						"�����ðڽ��ϱ�?");
				d.setSize(400, 100);
				d.setLocationRelativeTo(me);
				d.setVisible(true);
			}
		};

		addWindowListener(winHandler);

	}

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
}