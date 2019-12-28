package com.triangle.enterance;

import java.io.IOException;
import java.net.Socket;
import java.util.List;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

import Triangle.Protocol.LoginProtocol.Authentication;
import Triangle.Protocol.LoginProtocol.JoinServer;
import Triangle.Protocol.LoginProtocol.LoginMessage;
import Triangle.Protocol.LoginProtocol.LoginMessage.LoginMessageType;
import Triangle.TriangleConfigure.TriangleConf;
import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
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
import com.triangle.mainview.UnitGrid;

public class Login extends Activity {
	private Button login;
	private Button newID;
	private EditText text_id;
	private EditText text_pw;
	private List<String> serverList;
	private static final int result_newid = 0;

	@Override
	public void onCreate(Bundle savedInstanceState) {

		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);
		setContentView(R.layout.login);

		text_id = (EditText) findViewById(R.id.idt);
		text_pw = (EditText) findViewById(R.id.pswd);

		// final Bundle b = this.getIntent().getExtras();

		login = (Button) findViewById(R.id.login);
		login.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				String id, pw;
				id = (text_id.getText().toString());
				pw = (text_pw.getText().toString());
				try {
					LoginMessage msg = new authenticate().execute(id, pw).get(1,
							TimeUnit.SECONDS);

					int accNum = msg.getAuthentication().getAccountNumber();
					if (accNum >= 0) {
						Global.accountNumber = accNum;
						Toast.makeText(Login.this, "Login Success", Toast.LENGTH_SHORT).show();

						serverList = msg.getAuthentication().getServerNameList();
						CharSequence[] seq = serverList.toArray(new CharSequence[msg.getAuthentication().getServerNameList().size()]);
						new AlertDialog.Builder(Login.this).setTitle("채널").setItems(seq,
								new DialogInterface.OnClickListener() {
									public void onClick(DialogInterface dialog, int which) {
										try {
											if (new joinServer().execute(which).get(1,
													TimeUnit.SECONDS)) {
												startActivity(new Intent(Login.this, UnitGrid.class));
												finish();
											}
										} catch (InterruptedException e) {
											e.printStackTrace();
										} catch (ExecutionException e) {
											e.printStackTrace();
										} catch (TimeoutException e) {
											e.printStackTrace();
										}
									}
								}).show();
					} else {
						Toast.makeText(Login.this, "Login Failed " + accNum,
								Toast.LENGTH_SHORT).show();
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

		newID = (Button) findViewById(R.id.newID);
		newID.setOnClickListener(new View.OnClickListener() {
			public void onClick(View v) {
				Intent intentNewId = new Intent(Login.this, CreateNewId.class);
				startActivityForResult(intentNewId, result_newid);
			}
		});
	}

	public class authenticate extends AsyncTask<String, Void, LoginMessage> {

		@Override
		protected LoginMessage doInBackground(String... params) {
			try {
				String id, pw;
				id = params[0];
				pw = params[1];

				// Socket login_socket;
				// OutputStream login_os;
				// InputStream login_is;

				Global.login_socket = new Socket(TriangleConf.ADDRESS_LOGINSERVER,
						TriangleConf.PORT_toClient_LOGINSERVER);
				Global.login_os = Global.login_socket.getOutputStream();
				Global.login_is = Global.login_socket.getInputStream();

				LoginMessage.newBuilder().setLoginMessageType(
						LoginMessageType.Authentication).setAuthentication(
						Authentication.newBuilder().setId(id).setPassword(pw)).build().writeDelimitedTo(
						Global.login_os);

				LoginMessage result = LoginMessage.parseDelimitedFrom(Global.login_is);
				Global.login_os.close();
				Global.login_is.close();
				Global.login_socket.close();

				return result;

			} catch (Exception e) {
				e.printStackTrace();
			}
			return null;
		}
	}

	public class joinServer extends AsyncTask<Integer, Void, Boolean> {
		@Override
		public Boolean doInBackground(Integer... serverNumber) {
			String server = serverList.get(serverNumber[0]);
			try {
				Global.login_socket = new Socket(TriangleConf.ADDRESS_LOGINSERVER,
						TriangleConf.PORT_toClient_LOGINSERVER);
				Global.login_os = Global.login_socket.getOutputStream();
				Global.login_is = Global.login_socket.getInputStream();

				LoginMessage.newBuilder().setLoginMessageType(
						LoginMessageType.JoinServer).setJoinServer(
						JoinServer.newBuilder().setAccountNumber(Global.accountNumber).setServerName(
								server)).build().writeDelimitedTo(Global.login_os);
				LoginMessage result = LoginMessage.parseDelimitedFrom(Global.login_is);

				Global.login_is.close();
				Global.login_os.close();
				Global.login_socket.close();

				// Log.i("com.triangle2",
				// result.getJoinServer().getServerInformation().getServerAddress()
				// + " "
				// + result.getJoinServer().getServerInformation().getServerPort());

				Global.game_socket = new Socket(
						result.getJoinServer().getServerInformation().getServerAddress(),
						result.getJoinServer().getServerInformation().getServerPort());

				Global.game_is = Global.game_socket.getInputStream();
				Global.game_os = Global.game_socket.getOutputStream();

				return true;
			} catch (IOException e) {
				e.printStackTrace();
				return false;
			}
		}
	}

	public void onActivityResult(int requestCode, int resultCode, Intent intent) {
		super.onActivityResult(requestCode, resultCode, intent);
		switch (requestCode) {
		case result_newid:
			if (resultCode == RESULT_OK) {
				text_id.setText(intent.getExtras().getString("id"));
				text_pw.setText(intent.getExtras().getString("pw"));
			}
		}
	}

	public void onBackPressed() {
		new AlertDialog.Builder(this).setTitle("게임 종료").setMessage("정말 종료하시겠습니까?").setPositiveButton(
				"확인", new DialogInterface.OnClickListener() {
					public void onClick(DialogInterface dialog, int which) {
						finish();
					}
				}).setNegativeButton("취소", new DialogInterface.OnClickListener() {
			public void onClick(DialogInterface dialog, int which) {
				// TODO Auto-generated method stub
			}
		}).show();
	}
}
