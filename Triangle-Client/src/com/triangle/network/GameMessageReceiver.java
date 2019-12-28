package com.triangle.network;

import java.io.IOException;

import Triangle.Protocol.GameProtocol.GameMessage;
import android.os.AsyncTask;
import android.util.Log;

import com.triangle.common.Global;

public class GameMessageReceiver extends AsyncTask<Void, Void, GameMessage> {

	@Override
	public GameMessage doInBackground(Void... voids) {
		try {
			GameMessage recv = GameMessage.parseDelimitedFrom(Global.game_is);
			if (recv.getResult()) {
				Log.i("com.triangle2", "game message result received : "+ recv.getResult());
				return recv;
			} 
		} catch (IOException e) {
			e.printStackTrace();
		}
		return null;
	}

}
