using System;

namespace CadAutoCloudNote.Core.Config
{
    /// <summary>
    /// 插件全局与图纸级配置参数
    /// </summary>
    public class NoteSettings
    {
        // 1. 图层与样式 (Layer & Style)
        public string CloudLayer { get; set; } = "CAD_NOTE_CLOUD";
        public string DefaultLayerName { get => CloudLayer; set => CloudLayer = value; }

        public string TextLayer { get; set; } = "CAD_NOTE_TEXT";
        public bool ForceNonPlotting { get; set; } = true;
        public bool ForceNonPlottable { get => ForceNonPlotting; set => ForceNonPlotting = value; }

        public int CloudColorIndex { get; set; } = 1;         // 云线默认颜色索引 (1=红色, 6=洋红, 256=ByLayer)
        public int TextColorIndex { get; set; } = 1;          // 文字默认颜色索引 (1=红色, 6=洋红, 默认红色)
        public string TextStyleName { get; set; } = "Standard";
        public string DefaultTextStyle { get => TextStyleName; set => TextStyleName = value; }

        // 2. 云线几何 (Cloud Geometry)
        public double ArcLengthRatio { get; set; } = 6.0;      // 弧长相对图纸比例的系数 (默认 6.0，贴近 AutoCAD 原生 REVCLOUD 自然云弧)
        public double CloudArcLengthFactor { get => ArcLengthRatio; set => ArcLengthRatio = value; }
        public double BulgeCurvature { get; set; } = 0.520567; // 云线圆弧凸度 (默认 0.520567 约 110 度圆弧)
        public double CloudBulge { get => BulgeCurvature; set => BulgeCurvature = value; }
        public int DefaultCloudType { get; set; } = 0;        // 默认绘制模式: 0=矩形(Rect), 1=多边形(Polygon), 2=徒手(Freehand)
        public double MinArcLength { get; set; } = 3.0;        // 最小弧长保护下限 (防止密集群弧卡死)
        public double MaxArcLengthRatio { get; set; } = 30.0;  // 最大弧长保护上限系数
        public double MaxArcLengthFactor { get => MaxArcLengthRatio; set => MaxArcLengthRatio = value; }
        public double CloudWidth { get; set; } = 0.0;          // 云线多段线全局宽度 (0=默认细线, 0.3mm, 0.5mm, 0.8mm, 1.0mm)
        public int CloudStyle { get; set; } = 1;               // 云线样式: 0=普通等宽(Normal), 1=书法笔锋(Calligraphy)

        // 3. 文字与引线 (Text & Leader)
        public double TextHeightRatio { get; set; } = 3.5;    // 文字高度相对图纸比例系数 (默认 3.5)
        public double DefaultTextHeight { get => TextHeightRatio; set => TextHeightRatio = value; }
        public double ArrowSizeRatio { get; set; } = 2.5;     // 引线箭头大小系数 (默认 2.5)
        public int LeaderType { get; set; } = 0;              // 引线模式: 0=带箭头引线, 1=点引线, 2=无引线
        public string AutoNumberPrefix { get; set; } = "【";   // 批注编号前缀 (例如 "【", "#", "NOTE-")

        // 4. 默认业务与协同属性 (Discipline & Workflow)
        public string DefaultDiscipline { get; set; } = "建筑";  // 默认专业（建筑、结构、给排水、暖通、电气、总图、通用）
        public string DefaultAssignee { get; set; } = Environment.UserName;    // 默认批注者 / 审核人
        public string LastReplyAuthor { get; set; } = string.Empty;            // 上次流转回复人 (记忆用户输入)
        public string DefaultPriority { get; set; } = "重要";    // 优先级 (重要、紧急)
        public bool AutoZoomOnSelect { get; set; } = true;     // 看板选中是否自动缩放定位
        public bool EnableSoundNotification { get; set; } = false;
        public bool AutoStartCreateAfterSave { get; set; } = false; // 保存设置后立即启动新建批注 (CNOTE)
        public bool EnableSimpleNoteMode { get; set; } = true;      // 启用极简批注模式 (快速批注，免弹窗快速录入)
        public string RapidDefaultPhrase { get; set; } = "待修改";  // 极速批注回车默认短语
        public string SimpleModeDefaultContent { get => RapidDefaultPhrase; set => RapidDefaultPhrase = value; }
        public bool EnableRapidCmdKeySelection { get; set; } = true; // 极速模式引线放置时允许键盘单键切词 (快捷短语S/输入文字T)
        public string CurrentTemplateId { get; set; } = "tpl_general"; // 当前选定的工程场景模板 ID
        public bool SaveToPhraseLibraryByDefault { get; set; } = true; // 是否默认勾选存入常用快捷短语库 (记忆用户勾选状态)

        // 5. 规范与 AI 智能 (Knowledge & AI)
        public bool EnableAiSemantic { get; set; } = false;     // 是否开启 AI 语义检索匹配
        public bool EnableAiSuggestions { get => EnableAiSemantic; set => EnableAiSemantic = value; }
        public double AiSimilarityThreshold { get; set; } = 0.60; // 语义匹配相似度阈值
        public bool AutoFillStandardCode { get; set; } = true;  // 自动将匹配规范条文编号填入批注

