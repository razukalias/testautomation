namespace Test_Automation.Models.ProjectFiles
{
    public class AssertionFileModel
    {
        public string Mode { get; set; } = "Assert";
        public string Source { get; set; } = string.Empty;
        public string JsonPath { get; set; } = string.Empty;
        public string Condition { get; set; } = "Equals";
        public string Expected { get; set; } = string.Empty;
    }
}
