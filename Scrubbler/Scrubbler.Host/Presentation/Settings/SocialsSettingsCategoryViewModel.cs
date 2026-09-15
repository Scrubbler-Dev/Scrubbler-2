namespace Scrubbler.Host.Presentation.Settings;

internal partial class SocialsSettingsCategoryViewModel(IWritableOptions<UserConfig> config) : SettingsCategoryViewModel(config)
{
    public override string Name { get; } = "Socials";
}
