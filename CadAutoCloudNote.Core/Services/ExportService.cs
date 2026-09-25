using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 审查批注导出服务（支持 CSV 及原生 Excel XML Spreadsheet 标准无依赖导出）
    /// </summary>
    public static class ExportService
    {
        public static void ExportToCsv(List<NoteRecord> notes, string filePath)
        {
            if (notes == null) notes = new List<NoteRecord>();

            StringBuilder sb = new StringBuilder();
            // 写入表头 (含完整闭环流转答复字段)
            sb.AppendLine("序号,标题,专业,优先级,状态,创建人,批注者,最新回复人,最新回复时间,批注内容,最新整改答复");

            foreach (var n in notes)
            {
                string latestReplier = string.Empty;
                string latestReplyTime = string.Empty;
                string latestReplyContent = string.Empty;

                if (n.Replies != null && n.Replies.Count > 0)
                {
                    var r = n.Replies[n.Replies.Count - 1];
                    latestReplier = r.Author ?? string.Empty;
                    latestReplyTime = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    latestReplyContent = r.Content ?? string.Empty;
                }
                else if (n.AuditLogs != null && n.AuditLogs.Count > 0)
                {
                    for (int k = n.AuditLogs.Count - 1; k >= 0; k--)
                    {
                        var log = n.AuditLogs[k];
                        if (!string.IsNullOrEmpty(log.FieldChanges) || !string.IsNullOrEmpty(log.Action))
                        {
                            latestReplier = log.Operator ?? string.Empty;
                            latestReplyTime = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                            latestReplyContent = log.FieldChanges ?? log.Action;
                            break;
                        }
                    }
                }

                sb.AppendFormat("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                    n.SequenceNumber,
                    EscapeCsv(n.Title),
                    EscapeCsv(n.Discipline),
                    n.Priority.ToDisplayName(),
                    NoteLifecycleStateExtensions.ToDisplayName(n.State),
                    EscapeCsv(n.CreatedBy),
                    EscapeCsv(n.Assignee),
                    EscapeCsv(latestReplier),
                    EscapeCsv(latestReplyTime),
                    EscapeCsv(n.Content),
                    EscapeCsv(latestReplyContent));
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        public static void ExportToExcelXml(List<NoteRecord> notes, string filePath)
        {
            if (notes == null) notes = new List<NoteRecord>();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
            sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
            sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            sb.AppendLine(" <Styles>");
            sb.AppendLine("  <Style ss:ID=\"Header\">");
            sb.AppendLine("   <Font ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/>");
            sb.AppendLine("   <Interior ss:Color=\"#2B579A\" ss:Pattern=\"Solid\"/>");
            sb.AppendLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine("  <Style ss:ID=\"Data\">");
            sb.AppendLine("   <Alignment ss:Vertical=\"Center\" ss:WrapText=\"1\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine(" </Styles>");
            sb.AppendLine(" <Worksheet ss:Name=\"批注汇总清单\">");
            sb.AppendLine("  <Table>");
            sb.AppendLine("   <Column ss:Width=\"50\"/>");
            sb.AppendLine("   <Column ss:Width=\"140\"/>");
            sb.AppendLine("   <Column ss:Width=\"70\"/>");
            sb.AppendLine("   <Column ss:Width=\"60\"/>");
            sb.AppendLine("   <Column ss:Width=\"70\"/>");
            sb.AppendLine("   <Column ss:Width=\"80\"/>");
            sb.AppendLine("   <Column ss:Width=\"80\"/>");
            sb.AppendLine("   <Column ss:Width=\"120\"/>");
            sb.AppendLine("   <Column ss:Width=\"260\"/>");
            sb.AppendLine("   <Column ss:Width=\"260\"/>");
            sb.AppendLine("   <Column ss:Width=\"80\"/>");
            sb.AppendLine("   <Column ss:Width=\"120\"/>");

            // 表头行
            sb.AppendLine("   <Row ss:Height=\"24\" ss:StyleID=\"Header\">");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">序号</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">批注标题</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">专业</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">优先级</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">状态</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">创建人</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">批注者</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">创建时间</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">详细审查意见</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">最新整改答复/流转说明</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">答复人(使用者)</Data></Cell>");
            sb.AppendLine("    <Cell><Data ss:Type=\"String\">答复时间</Data></Cell>");
            sb.AppendLine("   </Row>");

            foreach (var n in notes)
            {
                string latestReplier = string.Empty;
                string latestReplyTime = string.Empty;
                string latestReplyContent = string.Empty;

                if (n.Replies != null && n.Replies.Count > 0)
                {
                    var r = n.Replies[n.Replies.Count - 1];
                    latestReplier = r.Author ?? string.Empty;
                    latestReplyTime = r.Timestamp.ToString("yyyy-MM-dd HH:mm");
                    latestReplyContent = r.Content ?? string.Empty;
                }
                else if (n.AuditLogs != null && n.AuditLogs.Count > 0)
                {
                    for (int k = n.AuditLogs.Count - 1; k >= 0; k--)
                    {
                        var log = n.AuditLogs[k];
                        if (!string.IsNullOrEmpty(log.FieldChanges) || !string.IsNullOrEmpty(log.Action))
                        {
                            latestReplier = log.Operator ?? string.Empty;
                            latestReplyTime = log.Timestamp.ToString("yyyy-MM-dd HH:mm");
                            latestReplyContent = log.FieldChanges ?? log.Action;
                            break;
                        }
                    }
                }

                sb.AppendLine("   <Row ss:Height=\"20\" ss:StyleID=\"Data\">");
                sb.AppendFormat("    <Cell><Data ss:Type=\"Number\">{0}</Data></Cell>\n", n.SequenceNumber);
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.Title));
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.Discipline));
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", n.Priority.ToDisplayName());
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", NoteLifecycleStateExtensions.ToDisplayName(n.State));
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.CreatedBy));
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.Assignee));
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", n.CreatedTime.ToString("yyyy-MM-dd HH:mm"));
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.Content));
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(latestReplyContent));
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(latestReplier));
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(latestReplyTime));
                sb.AppendLine("   </Row>");
            }

            sb.AppendLine("  </Table>");
            sb.AppendLine(" </Worksheet>");
            sb.AppendLine("</Workbook>");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 依据工程场景模板导出格式化专有 Excel XML 报告
        /// </summary>
        public static void ExportTemplateReport(List<NoteRecord> notes, string templateId, string filePath)
        {
            if (notes == null) notes = new List<NoteRecord>();
            var tpl = ProjectTemplateService.GetTemplate(templateId);

            string sheetName = tpl != null ? tpl.Name + "清单" : "批注汇总清单";
            string titleText = tpl != null ? string.Format("【{0}】差异与批注汇总表 ({1})", tpl.Name, tpl.Scenario) : "CAD 图纸批注汇总清单";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
            sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
            sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            sb.AppendLine(" <Styles>");
            sb.AppendLine("  <Style ss:ID=\"Header\">");
            sb.AppendLine("   <Font ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/>");
            sb.AppendLine("   <Interior ss:Color=\"#1B3552\" ss:Pattern=\"Solid\"/>");
            sb.AppendLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine("  <Style ss:ID=\"Data\">");
            sb.AppendLine("   <Alignment ss:Vertical=\"Center\" ss:WrapText=\"1\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine(" </Styles>");
            sb.AppendLine(string.Format(" <Worksheet ss:Name=\"{0}\">", EscapeXml(sheetName)));
            sb.AppendLine("  <Table>");

            // 依据不同场景定义专有列宽与表头
            List<string> headers = new List<string>();
            if (templateId == "tpl_asbuilt")
            {
                // 竣工图模板
                headers = new List<string> { "序号", "竣工编号", "涉及专业", "变更类型", "依据文件/洽商号", "现场实际差异与批注描述", "状态", "编制人", "编制日期" };
                sb.AppendLine("   <Column ss:Width=\"45\"/><Column ss:Width=\"75\"/><Column ss:Width=\"60\"/><Column ss:Width=\"90\"/><Column ss:Width=\"110\"/><Column ss:Width=\"320\"/><Column ss:Width=\"70\"/><Column ss:Width=\"70\"/><Column ss:Width=\"110\"/>");
            }
            else if (templateId == "tpl_audit")
            {
                // 施工图审图模板
                headers = new List<string> { "序号", "审图编号", "涉及专业", "问题分类", "规范条文依据", "审查意见与整改要求", "销项状态", "审查人", "审查日期" };
                sb.AppendLine("   <Column ss:Width=\"45\"/><Column ss:Width=\"75\"/><Column ss:Width=\"60\"/><Column ss:Width=\"90\"/><Column ss:Width=\"130\"/><Column ss:Width=\"300\"/><Column ss:Width=\"70\"/><Column ss:Width=\"70\"/><Column ss:Width=\"110\"/>");
            }
            else if (templateId == "tpl_check")
            {
                // 内部校审
                headers = new List<string> { "序号", "校对编号", "涉及专业", "校对阶段", "问题性质", "内部校对批注意见", "整改状态", "校对人", "校对日期" };
                sb.AppendLine("   <Column ss:Width=\"45\"/><Column ss:Width=\"75\"/><Column ss:Width=\"60\"/><Column ss:Width=\"80\"/><Column ss:Width=\"90\"/><Column ss:Width=\"320\"/><Column ss:Width=\"70\"/><Column ss:Width=\"70\"/><Column ss:Width=\"110\"/>");
            }
            else if (templateId == "tpl_meeting")
            {
                // 图纸会审
                headers = new List<string> { "序号", "会审编号", "提出单位", "问题类型", "会审纪要编号", "会审澄清疑问与设计答复", "处理状态", "提出人", "提出日期" };
                sb.AppendLine("   <Column ss:Width=\"45\"/><Column ss:Width=\"75\"/><Column ss:Width=\"80\"/><Column ss:Width=\"90\"/><Column ss:Width=\"110\"/><Column ss:Width=\"320\"/><Column ss:Width=\"70\"/><Column ss:Width=\"70\"/><Column ss:Width=\"110\"/>");
            }
            else if (templateId == "tpl_change")
            {
                // 变更洽商
                headers = new List<string> { "序号", "变更编号", "变更/洽商编号", "变更性质", "涉及专业", "变更设计内容及附图标记", "签发状态", "编制人", "签发日期" };
                sb.AppendLine("   <Column ss:Width=\"45\"/><Column ss:Width=\"75\"/><Column ss:Width=\"110\"/><Column ss:Width=\"90\"/><Column ss:Width=\"60\"/><Column ss:Width=\"320\"/><Column ss:Width=\"70\"/><Column ss:Width=\"70\"/><Column ss:Width=\"110\"/>");
            }
            else if (templateId == "tpl_construct")
            {
                // 施工交底
                headers = new List<string> { "序号", "交底编号", "施工分区", "工序类型", "风险等级", "现场技术交底与管控要求", "交底人", "交底日期" };
                sb.AppendLine("   <Column ss:Width=\"45\"/><Column ss:Width=\"75\"/><Column ss:Width=\"90\"/><Column ss:Width=\"90\"/><Column ss:Width=\"70\"/><Column ss:Width=\"350\"/><Column ss:Width=\"70\"/><Column ss:Width=\"110\"/>");
            }
            else if (templateId == "tpl_supervise")
            {
                // 监理巡查
                headers = new List<string> { "序号", "监理编号", "巡查部位", "问题等级", "监理巡查问题与整改要求", "整改期限", "整改状态", "监理工程师", "巡查日期" };
                sb.AppendLine("   <Column ss:Width=\"45\"/><Column ss:Width=\"75\"/><Column ss:Width=\"90\"/><Column ss:Width=\"70\"/><Column ss:Width=\"320\"/><Column ss:Width=\"90\"/><Column ss:Width=\"70\"/><Column ss:Width=\"75\"/><Column ss:Width=\"110\"/>");
            }
            else if (templateId == "tpl_clash")
            {
                // BIM管综
                headers = new List<string> { "序号", "碰撞编号", "碰撞专业", "碰撞类型", "管线碰撞分析与避让调整方案", "处理状态", "管综设计师", "记录日期" };
                sb.AppendLine("   <Column ss:Width=\"45\"/><Column ss:Width=\"75\"/><Column ss:Width=\"90\"/><Column ss:Width=\"90\"/><Column ss:Width=\"350\"/><Column ss:Width=\"70\"/><Column ss:Width=\"75\"/><Column ss:Width=\"110\"/>");
            }
            else
            {
                // 通用
                headers = new List<string> { "序号", "批注编号", "涉及专业", "优先级", "批注标题", "详细审查批注意见", "流转状态", "批注者", "创建时间", "最新整改答复", "答复人(使用者)" };
                sb.AppendLine("   <Column ss:Width=\"45\"/><Column ss:Width=\"75\"/><Column ss:Width=\"60\"/><Column ss:Width=\"60\"/><Column ss:Width=\"120\"/><Column ss:Width=\"260\"/><Column ss:Width=\"70\"/><Column ss:Width=\"70\"/><Column ss:Width=\"110\"/><Column ss:Width=\"260\"/><Column ss:Width=\"80\"/>");
            }

            // 表头行
            sb.AppendLine("   <Row ss:Height=\"24\" ss:StyleID=\"Header\">");
            foreach (var h in headers)
            {
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(h));
            }
            sb.AppendLine("   </Row>");

            // 数据行
            string prefix = tpl?.Prefix ?? "#";
            foreach (var n in notes)
            {
                string latestReplier = string.Empty;
                string latestReplyContent = string.Empty;
                if (n.Replies != null && n.Replies.Count > 0)
                {
                    var r = n.Replies[n.Replies.Count - 1];
                    latestReplier = r.Author ?? string.Empty;
                    latestReplyContent = r.Content ?? string.Empty;
                }
                else if (n.AuditLogs != null && n.AuditLogs.Count > 0)
                {
                    for (int k = n.AuditLogs.Count - 1; k >= 0; k--)
                    {
                        var log = n.AuditLogs[k];
                        if (!string.IsNullOrEmpty(log.FieldChanges) || !string.IsNullOrEmpty(log.Action))
                        {
                            latestReplier = log.Operator ?? string.Empty;
                            latestReplyContent = log.FieldChanges ?? log.Action;
                            break;
                        }
                    }
                }

                sb.AppendLine("   <Row ss:Height=\"22\" ss:StyleID=\"Data\">");
                sb.AppendFormat("    <Cell><Data ss:Type=\"Number\">{0}</Data></Cell>\n", n.SequenceNumber);
                string noteCode = string.Format("{0}{1:D3}", prefix, n.SequenceNumber);
                sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(noteCode));

                if (templateId == "tpl_asbuilt")
                {
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.Discipline));
                    string changeType = n.MetadataFields.ContainsKey("变更类型") ? n.MetadataFields["变更类型"] : "现场工程洽商(C类)";
                    string docNo = n.MetadataFields.ContainsKey("依据文件") ? n.MetadataFields["依据文件"] : "详见洽商记录";
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(changeType));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(docNo));
                    string detail = !string.IsNullOrEmpty(n.Content) ? n.Content : n.Title;
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(detail));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", NoteLifecycleStateExtensions.ToDisplayName(n.State));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.CreatedBy));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", n.CreatedTime.ToString("yyyy-MM-dd HH:mm"));
                }
                else if (templateId == "tpl_audit")
                {
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.Discipline));
                    string probType = n.MetadataFields.ContainsKey("问题类型") ? n.MetadataFields["问题类型"] : "强条与合规审查";
                    string stdCode = n.MetadataFields.ContainsKey("规范条文") ? n.MetadataFields["规范条文"] : "现行国家强制性工程建设标准";
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(probType));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(stdCode));
                    string detail = !string.IsNullOrEmpty(n.Content) ? n.Content : n.Title;
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(detail));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", NoteLifecycleStateExtensions.ToDisplayName(n.State));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.CreatedBy));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", n.CreatedTime.ToString("yyyy-MM-dd HH:mm"));
                }
                else
                {
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.Discipline));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", n.Priority.ToDisplayName());
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.Title));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(!string.IsNullOrEmpty(n.Content) ? n.Content : n.Title));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", NoteLifecycleStateExtensions.ToDisplayName(n.State));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(n.Assignee));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", n.CreatedTime.ToString("yyyy-MM-dd HH:mm"));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(latestReplyContent));
                    sb.AppendFormat("    <Cell><Data ss:Type=\"String\">{0}</Data></Cell>\n", EscapeXml(latestReplier));
                }

                sb.AppendLine("   </Row>");
            }

            sb.AppendLine("  </Table>");
            sb.AppendLine(" </Worksheet>");
            sb.AppendLine("</Workbook>");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string str)
        {
            if (string.IsNullOrEmpty(str)) return "\"\"";
            return "\"" + str.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        private static string EscapeXml(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("&", "&amp;")
                      .Replace("<", "&lt;")
                      .Replace(">", "&gt;")
                      .Replace("\"", "&quot;")
                      .Replace("'", "&apos;");
        }
    }
}
