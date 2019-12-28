package com.triangle.network;

import java.io.IOException;

import Triangle.Protocol.GameProtocol.GameMessage;
import android.os.AsyncTask;

import com.triangle.common.Global;

public class GameMessageSender extends
		AsyncTask<Void, Void, Boolean> {

	GameMessage msg;
	public GameMessageSender(GameMessage msg){
		this.msg = msg;
	}
	
	@Override
	public Boolean doInBackground(Void... voids) {
		try {
			msg.writeDelimitedTo(Global.game_os);
			return true;
		} catch (IOException e) {
			e.printStackTrace();
		}
		return false;
	}
}
