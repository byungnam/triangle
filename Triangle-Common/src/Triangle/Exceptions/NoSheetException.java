package Triangle.Exceptions;

public class NoSheetException extends Exception {

	private static final long serialVersionUID = -651859641105284497L;
	private int errCode;

	public NoSheetException(String msg) {
		super(msg);
	}

	public NoSheetException(String msg, int errCode) {
		super(msg);
		this.errCode = errCode;
	}

	public int getErrCode() {
		return this.errCode;
	}

}
