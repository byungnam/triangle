package com.triangle.enterance;

import java.net.Socket;
import java.util.ArrayList;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

import Triangle.Protocol.LoginProtocol.CreateAccount;
import Triangle.Protocol.LoginProtocol.LoginMessage;
import Triangle.Protocol.LoginProtocol.LoginMessage.LoginMessageType;
import Triangle.TriangleConfigure.TriangleConf;
import android.app.Activity;
import android.content.Intent;
import android.os.AsyncTask;
import android.os.Bundle;
import android.view.View;
import android.view.Window;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Toast;

import com.triangle.R;
import com.triangle.common.Global;

public class CreateNewId extends Activity {
	ArrayList<String> Firstlist = new ArrayList<String>();
	private Button accept;
	private EditText idbox;
	private EditText pwbox;

	private static final int result_newid = 0;

	@Override
	public void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);
		setContentView(R.layout.newid);

		idbox = (EditText) findViewById(R.id.name);
		pwbox = (EditText) findViewById(R.id.passwd);

		accept = (Button) findViewById(R.id.Accept);
		accept.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {

				String id, pw;
				id = idbox.getText().toString();
				pw = pwbox.getText().toString();
				Intent intent = new Intent();

				AsyncTask<String, Void, Boolean> task = new CreateIDRequest().execute(
						id, pw);
				try {
					if (task.get(1, TimeUnit.SECONDS)) {
						Toast.makeText(CreateNewId.this, "Success", Toast.LENGTH_SHORT).show();
						intent.putExtra("id", id);
						intent.putExtra("pw", pw);
						setResult(result_newid, intent);
						finish();
					} else {
						Toast.makeText(CreateNewId.this, "Failed", Toast.LENGTH_SHORT).show();
					}
				} catch (InterruptedException e) {
					e.printStackTrace();
				} catch (ExecutionException e) {
					e.printStackTrace();
				} catch (TimeoutException e) {
					e.printStackTrace();
				}
			}
		});

	}

	public class CreateIDRequest extends AsyncTask<String, Void, Boolean> {

		@Override
		protected Boolean doInBackground(String... params) {
			try {

				String id, pw;
				id = params[0];
				pw = params[1];

				Global.login_socket = new Socket(TriangleConf.ADDRESS_LOGINSERVER,
						TriangleConf.PORT_toClient_LOGINSERVER);
				Global.login_os = Global.login_socket.getOutputStream();
				Global.login_is = Global.login_socket.getInputStream();

				LoginMessage.newBuilder().setLoginMessageType(
						LoginMessageType.CreateAccount).setCreateAccount(
						CreateAccount.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
						Global.login_os);
				LoginMessage result = LoginMessage.parseDelimitedFrom(Global.login_is);

				Global.login_os.close();
				Global.login_is.close();
				Global.login_socket.close();

				if (result.getCreateAccount().getSuccess()) {
					return true;
				} else {
					return false;
				}
			} catch (Exception e) {
				e.printStackTrace();
			}
			return null;
		}
	}

	public void onBackPressed() {
		finish();
	}
}
