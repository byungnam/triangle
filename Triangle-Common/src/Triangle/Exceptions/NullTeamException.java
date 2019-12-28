package Triangle.Exceptions;

public class NullTeamException extends Exception {

	/**
	 * 
	 */
	private static final long serialVersionUID = 1908842322227297050L;
	private int errCode;

	public NullTeamException(String msg) {
		super(msg);
	}

	public NullTeamException(String msg, int errCode) {
		super(msg);
		this.errCode = errCode;
	}

	public int getErrCode() {
		return this.errCode;
	}
}
