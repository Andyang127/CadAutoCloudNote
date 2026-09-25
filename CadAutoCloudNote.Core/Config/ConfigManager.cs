using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace CadAutoCloudNote.Core.Config
{
    /// <summary>
    /// 配置管理器 (全局 AppData 存取与内存缓存)
    /// </summary>
    public class ConfigManager
    {
        private static readonly ConfigManager _instance = new ConfigManager();
        public static ConfigManager Instance => _instance;

        private static NoteSettings _currentSettings;
        private static readonly object _lock = new object();

        public NoteSettings CurrentSettings => Current;

        public void SaveSettings(NoteSettings settings = null)
        {
            Save(settings);
        }

        public static string ConfigDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(Path.Combine(appData, "InkVerse"), "CadAutoCloudNote");
                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { }
                }
                return dir;
            }
        }

        public static string ConfigFilePath => Path.Combine(ConfigDirectory, "Config.json");

        public static NoteSettings Current
        {
            get
            {
                if (_currentSettings == null)
                {
                    lock (_lock)
                    {
                        if (_currentSettings == null)
                        {
                            _currentSettings = Load();
                        }
                    }
                }
                return _currentSettings;
            }
        }

        public static NoteSettings Load()
        {
            var settings = new NoteSettings();
            string path = ConfigFilePath;
            if (!File.Exists(path))
            {
                Save(settings);
                return settings;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var dict = ParseSimpleJson(json);

                // 1. 图层与样式
                if (dict.ContainsKey("CloudLayer")) settings.CloudLayer = dict["CloudLayer"];
                if (dict.ContainsKey("TextLayer")) settings.TextLayer = dict["TextLayer"];
                if (dict.ContainsKey("ForceNonPlotting")) settings.ForceNonPlotting = dict["ForceNonPlotting"].Equals("true", StringComparison.OrdinalIgnoreCase);
                if (dict.ContainsKey("CloudColorIndex") && int.TryParse(dict["CloudColorIndex"], out int cci)) settings.CloudColorIndex = cci;
                if (dict.ContainsKey("TextColorIndex") && int.TryParse(dict["TextColorIndex"], out int tci)) settings.TextColorIndex = tci;
                // 默认批注文字颜色为 1（红色）或 6（洋红色），若历史旧版本存有 7 (黑白)，自动纠偏升级为默认红色 1
                if (settings.TextColorIndex == 7) settings.TextColorIndex = 1;
                if (dict.ContainsKey("TextStyleName")) settings.TextStyleName = dict["TextStyleName"];

                // 2. 云线几何
                if (dict.ContainsKey("ArcLengthRatio") && double.TryParse(dict["ArcLengthRatio"], out double alr)) settings.ArcLengthRatio = alr;
                if (dict.ContainsKey("BulgeCurvature") && double.TryParse(dict["BulgeCurvature"], out double bc)) settings.BulgeCurvature = bc;
                if (dict.ContainsKey("DefaultCloudType") && int.TryParse(dict["DefaultCloudType"], out int dct)) settings.DefaultCloudType = dct;
                if (dict.ContainsKey("MinArcLength") && double.TryParse(dict["MinArcLength"], out double mal)) settings.MinArcLength = mal;

                // 3. 文字与引线
                if (dict.ContainsKey("TextHeightRatio") && double.TryParse(dict["TextHeightRatio"], out double thr)) settings.TextHeightRatio = thr;
                if (dict.ContainsKey("ArrowSizeRatio") && double.TryParse(dict["ArrowSizeRatio"], out double asr)) settings.ArrowSizeRatio = asr;
                if (dict.ContainsKey("LeaderType") && int.TryParse(dict["LeaderType"], out int lt)) settings.LeaderType = lt;
                if (dict.ContainsKey("AutoNumberPrefix")) settings.AutoNumberPrefix = dict["AutoNumberPrefix"];

                // 4. 业务与协同
                if (dict.ContainsKey("DefaultDiscipline")) settings.DefaultDiscipline = dict["DefaultDiscipline"];
                if (dict.ContainsKey("DefaultAssignee")) settings.DefaultAssignee = dict["DefaultAssignee"];
                if (dict.ContainsKey("DefaultPriority")) settings.DefaultPriority = dict["DefaultPriority"];
                if (dict.ContainsKey("AutoZoomOnSelect")) settings.AutoZoomOnSelect = dict["AutoZoomOnSelect"].Equals("true", StringComparison.OrdinalIgnoreCase);
                if (dict.ContainsKey("EnableSoundNotification")) settings.EnableSoundNotification = dict["EnableSoundNotification"].Equals("true", StringComparison.OrdinalIgnoreCase);

                // 5. 规范与 AI
                if (dict.ContainsKey("EnableAiSemantic")) settings.EnableAiSemantic = dict["EnableAiSemantic"].Equals("true", StringComparison.OrdinalIgnoreCase);
                if (dict.ContainsKey("AiSimilarityThreshold") && double.TryParse(dict["AiSimilarityThreshold"], out double ast)) settings.AiSimilarityThreshold = ast;
                if (dict.ContainsKey("AutoFillStandardCode")) settings.AutoFillStandardCode = dict["AutoFillStandardCode"].Equals("true", StringComparison.OrdinalIgnoreCase);

                if (dict.ContainsKey("MaxArcLengthRatio") && double.TryParse(dict["MaxArcLengthRatio"], out double mar)) settings.MaxArcLengthRatio = mar;
                if (dict.ContainsKey("CloudWidth") && double.TryParse(dict["CloudWidth"], out double cw)) settings.CloudWidth = cw;
                if (dict.ContainsKey("CloudStyle") && int.TryParse(dict["CloudStyle"], out int cstyle)) settings.CloudStyle = cstyle;
                if (dict.ContainsKey("AutoStartCreateAfterSave")) settings.AutoStartCreateAfterSave = dict["AutoStartCreateAfterSave"].Equals("true", StringComparison.OrdinalIgnoreCase);
                if (dict.ContainsKey("EnableSimpleNoteMode")) settings.EnableSimpleNoteMode = dict["EnableSimpleNoteMode"].Equals("true", StringComparison.OrdinalIgnoreCase);
                if (dict.ContainsKey("RapidDefaultPhrase")) settings.RapidDefaultPhrase = dict["RapidDefaultPhrase"];
                else if (dict.ContainsKey("SimpleModeDefaultContent")) settings.RapidDefaultPhrase = dict["SimpleModeDefaultContent"];
                if (dict.ContainsKey("EnableRapidCmdKeySelection")) settings.EnableRapidCmdKeySelection = dict["EnableRapidCmdKeySelection"].Equals("true", StringComparison.OrdinalIgnoreCase);
                if (dict.ContainsKey("CurrentTemplateId")) settings.CurrentTemplateId = dict["CurrentTemplateId"];
                if (dict.ContainsKey("SaveToPhraseLibraryByDefault")) settings.SaveToPhraseLibraryByDefault = dict["SaveToPhraseLibraryByDefault"].Equals("true", StringComparison.OrdinalIgnoreCase);

                // 6. 存储与审计
                if (dict.ContainsKey("EnableAuditTrail")) settings.EnableAuditTrail = dict["EnableAuditTrail"].Equals("true", StringComparison.OrdinalIgnoreCase);
                if (dict.ContainsKey("MaxNotesPerVolume") && int.TryParse(dict["MaxNotesPerVolume"], out int mnv)) settings.MaxNotesPerVolume = mnv;
                if (dict.ContainsKey("AutoBackupOnSave")) settings.AutoBackupOnSave = dict["AutoBackupOnSave"].Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // 加载失败时使用默认配置
            }

            return settings;
        }

        public static void Save(NoteSettings settings = null)
        {
            if (settings == null) settings = _currentSettings ?? new NoteSettings();
            _currentSettings = settings;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"CloudLayer\": \"{0}\",", Escape(settings.CloudLayer)));
                sb.AppendLine(string.Format("  \"TextLayer\": \"{0}\",", Escape(settings.TextLayer)));
                sb.AppendLine(string.Format("  \"ForceNonPlotting\": {0},", settings.ForceNonPlotting ? "true" : "false"));
                sb.AppendLine(string.Format("  \"CloudColorIndex\": {0},", settings.CloudColorIndex));
                sb.AppendLine(string.Format("  \"TextColorIndex\": {0},", settings.TextColorIndex));
                sb.AppendLine(string.Format("  \"TextStyleName\": \"{0}\",", Escape(settings.TextStyleName)));

                sb.AppendLine(string.Format("  \"ArcLengthRatio\": {0},", settings.ArcLengthRatio));
                sb.AppendLine(string.Format("  \"BulgeCurvature\": {0},", settings.BulgeCurvature));
                sb.AppendLine(string.Format("  \"DefaultCloudType\": {0},", settings.DefaultCloudType));
                sb.AppendLine(string.Format("  \"MinArcLength\": {0},", settings.MinArcLength));
                sb.AppendLine(string.Format("  \"MaxArcLengthRatio\": {0},", settings.MaxArcLengthRatio));
                sb.AppendLine(string.Format("  \"CloudWidth\": {0},", settings.CloudWidth));
                sb.AppendLine(string.Format("  \"CloudStyle\": {0},", settings.CloudStyle));

                sb.AppendLine(string.Format("  \"TextHeightRatio\": {0},", settings.TextHeightRatio));
                sb.AppendLine(string.Format("  \"ArrowSizeRatio\": {0},", settings.ArrowSizeRatio));
                sb.AppendLine(string.Format("  \"LeaderType\": {0},", settings.LeaderType));
                sb.AppendLine(string.Format("  \"AutoNumberPrefix\": \"{0}\",", Escape(settings.AutoNumberPrefix)));

                sb.AppendLine(string.Format("  \"DefaultDiscipline\": \"{0}\",", Escape(settings.DefaultDiscipline)));
                sb.AppendLine(string.Format("  \"DefaultAssignee\": \"{0}\",", Escape(settings.DefaultAssignee)));
                sb.AppendLine(string.Format("  \"DefaultPriority\": \"{0}\",", Escape(settings.DefaultPriority)));
                sb.AppendLine(string.Format("  \"AutoZoomOnSelect\": {0},", settings.AutoZoomOnSelect ? "true" : "false"));
                sb.AppendLine(string.Format("  \"EnableSoundNotification\": {0},", settings.EnableSoundNotification ? "true" : "false"));
                sb.AppendLine(string.Format("  \"AutoStartCreateAfterSave\": {0},", settings.AutoStartCreateAfterSave ? "true" : "false"));
                sb.AppendLine(string.Format("  \"EnableSimpleNoteMode\": {0},", settings.EnableSimpleNoteMode ? "true" : "false"));
                sb.AppendLine(string.Format("  \"RapidDefaultPhrase\": \"{0}\",", Escape(settings.RapidDefaultPhrase)));
                sb.AppendLine(string.Format("  \"EnableRapidCmdKeySelection\": {0},", settings.EnableRapidCmdKeySelection ? "true" : "false"));
                sb.AppendLine(string.Format("  \"CurrentTemplateId\": \"{0}\",", Escape(settings.CurrentTemplateId)));
                sb.AppendLine(string.Format("  \"SaveToPhraseLibraryByDefault\": {0},", settings.SaveToPhraseLibraryByDefault ? "true" : "false"));

                sb.AppendLine(string.Format("  \"EnableAiSemantic\": {0},", settings.EnableAiSemantic ? "true" : "false"));
                sb.AppendLine(string.Format("  \"AiSimilarityThreshold\": {0},", settings.AiSimilarityThreshold));
                sb.AppendLine(string.Format("  \"AutoFillStandardCode\": {0},", settings.AutoFillStandardCode ? "true" : "false"));

                sb.AppendLine(string.Format("  \"EnableAuditTrail\": {0},", settings.EnableAuditTrail ? "true" : "false"));
                sb.AppendLine(string.Format("  \"MaxNotesPerVolume\": {0},", settings.MaxNotesPerVolume));
                sb.AppendLine(string.Format("  \"AutoBackupOnSave\": {0}", settings.AutoBackupOnSave ? "true" : "false"));
                sb.AppendLine("}");

                File.WriteAllText(ConfigFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public void Save()
        {
            Save(_currentSettings);
        }

        public static void ResetToDefaults()
        {
            var settings = new NoteSettings();
            settings.ResetDefaults();
            _currentSettings = settings;
            Save(settings);
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static Dictionary<string, string> ParseSimpleJson(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(json)) return result;

            string[] lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmed = line.Trim().TrimEnd(',');
                int colonIdx = trimmed.IndexOf(':');
                if (colonIdx > 0)
                {
                    string key = trimmed.Substring(0, colonIdx).Trim().Trim('"');
                    string val = trimmed.Substring(colonIdx + 1).Trim().Trim('"');
                    result[key] = val;
                }
            }
            return result;
        }
    }
}
