using WalkBase.Views;

namespace WalkBase;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		// Stats is reached from the Insights page (pushed, not a tab).
		Routing.RegisterRoute("stats", typeof(StatsPage));
		// Goals/quests are reached from the Town page (pushed, not a tab).
		Routing.RegisterRoute("quests", typeof(QuestsPage));
		// Privacy explainer, reached from Settings.
		Routing.RegisterRoute("privacy", typeof(PrivacyPage));
		// Achievements/profile, reached from Insights.
		Routing.RegisterRoute("profile", typeof(ProfilePage));
	}
}
