using System;
using System.Security.Cryptography;
using System.Text;
using CadAutoCloudNote.Core.Models;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 链式 SHA256 防篡改审计服务
    /// </summary>
    public static class AuditService
    {
        public static AuditEntry CreateEntry(string previousHash, string action, string diffSummary, string operatorName)
        {
            DateTime now = DateTime.Now;
            string currentHash = ComputeHash(previousHash, action, diffSummary, now, operatorName);

            return new AuditEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Operator = string.IsNullOrEmpty(operatorName) ? Environment.UserName : operatorName,
                Timestamp = now,
                Action = action,
                DiffSummary = diffSummary,
                PreviousHash = previousHash ?? string.Empty,
                CurrentHash = currentHash
            };
        }

        public static string ComputeHash(string previousHash, string action, string diffSummary, DateTime timestamp, string operatorName)
        {
            string raw = string.Format("{0}|{1}|{2}|{3:o}|{4}", previousHash, action, diffSummary, timestamp, operatorName);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static bool VerifyChain(System.Collections.Generic.List<AuditEntry> entries)
        {
            if (entries == null || entries.Count == 0) return true;

            string expectedPrevHash = string.Empty;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.PreviousHash != expectedPrevHash)
                    return false;

                string computed = ComputeHash(e.PreviousHash, e.Action, e.DiffSummary, e.Timestamp, e.Operator);
                if (e.CurrentHash != computed)
                    return false;

                expectedPrevHash = e.CurrentHash;
            }

            return true;
        }
    }
}
