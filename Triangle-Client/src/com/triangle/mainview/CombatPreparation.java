package com.triangle.mainview;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

import Triangle.Protocol.GameProtocol.CombatRequest;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.Protocol.GameProtocol.GetNPCTeam;
import Triangle.Protocol.GameProtocol.GetTeamByAccountNumber;
import Triangle.Protocol.GameProtocol.ProtocolTeam;
import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;
import android.view.View;
import android.view.View.OnClickListener;
import android.view.Window;
import android.widget.AdapterView;
import android.widget.AdapterView.OnItemClickListener;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.ListView;

import com.triangle.R;
import com.triangle.common.Global;
import com.triangle.network.GameMessageReceiver;
import com.triangle.network.GameMessageSender;

public class CombatPreparation extends Activity {

	private int selectedField = -1;
	private int selectedTeam = -1;
	
	private List<Integer> enemyNumberList;
	private List<String> enemyNameList;
	
	private List<Integer> teamNumberList;
	private List<String> teamNameList;
	private Button runButton;

	@Override
	public void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);
		setContentView(R.layout.battle);

		enemyNumberList = new ArrayList<Integer>();
		enemyNameList = new ArrayList<String>();
		
		teamNumberList = new ArrayList<Integer>();
		teamNameList = new ArrayList<String>();
		
		
		getNPCTeam();
		getTeamByAccountNumber();
		
		ListView fieldListView = (ListView) findViewById(R.id.battleFieldListView);
		ArrayAdapter<String> fieldAdapter = new ArrayAdapter<String>(this,
				android.R.layout.simple_list_item_activated_1, enemyNameList);
		fieldListView.setAdapter(fieldAdapter);
		fieldListView.setOnItemClickListener(new OnItemClickListener() {
			public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
				selectedField = position;
				if (selectedTeam != -1) {
					runButton.setClickable(true);
					runButton.setEnabled(true);
				}
			}
		});

		ListView teamListView = (ListView) findViewById(R.id.battleTeamListView);
		ArrayAdapter<String> teamAdapter = new ArrayAdapter<String>(this,
				android.R.layout.simple_list_item_activated_1, teamNameList);
		teamListView.setAdapter(teamAdapter);
		teamListView.setOnItemClickListener(new OnItemClickListener() {
			public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
				selectedTeam = position;
				if (selectedField != -1) {
					runButton.setClickable(true);
					runButton.setEnabled(true);
				}
			}
		});

		runButton = (Button) findViewById(R.id.battleRunButton);
		runButton.setClickable(false);
		runButton.setEnabled(false);
		runButton.setOnClickListener(new OnClickListener() {
			public void onClick(View v) {
				if (requestBattle()) {
					setResult(RESULT_OK);
					Intent combatResult = new Intent(CombatPreparation.this, CombatResult.class);
					combatResult.putExtra("isCombatExecuted", true);
					startActivity(combatResult);
				} else {
					Log.i("com.triangle2", "failed to request battle");
				}
			}
		});
	}

	public boolean getTeamByAccountNumber() {
		try {
			if (new GameMessageSender(
					GameMessage.newBuilder().setGameMessageType(GameMessageType.GetTeamByAccountNumber).setAccountNumber(
							Global.accountNumber).setGetTeamByAccountNumber(GetTeamByAccountNumber.newBuilder()).build()).execute().get(
					1, TimeUnit.SECONDS)) {
				GameMessage recv = new GameMessageReceiver().execute().get(1,
						TimeUnit.SECONDS);
				if (recv != null && recv.getResult()) {
					for (int i = 0; i < recv.getGetTeamByAccountNumber().getTeamCount(); i++) {
						ProtocolTeam protoTeam = recv.getGetTeamByAccountNumber().getTeam(i);
						teamNameList.add(protoTeam.getTeamName());
						teamNumberList.add(protoTeam.getTeamNumber());
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

	public boolean getNPCTeam() {
		try {
			if (new GameMessageSender(
					GameMessage.newBuilder().setGameMessageType(
							GameMessageType.GetNPCTeam).setAccountNumber(Global.accountNumber).setGetNPCTeam(
							GetNPCTeam.newBuilder().setNpcNumber(0)).build()).execute().get(
					1, TimeUnit.SECONDS)) {
				GameMessage recv = new GameMessageReceiver().execute().get(1,
						TimeUnit.SECONDS);
				if (recv != null && recv.getResult()) {
					for (int i = 0; i < recv.getGetNPCTeam().getTeamCount(); i++) {
						ProtocolTeam protoTeam = recv.getGetNPCTeam().getTeam(i);
						enemyNameList.add(protoTeam.getTeamName());
						enemyNumberList.add(protoTeam.getTeamNumber());
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

	public boolean requestBattle() {
		try {
			if (new GameMessageSender(
					GameMessage.newBuilder().setGameMessageType(
							GameMessageType.CombatRequest).setAccountNumber(
							Global.accountNumber).setCombatRequest(
							CombatRequest.newBuilder().setAllyTeamNumber(
									teamNumberList.get(selectedTeam)).setEnemyTeamNumber(
									enemyNumberList.get(selectedField))).build()).execute().get(
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

	public void onBackPressed() {
		finish();
	}
}