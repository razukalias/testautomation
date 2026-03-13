using System.Collections.Generic;

namespace Test_Automation.Models.ProjectFiles
{
    public class NodeFileModel
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
        public List<VariableExtractionFileModel> Extractors { get; set; } = new List<VariableExtractionFileModel>();
        public List<AssertionFileModel> Assertions { get; set; } = new List<AssertionFileModel>();
        public List<NodeFileModel> Children { get; set; } = new List<NodeFileModel>();
    }
}
