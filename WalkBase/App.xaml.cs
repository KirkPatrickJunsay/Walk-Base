using WalkBase.Services;

namespace WalkBase;

public partial class App : Application
{
	private readonly IStepSensor _sensor;

	public App(IStepSensor sensor)
	{
		InitializeComponent();
		_sensor = sensor;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		// Release the step sensor while backgrounded; re-enable + re-subscribe on return.
		window.Stopped += (_, _) => _sensor.Stop();
		window.Resumed += (_, _) => _sensor.Resume();
		return window;
	}
}