package com.triangle.mainview;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

import Triangle.Protocol.GameProtocol.DeleteTactic;
import Triangle.Protocol.GameProtocol.EditTactic;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.Protocol.GameProtocol.GetTactic;
import Triangle.Protocol.GameProtocol.ProtocolTactic;
import Triangle.TriangleValues.TriangleValues;
import Triangle.TriangleValues.TriangleValues.Action;
import Triangle.TriangleValues.TriangleValues.Conditions;
import android.app.Activity;
import android.app.Dialog;
import android.content.Context;
import android.graphics.Color;
import android.os.Bundle;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.View.OnClickListener;
import android.view.Window;
import android.widget.AdapterView;
import android.widget.AdapterView.OnItemClickListener;
import android.widget.ArrayAdapter;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import com.triangle.R;
import com.triangle.common.Global;
import com.triangle.network.GameMessageReceiver;
import com.triangle.network.GameMessageSender;

public class EditUnitTactic extends Activity {
	// private Button save;
	// private Button add;
	// private List<Button> option;
	// private Button cancel;

	private int unitNumber;
	private List<TextView> condition;
	private List<TextView> value;
	private List<TextView> action;

	private List<Integer> selectedCondition;
	private List<Integer> selectedValue;
	private List<Integer> selectedAction;

	// private List<Integer> _selectedCondition;
	// private List<Integer> _selectedValue;
	// private List<Integer> _selectedAction;
	//
	// private List<GameMessage> orderList;

	private LinearLayout linearLayout;

