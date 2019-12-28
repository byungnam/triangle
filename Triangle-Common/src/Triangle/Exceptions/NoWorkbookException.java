package Triangle.Exceptions;

public class NoWorkbookException extends Exception {

	private static final long serialVersionUID = 8142323990222763357L;
	private int errCode;

	public NoWorkbookException(String msg) {
		super(msg);
	}

	public NoWorkbookException(String msg, int errCode) {
		super(msg);
		this.errCode = errCode;
	}

	public int getErrCode() {
		return this.errCode;
	}

}
