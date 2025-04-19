namespace Voidstrap.Models
{
    public class CustomIntegration
    {
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public string LaunchArgs { get; set; } = "";
        public bool AutoClose { get; set; } = true;
        public bool SpecifyGame { get; set; } = false;
        public string GameID { get; set; } = "";
        public bool AutoCloseOnGame { get; set; } = true;
    }
}
