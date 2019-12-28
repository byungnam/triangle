package com.triangle.common;

import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;

public class Global {
	public static Socket login_socket;
	public static OutputStream login_os;
	public static InputStream login_is;
	
	public static Socket game_socket;
	public static OutputStream game_os;
	public static InputStream game_is;
	
	public static int accountNumber;
	
//	public static String action_soldier[] = { "미선택", "기본공격", "찌르기", "더블어택", "자기회복" };
//	public static String action_specialist[] = { "미선택", "기본공격", "트리플 샷", "독화살", "조준사격" };
//	public static String action_magician[] = { "미선택", "기본공격", "번개화살", "파이어월", "운석소환",
//			"태풍", "자기마나회복" };
//	public static String action_shaman[] = { "미선택", "치유", "팀원 치유", "상급 치유", "더블 치유",
//			"자기마나회복", "파티마나회복" };
//	public static String[] UnitClass = {
//		"Soldier", "Mage", "Medic"
//	};
	
}
