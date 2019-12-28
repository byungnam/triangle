package com.editor.cellEditors;

import javax.swing.DefaultCellEditor;
import javax.swing.InputVerifier;
import javax.swing.JComponent;
import javax.swing.JOptionPane;
import javax.swing.JTextField;

@SuppressWarnings("serial")
public class DigitEditor extends DefaultCellEditor {
	InputVerifier iv;
	int min;
	int max;

	public DigitEditor(int min, int max) {
		super(new JTextField(0));
		this.min = min;
		this.max = max;
		setup();
	}

	public DigitEditor(Integer value, int min, int max) {
		super(new JTextField(value));
		this.min = min;
		this.max = max;
		setup();
	}

	public void setup() {
		iv = new InputVerifier() {
			@Override
			public boolean verify(JComponent input) {
				JTextField field = (JTextField) input;
				try {
					int value = Integer.parseInt(field.getText());
					if (value > max || value < min) {
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
					JOptionPane.showMessageDialog(null, "Min :" + min + " / Max : " + max);
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