package com.editor.cellEditors;

import java.util.Set;

import javax.swing.DefaultCellEditor;
import javax.swing.InputVerifier;
import javax.swing.JComponent;
import javax.swing.JOptionPane;
import javax.swing.JTextField;

@SuppressWarnings("serial")
public class IdxEditor extends DefaultCellEditor {
	InputVerifier iv;
	Set<Integer> idxSet;
	
	public IdxEditor(Set<Integer> idxSet) {
		super(new JTextField());
		this.idxSet = idxSet;
		setup();
	}

	public void setup() {
		iv = new InputVerifier() {
			@Override
			public boolean verify(JComponent input) {
				JTextField field = (JTextField) input;
				int value;
				try {
					int before = Integer.parseInt((String) getCellEditorValue());
					value = Integer.parseInt(field.getText());
					if (value != before && idxSet.contains(value)) {
						return false;
					}
				} catch (NumberFormatException e) {
					return false;
				}
				return true;
			}

			@Override
			public boolean shouldYieldFocus(JComponent input) {
				boolean valid = verify(input);
				if (!valid) {
					JOptionPane.showMessageDialog(null, "셀의 값이 숫자가 아니거나 다른 셀과 중복됩니다.");
				}
				return valid;
			}
		};
		((JTextField) getComponent()).setInputVerifier(iv);
	}

	@Override
	public boolean stopCellEditing() {
		if (!iv.shouldYieldFocus(getComponent()))
			return false;
		return super.stopCellEditing();
	}

	@Override
	public JTextField getComponent() {
		return (JTextField) super.getComponent();
	}

}
