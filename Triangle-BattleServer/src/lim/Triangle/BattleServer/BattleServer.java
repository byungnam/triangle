package lim.Triangle.BattleServer;

/*
public class BattleServer implements IServerInterface {
	private Logger Log = Logger.getLogger(this.getClass());


	private ServerSocket serverSocket;
	private Socket socket_Session;

	private ExecutorService executor;
	private int NUM_OF_MAX_THREAD = 100;

	private static Long identifierNumber;

	public void init() {
		Log.info("BATTLE SERVER STARTING UP...");
		try {

			identifierNumber = new Long(0);

			taskQ = new LinkedBlockingQueue<CombatTask>();
			executor = Executors.newFixedThreadPool(NUM_OF_MAX_THREAD);

			executor.submit(new TaskRunner());

			socket_Session = new Socket("127.0.0.1", TriangleConf.PORT_DBSERVER);

			serverSocket = new ServerSocket(TriangleConf.PORT_BATTLESERVER);

			Log.info("BATTLE SERVER ONLINE...");
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	public void run() {
		while (true) {
			try {
				Socket socket = serverSocket.accept();
				executor.submit(new TaskReceiver(socket));
				Log.info("" + socket.getInetAddress() + " " + socket.getPort());
			} catch (IOException e) {
				e.printStackTrace();
			}
		}
	}

	public void shutdown() {
		taskQ.clear();
		try {
			executor.awaitTermination(600, TimeUnit.SECONDS);
		} catch (InterruptedException e) {
			e.printStackTrace();
		}
	}

	public List<CombatableUnit> getUnit(int teamNumber) {
		List<CombatableUnit> list = new LinkedList<CombatableUnit>();

		try {
			BattlePackets.newBuilder().setBattlePacketType(BattlePacketType.GetUnit).setGetUnit(
					GetUnit.newBuilder().setTeamNumber(teamNumber)).build().writeDelimitedTo(
					socket_Session.getOutputStream());
		} catch (IOException e) {
			e.printStackTrace();
		}
		return list;
	}

	// if (!ack.getResult()) {
	// return null;
	// }
	// CombatableUnit U = null;
	// for (Protocol_Unit unit : ack.getUnitsList()) {
	//
	// switch (UnitClass.values()[unit.getCls()]) {
	// case MAGE:
	// U = new Mage();
	// break;
	// case SOLDIER:
	// U = new Soldier();
	// break;
	//
	// default:
	// break;
	// }
	//
	// U.setName(unit.getName());
	// U.setAttribute(unit.getStr(), unit.getDex(), unit.getVital(),
	// unit.getIntel(), unit.getSpeed());
	//
	// for (Protocol_UnitTactic tactic : unit.getUnitTacticList()) {
	// Tactic T = new Tactic(tactic.getPriority(), tactic.getCondition(),
	// tactic.getValue(), tactic.getAction(), tactic.getActionparam());
	// U.addTactics(T);
	// }
	//
	// U.generateSpec();
	//
	// list.add(U);

	public void CombatReward() {
		try {
			BattlePackets.newBuilder().setBattlePacketType(
					BattlePacketType.CombatReward).setCombatReward(
					CombatReward.newBuilder()).build().writeDelimitedTo(
					socket_Session.getOutputStream());

			ShortAck ack = ShortAck.parseDelimitedFrom(socket_Session.getInputStream());
			if (!ack.getResult()) {
				Log.info("Error During gainExp");
			}
		} catch (Exception e) {
			e.printStackTrace();
		}

	}

	public BattleServer() {
		init();
		run();
		shutdown();
	}

	public static void main(String[] args) {
		new BattleServer();
	}
}
*/
