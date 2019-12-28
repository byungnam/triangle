package com.triangle.mainview;

import java.io.IOException;
import java.util.concurrent.ExecutionException;

import Triangle.Protocol.GameProtocol.CreateUnit;
import Triangle.Protocol.GameProtocol.GameMessage;
import Triangle.Protocol.GameProtocol.GameMessage.GameMessageType;
import android.app.Activity;
import android.os.AsyncTask;
import android.os.Bundle;
import android.view.View;
import android.view.Window;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Toast;

import com.triangle.R;
import com.triangle.common.Global;

public class CreateNewUnit extends Activity {
	private Button acceptButton;
	private EditText unitNameEditText;
	private int selectedClass;

	public void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);
		setContentView(R.layout.newunit);

//		ArrayAdapter<String> arrayAdapter1 = new ArrayAdapter<String>(this,
//				android.R.layout.simple_spinner_item, TriangleValues.UnitClass.toReadableArray());
//		Spinner spinner = (Spinner) findViewById(R.id.createunit_spinner1);
//		spinner.setAdapter(arrayAdapter1);
//		spinner.setOnItemSelectedListener(new OnItemSelectedListener() {
//			public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
//				selectedClass = position;
//			}
//
//			public void onNothingSelected(AdapterView<?> parent) {
//				selectedClass = 0;
//			}
//		});

		unitNameEditText = (EditText) findViewById(R.id.name);

		acceptButton = (Button) findViewById(R.id.Accept);
		acceptButton.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				try { 
					if (new createUnit().execute().get()) {
						setResult(RESULT_OK);
						finish();
					} else {
						Toast.makeText(CreateNewUnit.this, "Failed to create new unit",
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
	
	public class createUnit extends AsyncTask<Void, Void, Boolean> {

		@Override
		protected Boolean doInBackground(Void... params) {
			try {
				GameMessage.newBuilder().setGameMessageType(
						GameMessageType.CreateUnit).setAccountNumber(Global.accountNumber).setCreateUnit(
						CreateUnit.newBuilder().setName(
								unitNameEditText.getText().toString()).setCls(
								selectedClass)).build().writeDelimitedTo(
						Global.game_os);
				return GameMessage.parseDelimitedFrom(Global.game_is).getResult();
			} catch (IOException e) {
				e.printStackTrace();
			}
			return false;
		}
	}

	public void onBackPressed() {
		finish();
	}

}
