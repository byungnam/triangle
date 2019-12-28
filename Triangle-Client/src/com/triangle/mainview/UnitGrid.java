package com.triangle.mainview;

import java.io.IOException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

import Triangle.Protocol.GameProtocol.CreateTeam;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.Protocol.GameProtocol.GetTeamByAccountNumber;
import Triangle.Protocol.GameProtocol.GetUnitByAccountNumber;
import Triangle.Protocol.GameProtocol.GetUnitByTeamNumber;
import Triangle.Protocol.GameProtocol.ProtocolTeam;
import Triangle.Protocol.GameProtocol.ProtocolUnit;
import Triangle.Protocol.GameProtocol.RegisterUnit;
import Triangle.Protocol.GameProtocol.UnregisterUnit;
import android.app.Activity;
import android.app.AlertDialog;
import android.app.Dialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.graphics.PorterDuff;
import android.os.Bundle;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.View;
import android.view.View.OnClickListener;
import android.view.ViewGroup;
import android.view.ViewGroup.LayoutParams;
import android.view.Window;
import android.widget.AdapterView;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.GridView;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;

import com.triangle.R;
import com.triangle.common.Global;
import com.triangle.network.GameMessageReceiver;
import com.triangle.network.GameMessageSender;

public class UnitGrid extends Activity {

	private int currentState;
	private int currentTeamNumber;

	private static final int NORMAL = 0;
	private static final int TEAM_MANAGE = 1;

	private final int request_newUnit = 0;
	private final int request_unitinfo = 1;
	private final int request_battle = 2;

	private ListView teamListView;
	private ListAdapter teamListAdapter;

	private GridView unitGridView;
	private BaseAdapter unitGridAdapter;

	private List<ProtocolTeam> teamList;
	// private List<String> teamNameList;
	private List<ProtocolUnit> unitList;
	private Set<ProtocolUnit> teamUnitSet;
	private Set<ProtocolUnit> beforeTeamUnitSet;

	private Button battleButton;
	private Button newTeamButton;
	private Button teamManageButton;
	private Button newUnitButton;

	public void onCreate(Bundle savedInstanceState) {

		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);

		setContentView(R.layout.gridview);

		currentState = NORMAL;

		teamListView = (ListView) findViewById(R.id.teamListView);
		unitGridView = (GridView) findViewById(R.id.unitGridView);

		teamList = new ArrayList<ProtocolTeam>();
		// teamNameList = new ArrayList<String>();
		unitList = new ArrayList<ProtocolUnit>();
		teamUnitSet = new HashSet<ProtocolUnit>();
		beforeTeamUnitSet = new HashSet<ProtocolUnit>();

		getTeamByAccountNumber();
		getUnitByAccountNumber();

		teamListAdapter = new ListAdapter(this);
		teamListView.setAdapter(teamListAdapter);

		unitGridAdapter = new GridAdapter(this);
		unitGridView.setAdapter(unitGridAdapter);

