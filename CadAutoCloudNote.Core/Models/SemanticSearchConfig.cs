using System;

namespace CadAutoCloudNote.Core.Models
{
    /// <summary>
    /// 语义审查与知识库检索配置
    /// </summary>
    public class SemanticSearchConfig
    {
        public bool Enabled { get; set; } = true;
        public double MinSimilarityThreshold { get; set; } = 0.65;
        public int MaxSuggestions { get; set; } = 5;
        public string CustomDictionaryPath { get; set; } = string.Empty;
        public bool AutoCategorizeDiscipline { get; set; } = true;
    }
}