        // 6. 存储与审计 (Storage & Audit)
        public bool EnableAuditTrail { get; set; } = true;     // 开启防篡改审计日志
        public int MaxNotesPerVolume { get; set; } = 50;       // 单卷最大容纳批注数 (分卷上限)
        public bool AutoBackupOnSave { get; set; } = true;     // 保存图纸时自动备份批注元数据

        // 7. 高级联动机制 (Advanced Linkage)
        public bool EnablePriorityColorLink { get; set; } = true;    // 开启优先级与颜色智能联动
        public int PriorityColorNormal { get; set; } = 3;            // 一般优先级颜色 (默认 3=绿色)
        public int PriorityColorImportant { get; set; } = 6;         // 重要优先级颜色 (默认 6=品红)
        public int PriorityColorUrgent { get; set; } = 1;            // 紧急优先级颜色 (默认 1=红色)

        // 常用审查意见快捷词典
        public System.Collections.Generic.List<string> QuickReviewSnippets { get; set; } = new System.Collections.Generic.List<string>
        {
            "待修改",
            "请核对门窗洞口净尺寸与定位",
            "此处配筋与结构计算书不符",
            "管线与梁底标高冲突，请调整",
            "不满足消防疏散距离规范要求",
            "请补充细部构造做法与大样索引"
        };

        // 团队成员名单字典
        public System.Collections.Generic.List<string> TeamMembers { get; set; } = new System.Collections.Generic.List<string>
        {
            Environment.UserName,
            "项目负责人",
            "专业审定人",
            "校对人",
            "设计人"
        };

        public static CadAutoCloudNote.Core.Protocols.NotePriority ParsePriority(string text)
        {
            if (string.IsNullOrEmpty(text)) return CadAutoCloudNote.Core.Protocols.NotePriority.Important;
            if (text.Contains("紧急") || text.IndexOf("Urgent", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("High", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Critical", StringComparison.OrdinalIgnoreCase) >= 0)
                return CadAutoCloudNote.Core.Protocols.NotePriority.Urgent;
            if (text.Contains("一般") || text.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0)
                return CadAutoCloudNote.Core.Protocols.NotePriority.Normal;
            return CadAutoCloudNote.Core.Protocols.NotePriority.Important;
        }

        public int GetPriorityColorIndex(CadAutoCloudNote.Core.Protocols.NotePriority priority)
        {
            if (!EnablePriorityColorLink) return CloudColorIndex;
            switch (priority)
            {
                case CadAutoCloudNote.Core.Protocols.NotePriority.Urgent:
                    return PriorityColorUrgent;
                case CadAutoCloudNote.Core.Protocols.NotePriority.Important:
                    return PriorityColorImportant;
                case CadAutoCloudNote.Core.Protocols.NotePriority.Normal:
                default:
                    return PriorityColorNormal;
            }
        }

        public int GetPriorityColorIndex(string priorityText)
        {
            if (!EnablePriorityColorLink) return CloudColorIndex;
            return GetPriorityColorIndex(ParsePriority(priorityText));
        }

        public NoteSettings()
        {
        }

        /// <summary>
        /// 恢复系统默认值
        /// </summary>
        public void ResetDefaults()
        {
            CloudLayer = "CAD_NOTE_CLOUD";
            TextLayer = "CAD_NOTE_TEXT";
            ForceNonPlotting = true;
            CloudColorIndex = 1;
            TextColorIndex = 1;
            TextStyleName = "Standard";

            ArcLengthRatio = 6.0;
            BulgeCurvature = 0.520567;
            DefaultCloudType = 0;
            MinArcLength = 3.0;
            MaxArcLengthRatio = 30.0;
            CloudWidth = 0.0;
            CloudStyle = 1;

            TextHeightRatio = 3.5;
            ArrowSizeRatio = 2.5;
            LeaderType = 0;
            AutoNumberPrefix = "【";

            DefaultDiscipline = "建筑";
            DefaultAssignee = Environment.UserName;
            DefaultPriority = "重要";
            AutoZoomOnSelect = true;
            EnableSoundNotification = false;
            AutoStartCreateAfterSave = false;
            EnableSimpleNoteMode = true;
            RapidDefaultPhrase = "待修改";
            EnableRapidCmdKeySelection = true;
            CurrentTemplateId = "tpl_general";

            EnableAiSemantic = false;
            AiSimilarityThreshold = 0.60;
            AutoFillStandardCode = true;

            EnableAuditTrail = true;
            MaxNotesPerVolume = 50;
            AutoBackupOnSave = true;

            EnablePriorityColorLink = true;
            PriorityColorNormal = 3;
            PriorityColorImportant = 6;
            PriorityColorUrgent = 1;
            QuickReviewSnippets = new System.Collections.Generic.List<string>
            {
                "待修改",
                "请核对门窗洞口净尺寸与定位",
                "此处配筋与结构计算书不符",
                "管线与梁底标高冲突，请调整",
                "不满足消防疏散距离规范要求",
                "请补充细部构造做法与大样索引"
            };
            TeamMembers = new System.Collections.Generic.List<string>
            {
                Environment.UserName,
                "项目负责人",
                "专业审定人",
                "校对人",
                "设计人"
            };
        }
    }
}