		teamListView.setOnItemClickListener(new AdapterView.OnItemClickListener() {
			public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
				view.setSelected(true);
				getUnitByTeamNumber(teamList.get(position).getTeamNumber());
				unitGridAdapter.notifyDataSetChanged();
				currentTeamNumber = teamList.get(position).getTeamNumber();
				if (position == 0) {
					teamManageButton.setClickable(false);
					teamManageButton.setEnabled(false);
				} else {
					teamManageButton.setClickable(true);
					teamManageButton.setEnabled(true);
				}

				teamListAdapter.notifyDataSetChanged();
			}
		});

		unitGridView.setOnItemClickListener(new AdapterView.OnItemClickListener() {
			public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
				switch (currentState) {
				case NORMAL:
					Intent intentUnitInfo = new Intent(UnitGrid.this, UnitInfo.class);
					intentUnitInfo.putExtra("unitNumber",
							unitList.get(position).getUnitNumber());
					startActivityForResult(intentUnitInfo, request_unitinfo);
					break;
				case TEAM_MANAGE:
					if (teamUnitSet.contains(unitList.get(position))) {
						teamUnitSet.remove(unitList.get(position));
					} else {
						teamUnitSet.add(unitList.get(position));
					}
					unitGridAdapter.notifyDataSetChanged();
					break;
				}
			}
		});

		battleButton = (Button) findViewById(R.id.battlebutton);
		battleButton.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				Intent intentBattle = new Intent(UnitGrid.this, CombatPreparation.class);
				startActivityForResult(intentBattle, request_battle);
			}
		});

		newTeamButton = (Button) findViewById(R.id.createTeamButton);
		newTeamButton.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				Dialog d = new CreateTeamDialog(UnitGrid.this);
				d.show();
			}
		});

		teamManageButton = (Button) findViewById(R.id.manageTeamButton);
		teamManageButton.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				switch (currentState) {
				case NORMAL:
					currentState = TEAM_MANAGE;

					teamListView.setEnabled(false);
					teamListView.setClickable(false);

					battleButton.setEnabled(false);
					battleButton.setClickable(false);

					newTeamButton.setEnabled(false);
					newTeamButton.setClickable(false);

					newUnitButton.setEnabled(false);
					newUnitButton.setClickable(false);

					beforeTeamUnitSet.clear();
					beforeTeamUnitSet.addAll(teamUnitSet);

					break;
				case TEAM_MANAGE:
					currentState = NORMAL;
					teamListView.setEnabled(true);
					teamListView.setClickable(true);

					battleButton.setEnabled(true);
					battleButton.setClickable(true);

					newTeamButton.setEnabled(true);
					newTeamButton.setClickable(true);

					newUnitButton.setEnabled(true);
					newUnitButton.setClickable(true);

					if (!manageTeam()) {
						Log.i("com.triangle2", "failed to register/unregister units");
					}

					break;
				}
			}
		});
		teamManageButton.setClickable(false);
		teamManageButton.setEnabled(false);

		newUnitButton = (Button) findViewById(R.id.newbutton);
		newUnitButton.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				Intent intentNewUnit = new Intent(UnitGrid.this, CreateNewUnit.class);
				startActivityForResult(intentNewUnit, request_newUnit);
			}
		});
	}

	class CreateTeamDialog extends Dialog implements OnClickListener {
		EditText editText;
		Button confirmButton;

		public CreateTeamDialog(Context context) {
			super(context);
			this.requestWindowFeature(Window.FEATURE_NO_TITLE);

			setContentView(R.layout.dialog);
			LinearLayout dialogLayout = (LinearLayout) findViewById(R.id.dialogLinearLayout);

			TextView textView = (TextView) findViewById(R.id.dialogTextView);
			textView.setTextSize(20f);
			textView.setText(R.string.createTeamDialog);

			editText = new EditText(context);
			editText.setTextSize(20f);
			editText.setLayoutParams(new LayoutParams(300, LayoutParams.WRAP_CONTENT));
			dialogLayout.addView(editText);

			confirmButton = new Button(context);
			confirmButton.setText(R.string.confirm);
			confirmButton.setTextSize(20f);
			confirmButton.setLayoutParams(new LayoutParams(100,
					LayoutParams.WRAP_CONTENT));
			confirmButton.setOnClickListener(this);
			dialogLayout.addView(confirmButton);
		}

		public void onClick(View v) {
			if (!createTeam(editText.getText().toString())) {
				Log.i("com.triangle2", "failed to create team");
			}
			getTeamByAccountNumber();
			teamListAdapter.notifyDataSetChanged();
			dismiss();
		}
	}

	public class GridAdapter extends BaseAdapter {
		LayoutInflater inflater;

		public GridAdapter(Context c) {
			inflater = ((Activity) c).getLayoutInflater();
		}

		public int getCount() {
			return unitList.size();
		}

		public Object getItem(int position) {
			return unitList.get(position);
		}

		public long getItemId(int position) {
			return position;
		}

		public View getView(int position, View convertView, ViewGroup parent) {
			View item = convertView;
			ImageView image;
			TextView text;
			if (item == null) {
				item = inflater.inflate(R.layout.griditem, parent, false);
			}
			ProtocolUnit unit = unitList.get(position);
			image = (ImageView) item.findViewById(R.id.gridImage);
			text = (TextView) item.findViewById(R.id.gridText);

			image.setImageResource(R.drawable.warrior);
			text.setText(unit.getUnitName());

			if (!teamUnitSet.contains(unitList.get(position))) {
				image.setColorFilter(0x60FFFFFF, PorterDuff.Mode.MULTIPLY);
			} else {
				image.clearColorFilter();
			}

			return item;
		}
	}

	public class ListAdapter extends BaseAdapter {
		Context mContext;

		public ListAdapter(Context c) {
			mContext = c;
		}

		public int getCount() {
			return teamList.size();
		}

		public Object getItem(int position) {
			return null;
		}

		public long getItemId(int position) {
			return position;
		}

		public View getView(int position, View convertView, ViewGroup parent) {
			TextView text = (TextView) convertView;
			if (text == null) {
				text = new TextView(mContext);
			}
			text.setTextSize(25f);
			ProtocolTeam team = teamList.get(position);
			text.setText(team.getTeamName());

			if (text.isSelected()) {
				text.setTextColor(getResources().getColor(R.color.selected_color));
			} else {
				text.setTextColor(getResources().getColor(R.color.normal_color));
			}

			return text;
		}
	}

	public boolean createTeam(String teamName) {
		try {
			if (new GameMessageSender(
					GameMessage.newBuilder().setGameMessageType(
							GameMessageType.CreateTeam).setAccountNumber(Global.accountNumber).setCreateTeam(
							CreateTeam.newBuilder().setName(teamName)).build()).execute().get(
					1, TimeUnit.SECONDS)) {
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

	public boolean getTeamByAccountNumber() {
		try {
			teamList.clear();
			teamList.add(ProtocolTeam.newBuilder().setTeamName("전체").setTeamNumber(-1).build());
			if (new GameMessageSender(
					GameMessage.newBuilder().setGameMessageType(GameMessageType.GetTeamByAccountNumber).setAccountNumber(
							Global.accountNumber).setGetTeamByAccountNumber(GetTeamByAccountNumber.newBuilder()).build()).execute().get(
					1, TimeUnit.SECONDS)) {
				GameMessage recv = new GameMessageReceiver().execute().get(1,
						TimeUnit.SECONDS);
				if (recv != null && recv.getResult()) {
					for (int i = 0; i < recv.getGetTeamByAccountNumber().getTeamCount(); i++) {
						ProtocolTeam protoTeam = recv.getGetTeamByAccountNumber().getTeam(i);
						teamList.add(protoTeam);
					}
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

	public boolean getUnitByTeamNumber(int teamNumber) {
		if (teamNumber < 0) {
			teamUnitSet.clear();
			teamUnitSet.addAll(unitList);
			return true;
		}
		try {
			// unitList.clear();
			teamUnitSet.clear();
			if (new GameMessageSender(GameMessage.newBuilder().setGameMessageType(
					GameMessageType.GetUnitByTeamNumber).setAccountNumber(
					Global.accountNumber).setGetUnitByTeamNumber(
					GetUnitByTeamNumber.newBuilder().setTeamNumber(teamNumber)).build()).execute().get(
					1, TimeUnit.SECONDS)) {
				GameMessage recv = new GameMessageReceiver().execute().get(1,
						TimeUnit.SECONDS);
				if (recv != null && recv.getResult()) {
					for (int i = 0; i < recv.getGetUnitByTeamNumber().getUnitCount(); i++) {
						ProtocolUnit protoUnit = recv.getGetUnitByTeamNumber().getUnit(i);
						teamUnitSet.add(protoUnit);
						// unitList.add(protoUnit);
						// unitNumberList.add(protoUnit.getUnitNumber());
					}
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

	public boolean getUnitByAccountNumber() {
		try {
			unitList.clear();
			// unitNumberList.clear();
			if (new GameMessageSender(GameMessage.newBuilder().setGameMessageType(
					GameMessageType.GetUnitByAccountNumber).setAccountNumber(
					Global.accountNumber).setGetUnitByAccountNumber(
					GetUnitByAccountNumber.newBuilder()).build()).execute().get(1,
					TimeUnit.SECONDS)) {
				GameMessage recv = new GameMessageReceiver().execute().get(3,
						TimeUnit.SECONDS);
				if (recv != null && recv.getResult()) {

					for (int i = 0; i < recv.getGetUnitByAccountNumber().getUnitCount(); i++) {
						ProtocolUnit protoUnit = recv.getGetUnitByAccountNumber().getUnit(i);
						unitList.add(protoUnit);
						teamUnitSet.add(protoUnit);
					}
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

	public boolean manageTeam() {
		try {
			for (ProtocolUnit newTeamUnit : teamUnitSet) {
				Log.i("com.triangle2", "current unit : " + newTeamUnit.getUnitNumber()
						+ " " + newTeamUnit.getUnitName());
				if (!beforeTeamUnitSet.remove(newTeamUnit)) {
					Log.i("com.triangle2", "reg : " + newTeamUnit.getUnitNumber() + " "
							+ newTeamUnit.getUnitName());
					if (new GameMessageSender(
							GameMessage.newBuilder().setGameMessageType(
									GameMessageType.RegisterUnit).setAccountNumber(
									Global.accountNumber).setRegisterUnit(
									RegisterUnit.newBuilder().setUnitNumber(
											newTeamUnit.getUnitNumber()).setTeamNumber(
											currentTeamNumber)).build()).execute().get(1,
							TimeUnit.SECONDS)) {
						GameMessage recv = new GameMessageReceiver().execute().get(1,
								TimeUnit.SECONDS);
						if (recv == null || !recv.getResult()) {
							return false;
						}
					}
				}
			}

			for (ProtocolUnit unregisteredUnit : beforeTeamUnitSet) {
				if (new GameMessageSender(GameMessage.newBuilder().setGameMessageType(
						GameMessageType.UnregisterUnit).setAccountNumber(
						Global.accountNumber).setUnregisterUnit(
						UnregisterUnit.newBuilder().setUnitNumber(
								unregisteredUnit.getUnitNumber()).setTeamNumber(
								currentTeamNumber)).build()).execute().get(1, TimeUnit.SECONDS)) {
					GameMessage recv = new GameMessageReceiver().execute().get(1,
							TimeUnit.SECONDS);
					if (recv == null || !recv.getResult()) {
						return false;
					}
				}
			}
			return true;
		} catch (InterruptedException e) {
			e.printStackTrace();
		} catch (ExecutionException e) {
			e.printStackTrace();
		} catch (TimeoutException e) {
			e.printStackTrace();
		}
		return false;
	}

	public void onActivityResult(int requestCode, int resultCode, Intent intent) {
		super.onActivityResult(requestCode, resultCode, intent);
		switch (requestCode) {
		case request_newUnit:
		case request_unitinfo:
		case request_battle:
			if (resultCode == RESULT_OK) {
				getUnitByAccountNumber();
				teamListView.clearChoices();
				unitGridAdapter.notifyDataSetChanged();
			}
			break;
		}
	}

	public void onBackPressed() {
		new AlertDialog.Builder(UnitGrid.this).setTitle("게임 종료").setMessage(
				"정말 종료하시겠습니까?").setPositiveButton("확인",
				new DialogInterface.OnClickListener() {
					public void onClick(DialogInterface dialog, int which) {
						try {
							Global.game_is.close();
							Global.game_os.close();
							Global.game_socket.close();
						} catch (IOException e) {
							e.printStackTrace();
						}
						finish();
					}
				}).setNegativeButton("취소", new DialogInterface.OnClickListener() {
			public void onClick(DialogInterface dialog, int which) {
			}
		}).show();
	}
}