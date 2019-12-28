package Triangle.Exceptions;

public class NoTokenException extends Exception {

	private static final long serialVersionUID = 8953543621691655318L;
	private int errCode;

	public NoTokenException(String msg) {
		super(msg);
	}

	public NoTokenException(String msg, int errCode) {
		super(msg);
		this.errCode = errCode;
	}

	public int getErrCode() {
		return this.errCode;
	}

}
