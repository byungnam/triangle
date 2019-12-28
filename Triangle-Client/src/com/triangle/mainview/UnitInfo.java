package com.triangle.mainview;

import java.io.IOException;
import java.util.concurrent.ExecutionException;

import Triangle.Protocol.GameProtocol.DeleteUnit;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.Protocol.GameProtocol.GetUnitByUnitNumber;
import Triangle.Protocol.GameProtocol.ProtocolUnit;
import Triangle.TriangleValues.TriangleValues.UnitClass;
import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.os.AsyncTask;
import android.os.Bundle;
import android.view.View;
import android.view.Window;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

import com.triangle.R;
import com.triangle.common.Global;

public class UnitInfo extends Activity {

	private int str;
	private int dex;
	private int vital;
	private int intel;
	private int cls;

	private final int MAXIMUM_TACTICNUM = 10;
	private final int requestChangeStat = 0;

	private TextView tv_str, tv_dex, tv_vital, tv_intel, tv_speed, tv_name,
			tv_cls, tv_level, tv_exp;
	private TextView patk, pdef, matk, mdef, tv_hp, tv_mp;

	private int unitNumber;

	@Override
	public void onCreate(Bundle savedInstanceState) {

		Button changeStat, changeTactic, delete;

		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);
		setContentView(R.layout.unitinfo);

		// _DB = UConsts.Triangle.getDB();

		tv_name = (TextView) findViewById(R.id.name);
		tv_cls = (TextView) findViewById(R.id.Class);
		tv_level = (TextView) findViewById(R.id.level);
		tv_exp = (TextView) findViewById(R.id.exp);
		tv_str = (TextView) findViewById(R.id.unitinfo_str);
		tv_dex = (TextView) findViewById(R.id.unitinfo_dex);
		tv_vital = (TextView) findViewById(R.id.unitinfo_vit);
		tv_intel = (TextView) findViewById(R.id.unitinfo_int);
		tv_speed = (TextView) findViewById(R.id.unitinfo_Spd);
		patk = (TextView) findViewById(R.id.unitinfo_pAtk);
		pdef = (TextView) findViewById(R.id.unitinfo_pDef);
		matk = (TextView) findViewById(R.id.unitinfo_mAtk);
		mdef = (TextView) findViewById(R.id.unitinfo_mDef);
		tv_hp = (TextView) findViewById(R.id.unitinfo_HP);
		tv_mp = (TextView) findViewById(R.id.unitinfo_MP);

		unitNumber = getIntent().getIntExtra("unitNumber", -1);

		showData();

		changeStat = (Button) findViewById(R.id.changeStat);
		changeStat.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				Intent intentChangeUnitStat = new Intent(UnitInfo.this,
						EditUnitStat.class);
				intentChangeUnitStat.putExtra("unitNumber", unitNumber);
				startActivityForResult(intentChangeUnitStat, requestChangeStat);
			}
		});

		changeTactic = (Button) findViewById(R.id.changeTactic);
		changeTactic.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				Intent intentUnitTactic = new Intent(UnitInfo.this,
						EditUnitTactic.class);
				intentUnitTactic.putExtra("cls", cls);
				intentUnitTactic.putExtra("unitNumber", unitNumber);
				if (MAXIMUM_TACTICNUM > 3 + (intel / 15)) {
					intentUnitTactic.putExtra("tacticnum", 3 + (intel / 15));
				} else {
					intentUnitTactic.putExtra("tacticnum", MAXIMUM_TACTICNUM);
				}
				startActivity(intentUnitTactic);
			}
		});

		delete = (Button) findViewById(R.id.delete);
		delete.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				new AlertDialog.Builder(UnitInfo.this).setTitle("삭제").setMessage(
						"정말 삭제하시겠습니까?").setPositiveButton("확인",
						new DialogInterface.OnClickListener() {
							public void onClick(DialogInterface arg0, int arg1) {
								try {
									if (new deleteUnit().execute().get()) {
										setResult(RESULT_OK);
										finish();
									} else {
										Toast.makeText(getBaseContext(), "Failed to delete",
												Toast.LENGTH_SHORT).show();
									}
								} catch (InterruptedException e) {
									e.printStackTrace();
								} catch (ExecutionException e) {
									e.printStackTrace();
								}
							}
						}).setNegativeButton("취소", null).show();
			}
		});
	}
	
	public void showData(){
		ProtocolUnit protoUnit = null;
		try {
			protoUnit = new getData().execute().get();
		} catch (InterruptedException e) {
			e.printStackTrace();
		} catch (ExecutionException e) {
			e.printStackTrace();
		}
		
		if(protoUnit == null){
			Toast.makeText(this, "Unable to get Data", Toast.LENGTH_SHORT).show();
			return;
		}
		tv_name.setText(protoUnit.getUnitName());
		tv_level.setText("" + protoUnit.getLevel());
		tv_cls.setText(UnitClass.valueOf(protoUnit.getCls()).name());
		
		tv_str.setText("" + protoUnit.getStr());
		tv_dex.setText("" + protoUnit.getDex());
		tv_vital.setText("" + protoUnit.getVital());
		tv_intel.setText("" + protoUnit.getIntel());
		tv_speed.setText("" + protoUnit.getSpeed());
		tv_exp.setText("" + protoUnit.getExp());
	}
	
	public class deleteUnit extends AsyncTask<Void, Void, Boolean> {

		@Override
		protected Boolean doInBackground(Void... params) {
			try {
				GameMessage.newBuilder().setGameMessageType(GameMessageType.DeleteUnit).setAccountNumber(
						Global.accountNumber).setDeleteUnit(
						DeleteUnit.newBuilder().setUnitNumber(unitNumber)).build().writeDelimitedTo(
						Global.game_os);
				GameMessage recv = GameMessage.parseDelimitedFrom(Global.game_is);
				return recv.getResult();
			} catch (Exception e) {
				e.printStackTrace();
				return false;
			}
		}

	}

	public class getData extends AsyncTask<Void, Void, ProtocolUnit> {

		@Override
		public ProtocolUnit doInBackground(Void... voids) {
			try {
				GameMessage.newBuilder().setGameMessageType(
						GameMessageType.GetUnitByUnitNumber).setAccountNumber(
						Global.accountNumber).setGetUnitByUnitNumber(
						GetUnitByUnitNumber.newBuilder().setUnitNumber(unitNumber)).build().writeDelimitedTo(
						Global.game_os);

				GameMessage recv = GameMessage.parseDelimitedFrom(Global.game_is);

				if (recv.getResult()) {
					return recv.getGetUnitByUnitNumber().getUnit();
				}
			} catch (IOException e) {
				e.printStackTrace();
			}
			return null;
		}
	}

	public void onActivityResult(int requestCode, int resultCode, Intent intent) {
		super.onActivityResult(requestCode, resultCode, intent);
		switch (requestCode) {
		case requestChangeStat:
			if (resultCode == RESULT_OK) {
				showData();
			}
			break;
		}
	}

	public void onBackPressed() {
		finish();
	}
}
