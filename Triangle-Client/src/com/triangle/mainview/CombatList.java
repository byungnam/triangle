package com.triangle.mainview;

import android.app.Activity;
import android.content.Context;
import android.os.Bundle;
import android.view.Gravity;
import android.view.Window;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import com.triangle.R;

public class CombatList extends Activity {
	StringBuffer buffer01 = new StringBuffer();
	StringBuffer buffer02 = new StringBuffer();
	StringBuffer sbuffer;

	StringBuffer result = new StringBuffer();

	public Context getContext() {
		return this;
	}

	public void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		this.requestWindowFeature(Window.FEATURE_NO_TITLE);

		setContentView(R.layout.combat);

		LinearLayout combatFieldLayout = (LinearLayout) findViewById(R.id.CombatField);
		LinearLayout combatTextLayout = (LinearLayout) findViewById(R.id.CombatText);
		ImageView uniticon = new ImageView(this);
		TextView unitname = new TextView(this);
		TextView combatText = new TextView(this);

		LinearLayout leftTeamLayout = new LinearLayout(this);
		leftTeamLayout.setLayoutParams(new LinearLayout.LayoutParams(
				LinearLayout.LayoutParams.MATCH_PARENT,
				LinearLayout.LayoutParams.WRAP_CONTENT));
		leftTeamLayout.setOrientation(LinearLayout.VERTICAL);
		leftTeamLayout.setGravity(Gravity.CENTER);

		LinearLayout rightTeamLayout = new LinearLayout(this);
		rightTeamLayout.setLayoutParams(new LinearLayout.LayoutParams(
				LinearLayout.LayoutParams.MATCH_PARENT,
				LinearLayout.LayoutParams.WRAP_CONTENT));
		rightTeamLayout.setOrientation(LinearLayout.VERTICAL);
		rightTeamLayout.setGravity(Gravity.CENTER);

		LinearLayout unitLayout = new LinearLayout(this);
		unitLayout.setLayoutParams(new LinearLayout.LayoutParams(
				LinearLayout.LayoutParams.MATCH_PARENT,
				LinearLayout.LayoutParams.WRAP_CONTENT));
		unitLayout.setOrientation(LinearLayout.HORIZONTAL);
		unitLayout.setGravity(Gravity.LEFT);

		combatTextLayout.setLayoutParams(new LinearLayout.LayoutParams(
				LinearLayout.LayoutParams.MATCH_PARENT,
				LinearLayout.LayoutParams.WRAP_CONTENT));

		uniticon.setImageResource(R.drawable.archor);
		unitname.setText("NAME");
		
		

		unitLayout.addView(uniticon);
		unitLayout.addView(unitname);
		leftTeamLayout.addView(unitLayout);

		combatFieldLayout.addView(leftTeamLayout);

		combatText.setWidth(combatTextLayout.getWidth() / 2);

		combatTextLayout.addView(combatText);
		
		
		
	}
	
	public void getCombatResult(){
		
	}

	public void onBackPressed() {
		finish();
	}

}
