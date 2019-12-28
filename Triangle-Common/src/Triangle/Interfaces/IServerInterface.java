package Triangle.Interfaces;

import java.util.concurrent.ExecutionException;

public interface IServerInterface {
	public void init();

	public void run() throws ExecutionException;

	public void shutdown();
}
