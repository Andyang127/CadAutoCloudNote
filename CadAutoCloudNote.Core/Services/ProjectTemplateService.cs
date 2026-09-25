using System;
using System.Collections.Generic;
using System.Linq;
using CadAutoCloudNote.Core.Config;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 批注使用对象（使用本工具的人）与模板规则定义
    /// </summary>
    public class AnnotationTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string RoleTitle { get; set; }
        public string Scenario { get; set; }
        public string Description { get; set; }
        public string LayerName { get; set; }
        public string TextLayerName => (Id == "tpl_general" || string.IsNullOrEmpty(LayerName)) ? "CAD_NOTE_TEXT" : LayerName + "_TEXT";
        public int ColorIndex { get; set; }
        public string Prefix { get; set; }
        public bool ForceNonPlotting { get; set; }
        public bool IsNonPlottingLocked { get; set; }
        public List<string> ApplicableDisciplines { get; set; } = new List<string>();
        public List<string> DedicatedFields { get; set; } = new List<string>();
        public List<string> QuickPhrases { get; set; } = new List<string>();
        public List<string> QuickReviewSnippets
        {
            get => QuickPhrases;
            set => QuickPhrases = value;
        }

        public bool IsDisciplineApplicable(string discipline)
        {
            if (string.IsNullOrEmpty(discipline)) return true;
            if (discipline == "通用") return true;
            if (ApplicableDisciplines == null || ApplicableDisciplines.Count == 0) return true;
            return ApplicableDisciplines.Any(d => string.Equals(d, discipline, StringComparison.OrdinalIgnoreCase));
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// 批注使用对象（使用本工具的人）与模板引擎服务
    /// 支持审图机构专家、设计院校对人、会审代表、变更工程师、竣工图归档员、施工技术员、监理工程师、BIM管综工程师、通用设计人
    /// </summary>
    public static class ProjectTemplateService
    {
        private static readonly List<AnnotationTemplate> _templates = new List<AnnotationTemplate>();
        private static string _currentTemplateId = "tpl_general";

        static ProjectTemplateService()
        {
            InitTemplates();
        }

        public static List<AnnotationTemplate> GetAllTemplates()
        {
            return new List<AnnotationTemplate>(_templates);
        }

        public static AnnotationTemplate GetTemplate(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return _templates[0];
            return _templates.Find(t => string.Equals(t.Id, templateId, StringComparison.OrdinalIgnoreCase)) ?? _templates[0];
        }

        public static AnnotationTemplate CurrentTemplate
        {
            get
            {
                var settings = Config.ConfigManager.Instance.CurrentSettings;
                return GetTemplate(settings?.CurrentTemplateId ?? _currentTemplateId);
            }
            set
            {
                if (value != null)
                {
                    _currentTemplateId = value.Id;
                    var settings = Config.ConfigManager.Instance.CurrentSettings;
                    if (settings != null) settings.CurrentTemplateId = value.Id;
                }
            }
        }

        public static bool ApplyTemplateToSettings(string templateId, NoteSettings settings)
        {
            if (settings == null) return false;
            var tpl = GetTemplate(templateId);
            if (tpl == null) return false;

            settings.CurrentTemplateId = tpl.Id;
            settings.CloudLayer = tpl.LayerName;
            settings.TextLayer = tpl.TextLayerName;
            settings.CloudColorIndex = tpl.ColorIndex;
            settings.AutoNumberPrefix = tpl.Prefix;
            settings.ForceNonPlotting = tpl.ForceNonPlotting;

            if (tpl.QuickPhrases != null && tpl.QuickPhrases.Count > 0)
            {
                settings.RapidDefaultPhrase = tpl.QuickPhrases[0];
                settings.QuickReviewSnippets = new List<string>(tpl.QuickPhrases);
            }

            return true;
        }

        private static void InitTemplates()
        {
            // 0. 通用工程 (默认日常)
            _templates.Add(new AnnotationTemplate
            {
                Id = "tpl_general",
                Name = "通用工程",
                RoleTitle = "通用工程",
                Scenario = "日常设计与综合校对",
                Description = "通用标准模式，兼容各类工程图纸日常标记与校核沟通",
                LayerName = "CAD_NOTE_CLOUD",
                ColorIndex = 1, // 红色 (开箱默认工程红，对齐默认优先级)
                Prefix = "【",
                ForceNonPlotting = false,
                IsNonPlottingLocked = false,
                ApplicableDisciplines = new List<string> { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" },
                DedicatedFields = new List<string> { "批注编号", "涉及专业", "批注者", "优先级", "批注内容" },
                QuickPhrases = new List<string>
                {
                    "待修改",
                    "请核实",
                    "请补充节点详图",
                    "尺寸标注缺失，请完善",
                    "标高存在冲突，请复核"
                }
            });

            // 1. 施工审图
            _templates.Add(new AnnotationTemplate
            {
                Id = "tpl_audit",
                Name = "施工审图",
                RoleTitle = "施工审图",
                Scenario = "审图机构 / 正式施工图审查",
                Description = "依据《建设工程质量管理条例》，针对强条合规、安全疏散、防火等逐条销项审查",
                LayerName = "AUDIT-NOTE",
                ColorIndex = 6, // 洋红
                Prefix = "AUD-",
                ForceNonPlotting = false,
                IsNonPlottingLocked = false,
                ApplicableDisciplines = new List<string> { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" },
                DedicatedFields = new List<string> { "批注编号", "涉及专业", "问题类型", "规范条文号", "状态", "对应图号" },
                QuickPhrases = new List<string>
                {
                    "不满足规范要求，请复核修改",
                    "疏散宽度不满足规范限值，请调整",
                    "尺寸标注缺失，请核对完善",
                    "标高存在冲突，请复核",
                    "防火分区面积超限，请重新划分",
                    "此处表达不清，请补充节点详图",
                    "与其他专业图纸冲突，请协调"
                }
            });

            // 2. 内部校审 (强制不打印)
            _templates.Add(new AnnotationTemplate
            {
                Id = "tpl_check",
                Name = "内部校审",
                RoleTitle = "内部校审",
                Scenario = "设计院内部三校两审",
                Description = "院内初校、复校、终审流程闭环，重点排查制图错误与计算书不符；出图强制不打印",
                LayerName = "INTERNAL-CHECK-NOTE",
                ColorIndex = 30, // 橙色
                Prefix = "CHK-",
                ForceNonPlotting = true, // 默认关闭打印
                IsNonPlottingLocked = true,
                ApplicableDisciplines = new List<string> { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" },
                DedicatedFields = new List<string> { "批注编号", "校对阶段", "涉及专业", "问题分类", "整改状态", "校对人", "制图人" },
                QuickPhrases = new List<string>
                {
                    "本大样与平面尺寸不匹配，请核对统一",
                    "图例缺失，请补充图纸图例表",
                    "轴线编号重复，请修正",
                    "材料说明前后不一致，请统一",
                    "计算书参数与图纸不匹配，请复核",
                    "图名图号与目录不匹配，请修正",
                    "缺少定位轴线，请补充"
                }
            });

            // 3. 多方会审
            _templates.Add(new AnnotationTemplate
            {
                Id = "tpl_meeting",
                Name = "多方会审",
                RoleTitle = "多方会审",
                Scenario = "建设/设计/监理/施工多方会审",
                Description = "用于会审疑问澄清、施工可行性研讨与答复跟踪，可打印作为纪要附图",
                LayerName = "MEETING-REVIEW-NOTE",
                ColorIndex = 2, // 黄色
                Prefix = "MR-",
                ForceNonPlotting = false,
                IsNonPlottingLocked = false,
                ApplicableDisciplines = new List<string> { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" },
                DedicatedFields = new List<string> { "批注编号", "提出单位", "问题类型", "会审纪要编号", "答复状态", "对应图号" },
                QuickPhrases = new List<string>
                {
                    "此处构造做法，请设计予以澄清",
                    "本构件现场施工难度大，请设计优化方案",
                    "管线空间不足，请各专业协调调整",
                    "图纸做法与现场地质条件不匹配，请复核",
                    "请设计明确本节点施工工艺要求",
                    "此项纳入图纸会审纪要",
                    "预留洞口定位，请进一步明确"
                }
            });

            // 4. 设计变更
            _templates.Add(new AnnotationTemplate
            {
                Id = "tpl_change",
                Name = "设计变更",
                RoleTitle = "设计变更",
                Scenario = "设计变更单 / 工程洽商记录附图",
                Description = "云线框选变更范围并关联变更单号，正式签发后锁定元数据，可作为变更单附件附图",
                LayerName = "CHANGE-NOTE",
                ColorIndex = 6, // 洋红
                Prefix = "CHG-",
                ForceNonPlotting = false,
                IsNonPlottingLocked = false,
                ApplicableDisciplines = new List<string> { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" },
                DedicatedFields = new List<string> { "批注编号", "变更/洽商编号", "变更性质", "涉及专业", "状态", "对应原图号" },
                QuickPhrases = new List<string>
                {
                    "本范围依据变更文件，取消原有构件",
                    "新增构件，详见变更附图",
                    "构件尺寸调整，原设计此部分作废",
                    "材料规格调整，其余图纸内容不变",
                    "本位置按工程洽商调整",
                    "标高调整，详见变更文件",
                    "管道路由修改，按变更实施"
                }
            });

            // 5. 竣工归档 (蓝图不打印)
            _templates.Add(new AnnotationTemplate
            {
                Id = "tpl_asbuilt",
                Name = "竣工归档",
                RoleTitle = "竣工归档",
                Scenario = "施工单位 / 竣工图差异记录与归档",
                Description = "对照原施工图记录现场实际调整、变更单与洽商依据，导出差异汇总表；正式交付蓝图强制不打印",
                LayerName = "AS-BUILD-NOTE",
                ColorIndex = 6, // 洋红
                Prefix = "AS-",
                ForceNonPlotting = true, // 交付蓝图默认关闭打印
                IsNonPlottingLocked = true,
                ApplicableDisciplines = new List<string> { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" },
                DedicatedFields = new List<string> { "批注编号", "变更类型(B/C/D类)", "依据文件编号", "涉及专业", "状态", "原施工图图号" },
                QuickPhrases = new List<string>
                {
                    "本位置按洽商现场调整，与原施工图不一致",
                    "此处按设计变更修改，以现场实际施工为准",
                    "原设计取消，现场未施工",
                    "原设计增加该项内容，详见洽商记录",
                    "构件位置偏移，现场实测定位见本竣工图",
                    "原图纸尺寸有误，现场已按实际调整",
                    "标高调整，以现场竣工实测标高为准"
                }
            });

            // 6. 施工现场 (主要针对建筑、结构)
            _templates.Add(new AnnotationTemplate
            {
                Id = "tpl_construct",
                Name = "施工现场",
                RoleTitle = "施工现场",
                Scenario = "总承包 / 现场技术交底与工序提醒",
                Description = "重点标明施工分区、危险源管控、关键工序与旁站节点，可打印下发班组",
                LayerName = "CONSTRUCTION-NOTE",
                ColorIndex = 3, // 绿色
                Prefix = "CON-",
                ForceNonPlotting = false,
                IsNonPlottingLocked = false,
                ApplicableDisciplines = new List<string> { "建筑", "结构", "通用" },
                DedicatedFields = new List<string> { "批注编号", "施工分区", "工序类型", "风险等级", "交底人", "对应图号" },
                QuickPhrases = new List<string>
                {
                    "本区域为高支模范围，专项方案已审批",
                    "此处钢筋为重点管控节点，现场需旁站",
                    "该位置预留孔洞，施工时注意保护",
                    "管线交叉位置，按此避让方案施工",
                    "本构件需第三方检测，做好试件留置",
                    "此处为沉降观测点，施工做好保护",
                    "本节点属于关键工序，验收合格方可隐蔽"
                }
            });

            // 7. 工程监理 (总图不相关置灰)
            _templates.Add(new AnnotationTemplate
            {
                Id = "tpl_supervise",
                Name = "工程监理",
                RoleTitle = "工程监理",
                Scenario = "工程监理 / 现场巡查与通知单附图",
                Description = "对照图纸记录质量通病、施工偏差、材料核对并跟踪整改期限闭环，可作为监理通知附件",
                LayerName = "SUPERVISOR-NOTE",
                ColorIndex = 5, // 蓝色
                Prefix = "SUP-",
                ForceNonPlotting = false,
                IsNonPlottingLocked = false,
                ApplicableDisciplines = new List<string> { "建筑", "结构", "给排水", "暖通", "电气", "通用" },
                DedicatedFields = new List<string> { "批注编号", "问题等级", "巡查部位", "整改期限", "整改状态", "监理工程师" },
                QuickPhrases = new List<string>
                {
                    "现场做法与图纸不符，请施工单位核实",
                    "该节点施工质量需重点检查验收",
                    "现场构件尺寸偏差，请复核整改",
                    "材料与图纸设计型号不一致，请确认",
                    "本部位隐蔽验收前需通知监理到场",
                    "现场管线排布与图纸不一致，请整改",
                    "施工偏差超标，请校正"
                }
            });

            // 8. BIM管综 (针对暖通/给排水/电气/建筑，结构/总图不相关置灰)
            _templates.Add(new AnnotationTemplate
            {
                Id = "tpl_clash",
                Name = "BIM管综",
                RoleTitle = "BIM管综",
                Scenario = "机电深化 / BIM 管综碰撞分析与净空核查",
                Description = "用于机电综合深化图纸标记管线硬碰撞、检修空间不足与标高冲突，指导管线避让",
                LayerName = "CLASH-NOTE",
                ColorIndex = 210, // 紫色
                Prefix = "CLASH-",
                ForceNonPlotting = false,
                IsNonPlottingLocked = false,
                ApplicableDisciplines = new List<string> { "给排水", "暖通", "电气", "建筑", "通用" },
                DedicatedFields = new List<string> { "批注编号", "碰撞专业", "碰撞类型", "处理状态", "创建人", "对应图号" },
                QuickPhrases = new List<string>
                {
                    "给排水管与桥架碰撞，请调整路由",
                    "管线净空不足，不满足吊顶标高要求",
                    "管道与结构梁冲突，请优化标高",
                    "管线检修空间不足，请调整排布",
                    "风管与喷淋管硬碰撞，需移位",
                    "桥架与消火栓位置冲突，请协调",
                    "此处管线密集，综合排布优化"
                }
            });
        }
    }
}
