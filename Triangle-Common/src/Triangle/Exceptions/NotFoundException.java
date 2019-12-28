package Triangle.Exceptions;

public class NotFoundException extends Exception {

	private static final long serialVersionUID = -651859641105284497L;
	private int errCode;

	public NotFoundException(String msg) {
		super(msg);
	}

	public NotFoundException(String msg, int errCode) {
		super(msg);
		this.errCode = errCode;
	}

	public int getErrCode() {
		return this.errCode;
	}
}
