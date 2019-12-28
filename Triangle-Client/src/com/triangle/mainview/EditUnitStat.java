package com.triangle.mainview;

import java.io.IOException;
import java.util.concurrent.ExecutionException;

import Triangle.Protocol.GameProtocol.EditUnit;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import Triangle.Protocol.GameProtocol.GetUnitByUnitNumber;
import Triangle.Protocol.GameProtocol.ProtocolUnit;
import android.app.Activity;
import android.os.AsyncTask;
import android.os.Bundle;
import android.view.View;
import android.view.Window;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

import com.triangle.R;
import com.triangle.common.Global;

public class EditUnitStat extends Activity {
	private Button save;
	private TextView textView_str, textView_dex, textView_vital, textView_intel,
			textView_speed, textView_point;
	private int o_str, o_dex, o_vital, o_intel, o_speed, o_point;
	private int p_str, p_dex, p_vital, p_intel, p_speed;
	private Button dButton_str, dButton_dex, dButton_vital, dButton_intel,
			dButton_speed;
	private Button iButton_str, iButton_dex, iButton_vital, iButton_intel,
			iButton_speed;
	private int unitNumber;
	private ProtocolUnit protoUnit;

	@Override
	public void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);
		setContentView(R.layout.changestatus);

		dButton_str = (Button) findViewById(R.id.leftArrowStr);
		dButton_dex = (Button) findViewById(R.id.leftArrowDex);
		dButton_vital = (Button) findViewById(R.id.leftArrowVital);
		dButton_intel = (Button) findViewById(R.id.leftArrowIntel);
		dButton_speed = (Button) findViewById(R.id.leftArrowSpeed);

		iButton_str = (Button) findViewById(R.id.rightArrowStr);
		iButton_dex = (Button) findViewById(R.id.rightArrowDex);
		iButton_vital = (Button) findViewById(R.id.rightArrowVital);
		iButton_intel = (Button) findViewById(R.id.rightArrowIntel);
		iButton_speed = (Button) findViewById(R.id.rightArrowSpeed);

		textView_str = (TextView) findViewById(R.id.str);
		textView_dex = (TextView) findViewById(R.id.dex);
		textView_vital = (TextView) findViewById(R.id.vital);
		textView_intel = (TextView) findViewById(R.id.intel);
		textView_speed = (TextView) findViewById(R.id.speed);
		textView_point = (TextView) findViewById(R.id.point);

		unitNumber = getIntent().getIntExtra("unitNumber", -1);

		try {
			if(!new getUnitByUnitNumber().execute().get()){
				Toast.makeText(this, "Failed to get Unit Data from Server",
						Toast.LENGTH_SHORT).show();
			}
		} catch (InterruptedException e) {
			e.printStackTrace();
		} catch (ExecutionException e) {
			e.printStackTrace();
		}

		dButton_str.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				if (p_str > 0) {
					p_str--;
					o_point++;
					textView_str.setText("" + (o_str + p_str));
					textView_point.setText("" + o_point);
				}
			}
		});
		dButton_dex.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				if (p_dex > 0) {
					p_dex--;
					o_point++;
					textView_str.setText("" + (o_dex + p_dex));
					textView_point.setText("" + o_point);
				}
			}
		});
		dButton_vital.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				if (p_vital > 0) {
					p_vital--;
					o_point++;
					textView_str.setText("" + (o_vital + p_vital));
					textView_point.setText("" + o_point);
				}
			}
		});
		dButton_intel.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				if (p_intel > 0) {
					p_intel--;
					o_point++;
					textView_str.setText("" + (o_intel + p_intel));
					textView_point.setText("" + o_point);
				}
			}
		});
		dButton_speed.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				if (p_speed > 0) {
					p_speed--;
					o_point++;
					textView_str.setText("" + (o_speed + p_speed));
					textView_point.setText("" + o_point);
				}
			}
		});

		iButton_str.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				// if (o_point > 0) {
				p_str++;
				o_point--;
				textView_str.setText("" + (o_str + p_str));
				textView_point.setText("" + o_point);
				// }
			}
		});
		iButton_dex.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				if (o_point > 0) {
					p_dex++;
					o_point--;
					textView_str.setText("" + (o_dex + p_dex));
					textView_point.setText("" + o_point);
				}
			}
		});
		iButton_vital.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				if (o_point > 0) {
					p_vital++;
					o_point--;
					textView_str.setText("" + (o_vital + p_vital));
					textView_point.setText("" + o_point);
				}
			}
		});
		iButton_intel.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				if (o_point > 0) {
					p_intel++;
					o_point--;
					textView_str.setText("" + (o_intel + p_intel));
					textView_point.setText("" + o_point);
				}
			}
		});
		iButton_speed.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				if (o_point > 0) {
					p_speed++;
					o_point--;
					textView_str.setText("" + (o_speed + p_speed));
					textView_point.setText("" + o_point);
				}
			}
		});

		save = (Button) findViewById(R.id.save);
		save.setText("¿˙¿Â");
		save.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				try {
					if (new editUnit().execute().get()) {
						setResult(RESULT_OK);
						finish();
					} else {
						Toast.makeText(EditUnitStat.this, "Failed to edit unit",
								Toast.LENGTH_SHORT).show();
					}
				} catch (InterruptedException e) {
					e.printStackTrace();
				} catch (ExecutionException e) {
					e.printStackTrace();
				}
			}
		});
	}

	public class getUnitByUnitNumber extends AsyncTask<Void, Void, Boolean> {

		@Override
		protected Boolean doInBackground(Void... params) {
			try {
				GameMessage.newBuilder().setGameMessageType(
						GameMessageType.GetUnitByUnitNumber).setAccountNumber(
						Global.accountNumber).setGetUnitByUnitNumber(
						GetUnitByUnitNumber.newBuilder().setUnitNumber(unitNumber)).build().writeDelimitedTo(
						Global.game_os);
				GameMessage msg = GameMessage.parseDelimitedFrom(Global.game_is);

				if (msg.getResult()) {
					protoUnit = msg.getGetUnitByUnitNumber().getUnit();

					o_str = protoUnit.getStr();
					o_dex = protoUnit.getDex();
					o_vital = protoUnit.getVital();
					o_intel = protoUnit.getIntel();
					o_speed = protoUnit.getSpeed();
					o_point = protoUnit.getPoint();

					textView_str.setText("" + o_str);
					textView_dex.setText("" + o_dex);
					textView_vital.setText("" + o_vital);
					textView_intel.setText("" + o_intel);
					textView_speed.setText("" + o_speed);
					textView_point.setText("" + o_point);
					return true;
				}
			} catch (IOException e) {
				e.printStackTrace();
			}
			return false;
		}
	}

	public class editUnit extends AsyncTask<Void, Void, Boolean> {

		@Override
		protected Boolean doInBackground(Void... params) {
			protoUnit = protoUnit.toBuilder().setStr(o_str + p_str).setDex(
					o_dex + p_dex).setVital(o_vital + p_vital).setIntel(o_intel + p_intel).setSpeed(
					o_speed + p_speed).setPoint(o_point).build();
			try {
				GameMessage.newBuilder().setGameMessageType(GameMessageType.EditUnit).setAccountNumber(
						Global.accountNumber).setEditUnit(
						EditUnit.newBuilder().setUnitNumber(unitNumber).setUnit(protoUnit)).build().writeDelimitedTo(
						Global.game_os);
				GameMessage msg = GameMessage.parseDelimitedFrom(Global.game_is);

				if (msg.getResult()) {
					return true;
				} else {
					return false;
				}
			} catch (IOException e) {
				e.printStackTrace();
				return false;
			}
		}

	}

	public void onBackPressed() {
		finish();
	}
}