	@Override
	public void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);
		setContentView(R.layout.changetactic);

		linearLayout = (LinearLayout) findViewById(R.id.TacticLL);
		unitNumber = getIntent().getIntExtra("unitNumber", -1);
		// orderList = new ArrayList<GameMessage>();

		// add = new Button(this);
		// option = new ArrayList<Button>();
		condition = new ArrayList<TextView>();
		value = new ArrayList<TextView>();
		action = new ArrayList<TextView>();

		selectedCondition = new ArrayList<Integer>();
		selectedValue = new ArrayList<Integer>();
		selectedAction = new ArrayList<Integer>();

		// save = (Button) findViewById(R.id.save);
		// save.setOnClickListener(new View.OnClickListener() {
		// public void onClick(View v) {
		//
		// }
		// });

		// add.setText("추가");
		// add.setLayoutParams(new LayoutParams(LayoutParams.WRAP_CONTENT,
		// LayoutParams.WRAP_CONTENT));
		// add.setOnClickListener(new View.OnClickListener() {
		// public void onClick(View v) {
		// addOneTacticLine(action.size() + 1, 0, 0, 0);
		// }
		// });
		// linearLayout.addView(add);

		// cancel = new Button(this);
		// cancel.setText("취소");
		// cancel.setLayoutParams(new LayoutParams(LayoutParams.WRAP_CONTENT,
		// LayoutParams.WRAP_CONTENT));
		// cancel.setOnClickListener(new View.OnClickListener() {
		// public void onClick(View v) {
		// finish();
		// }
		// });
		// linearLayout.addView(add);

		if (!getTactics()) {
			Toast.makeText(this, "Failed to get Tactics", Toast.LENGTH_SHORT)
					.show();
		}
	}

	public void addOneTacticLine(int priority, int v_condition, int v_value,
			int v_action) {
		LinearLayout oneTacticLine = new LinearLayout(this);
		oneTacticLine.setLayoutParams(new LinearLayout.LayoutParams(
				LinearLayout.LayoutParams.MATCH_PARENT, 80));
		oneTacticLine.setGravity(Gravity.CENTER);

		selectedCondition.add(v_condition);
		selectedValue.add(v_value);
		selectedAction.add(v_action);

		// Button optionButton = new Button(this);
		TextView nth = new TextView(this);
		TextView condText = new TextView(this);
		TextView valueText = new TextView(this);
		TextView actionText = new TextView(this);

		// option.add(optionButton);
		condition.add(condText);
		value.add(valueText);
		action.add(actionText);

		// optionButton.setTextSize(20.0f);
		nth.setTextSize(20.0f);
		condText.setTextSize(20.0f);
		valueText.setTextSize(20.0f);
		actionText.setTextSize(20.0f);

		// optionButton.setLayoutParams(new LinearLayout.LayoutParams(0,
		// LinearLayout.LayoutParams.WRAP_CONTENT, 0.1f));
		nth.setLayoutParams(new LinearLayout.LayoutParams(0,
				LinearLayout.LayoutParams.MATCH_PARENT, 0.1f));
		condText.setLayoutParams(new LinearLayout.LayoutParams(0,
				LinearLayout.LayoutParams.MATCH_PARENT, 0.8f));
		valueText.setLayoutParams(new LinearLayout.LayoutParams(0,
				LinearLayout.LayoutParams.MATCH_PARENT, 0.1f));
		actionText.setLayoutParams(new LinearLayout.LayoutParams(0,
				LinearLayout.LayoutParams.MATCH_PARENT, 0.3f));

		// optionButton.setGravity(Gravity.CENTER);
		nth.setGravity(Gravity.CENTER);
		condText.setGravity(Gravity.CENTER);
		valueText.setGravity(Gravity.CENTER);
		actionText.setGravity(Gravity.CENTER);

		nth.setBackgroundColor(Color.BLUE);
		condText.setBackgroundColor(Color.BLACK);
		valueText.setBackgroundColor(Color.RED);
		actionText.setBackgroundColor(Color.GREEN);

		condText.setClickable(true);
		valueText.setClickable(true);
		actionText.setClickable(true);

		// optionButton.setOnClickListener(new
		// onClickListenerWithPriority(priority,
		// 0));
		condText.setOnClickListener(new onClickListenerWithPriority(priority, 1));
		valueText.setOnClickListener(new onClickListenerWithPriority(priority,
				2));
		actionText.setOnClickListener(new onClickListenerWithPriority(priority,
				3));

		// optionButton.setText("*");
		nth.setText("" + (priority + 1));
		condText.setText(Conditions.valueOf(v_condition).getValue());
		valueText.setText("" + v_value);
		actionText.setText(Action.valueOf(v_action).getValue());

		// oneTacticLine.addView(optionButton);
		oneTacticLine.addView(nth);
		oneTacticLine.addView(condText);
		oneTacticLine.addView(valueText);
		oneTacticLine.addView(actionText);

		linearLayout.addView(oneTacticLine);
	}

	public boolean getTactics() {
		try {
			GameMessage recv = null;
			if (new GameMessageSender(GameMessage
					.newBuilder()
					.setGameMessageType(GameMessageType.GetTactic)
					.setAccountNumber(Global.accountNumber)
					.setGetTactic(
							GetTactic.newBuilder().setUnitNumber(unitNumber))
					.build()).execute().get(1, TimeUnit.SECONDS)) {

				recv = new GameMessageReceiver().execute().get(1,
						TimeUnit.SECONDS);
			}
			if (recv != null && recv.getResult()) {
				List<ProtocolTactic> protoTacticList = recv.getGetTactic()
						.getTacticsList();
				if (protoTacticList != null) {
					for (ProtocolTactic protoTactic : protoTacticList) {
						addOneTacticLine(protoTactic.getPriority(),
								protoTactic.getCondition(),
								protoTactic.getValue(), protoTactic.getAction());
					}
				}
				return true;
			}
		} catch (InterruptedException e) {
			e.printStackTrace();
		} catch (ExecutionException e) {
			e.printStackTrace();
		} catch (TimeoutException e) {
			e.printStackTrace();
		}
		return false;
	}

	public boolean editTactic(int beforePriority, int priority, int condition,
			int value, int action) {
		ProtocolTactic.Builder tacticBuilder = ProtocolTactic.newBuilder();
		tacticBuilder.setPriority(priority).setCondition(condition)
				.setValue(value).setAction(action).setActionLevel(1);
		try {
			if (new GameMessageSender(GameMessage
					.newBuilder()
					.setGameMessageType(GameMessageType.EditTactic)
					.setAccountNumber(Global.accountNumber)
					.setEditTactic(
							EditTactic.newBuilder().setUnitNumber(unitNumber)
									.setPriority(beforePriority)
									.setTactic(tacticBuilder)).build())
					.execute().get(1, TimeUnit.SECONDS)) {
				GameMessage recv = new GameMessageReceiver().execute().get(1,
						TimeUnit.SECONDS);
				if (recv != null && recv.getResult()) {
					return true;
				}
			}
		} catch (InterruptedException e) {
			e.printStackTrace();
		} catch (ExecutionException e) {
			e.printStackTrace();
		} catch (TimeoutException e) {
			e.printStackTrace();
		}
		return false;
	}

	public boolean deleteTactic(int priority) {
		try {
			if (new GameMessageSender(GameMessage
					.newBuilder()
					.setGameMessageType(GameMessageType.DeleteTactic)
					.setAccountNumber(Global.accountNumber)
					.setDeleteTactic(
							DeleteTactic.newBuilder().setUnitNumber(unitNumber)
									.setPriority(priority)).build()).execute()
					.get(1, TimeUnit.SECONDS)) {

				GameMessage recv = new GameMessageReceiver().execute().get(1,
						TimeUnit.SECONDS);
				if (recv != null && recv.getResult()) {
					selectedCondition.remove(priority);
					selectedValue.remove(priority);
					selectedAction.remove(priority);
					return true;
				}
			}

		} catch (InterruptedException e) {
			e.printStackTrace();
		} catch (ExecutionException e) {
			e.printStackTrace();
		} catch (TimeoutException e) {
			e.printStackTrace();
		}
		return false;
	}

	class onClickListenerWithPriority implements OnClickListener {
		int priority;
		int which;

		public onClickListenerWithPriority(int priority, int which) {
			this.priority = priority;
			this.which = which;
		}

		public void onClick(View v) {
			Dialog d = null;
			// if (which > 0) {
			d = new SelectDialog(EditUnitTactic.this, priority, which);
			// } else {
			// d = new OptionDialog(ChangeUnitTactic.this, priority);
			// }
			d.show();

//			WindowManager.LayoutParams lp = new WindowManager.LayoutParams();
//		    lp.copyFrom(d.getWindow().getAttributes());
//		    lp.width = WindowManager.LayoutParams.MATCH_PARENT;
//		    lp.height = WindowManager.LayoutParams.MATCH_PARENT;
//		    d.getWindow().setAttributes(lp);
		}
	}

	// class OptionDialog extends AlertDialog implements
	// DialogInterface.OnClickListener {
	//
	// int priority;
	//
	// protected OptionDialog(Context context, int priority) {
	// super(context);
	// this.priority = priority;
	// this.setMessage("전략 " + (priority + 1) + "을 삭제하시겠습니까?");
	// this.setCancelable(false);
	// this.setButton(DialogInterface.BUTTON_POSITIVE, "예", this);
	// this.setButton(DialogInterface.BUTTON_NEGATIVE, "아니오", this);
	// }
	//
	// public void onClick(DialogInterface dialog, int which) {
	// switch (which) {
	// case DialogInterface.BUTTON_POSITIVE:
	// if (!deleteTactic(priority)) {
	// Log.i("com.triangle2", "failed to delete tactic");
	// }
	// break;
	// case DialogInterface.BUTTON_NEGATIVE:
	// break;
	// }
	// }
	// }

	class SelectDialog extends Dialog implements OnItemClickListener {

		int priority;
		int which;

		public SelectDialog(Context context, int priority, int which) {
			super(context);
			this.requestWindowFeature(Window.FEATURE_NO_TITLE);
			this.priority = priority;
			this.which = which;

			setContentView(R.layout.dialog);
			LinearLayout dialogLayout = (LinearLayout) findViewById(R.id.dialogLinearLayout);
			TextView textView = (TextView) findViewById(R.id.dialogTextView);
			textView.setTextSize(20f);
			String[] items = null;
			switch (which) {
			case 0:
				textView.setText(R.string.selectOption);
				items = new String[] { "삭제", "우선순위 변경" };
				break;
			case 1:
				textView.setText(R.string.selectCond);
				items = Conditions.toReadableArray();
				break;
			case 2:
				textView.setText(R.string.selectValue);
				items = TriangleValues.ValuesString;
				break;
			case 3:
				textView.setText(R.string.selectAction);
				items = Action.toReadableArray();
				break;
			}
			ListView listView = new ListView(context);
			ArrayAdapter<String> adapter = new ArrayAdapter<String>(context,
					android.R.layout.simple_list_item_1, items);
			listView.setAdapter(adapter);
			listView.setOnItemClickListener(this);
			dialogLayout.addView(listView);
		}

		public void onItemClick(AdapterView<?> parent, View view, int position,
				long id) {
			switch (which) {
			case 0:

				break;
			case 1:
				selectedCondition.set(priority, position);
				condition.get(priority).setText(
						Conditions.valueOf(position).getValue());
				break;
			case 2:
				selectedValue.set(priority, position);
				value.get(priority).setText(
						TriangleValues.ValuesString[position]);
				break;
			case 3:
				selectedAction.set(priority, position);
				action.get(priority).setText(
						Action.valueOf(position).getValue());
				break;
			}
			// Log.i("com.triangle2",priority + " "
			// +selectedCondition.get(priority) + " " +
			// selectedAction.get(priority));
			if (!editTactic(priority, priority,
					selectedCondition.get(priority),
					selectedValue.get(priority), selectedAction.get(priority))) {
				Log.i("com.triangle2", "failed to edit tactic");
			}
			this.dismiss();
		}
	}
}
