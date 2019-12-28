package com.triangle.enterance;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.os.Handler;
import android.view.Window;

import com.triangle.R;

public class Enterance extends Activity {

	@Override
	public void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);
		setContentView(R.layout.enterance);

		new Thread(new initialize()).run();

		Handler mHandler = new Handler();
		mHandler.postDelayed(new Runnable() {
			public void run() {
				Intent i = new Intent(Enterance.this, Login.class); 
				startActivity(i);
				finish();
			}
		}, 500); 
	}

	public class initialize implements Runnable {
		public void run() {
		}
	}

}
