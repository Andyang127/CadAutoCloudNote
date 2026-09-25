using System;
using System.Collections.Generic;
using CadAutoCloudNote.Core.Models;

namespace CadAutoCloudNote.Core.Services
{
    public class SearchMatchResult<T>
    {
        public T Item { get; set; }
        public double Score { get; set; }

        public SearchMatchResult(T item, double score)
        {
            Item = item;
            Score = score;
        }
    }

    /// <summary>
    /// 语义审查与模糊相似度检索服务（纯 C# 算法轻量化无依赖实现）
    /// </summary>
    public static class SemanticSearchService
    {
        public static List<SearchMatchResult<KnowledgeItem>> MatchKnowledgeBase(string query, string discipline, double threshold = 0.3)
        {
            List<SearchMatchResult<KnowledgeItem>> results = new List<SearchMatchResult<KnowledgeItem>>();
            if (string.IsNullOrEmpty(query)) return results;

            var allItems = KnowledgeBaseService.Search(null, discipline);
            foreach (var item in allItems)
            {
                double scoreTitle = ComputeSimilarity(query, item.Title);
                double scoreBody = ComputeSimilarity(query, item.Suggestion);
                double maxScore = Math.Max(scoreTitle, scoreBody);

                if (maxScore >= threshold)
                {
                    results.Add(new SearchMatchResult<KnowledgeItem>(item, maxScore));
                }
            }

            results.Sort((a, b) => b.Score.CompareTo(a.Score));
            return results;
        }

        public static List<SearchMatchResult<NoteRecord>> FindSimilarNotes(List<NoteRecord> notes, string query, double threshold = 0.4)
        {
            List<SearchMatchResult<NoteRecord>> results = new List<SearchMatchResult<NoteRecord>>();
            if (notes == null || string.IsNullOrEmpty(query)) return results;

            foreach (var note in notes)
            {
                double scoreTitle = ComputeSimilarity(query, note.Title);
                double scoreContent = ComputeSimilarity(query, note.Content);
                double maxScore = Math.Max(scoreTitle, scoreContent);

                if (maxScore >= threshold)
                {
                    results.Add(new SearchMatchResult<NoteRecord>(note, maxScore));
                }
            }

            results.Sort((a, b) => b.Score.CompareTo(a.Score));
            return results;
        }

        public static double ComputeSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;
            if (s1 == s2) return 1.0;

            // 基于 Bigram（2-gram）交并比系数 (Dice-Sørensen Coefficient)
            HashSet<string> bg1 = GetBigrams(s1);
            HashSet<string> bg2 = GetBigrams(s2);

            if (bg1.Count == 0 || bg2.Count == 0) return 0.0;

            int intersection = 0;
            foreach (string bg in bg1)
            {
                if (bg2.Contains(bg)) intersection++;
            }

            return (2.0 * intersection) / (bg1.Count + bg2.Count);
        }

        private static HashSet<string> GetBigrams(string s)
        {
            HashSet<string> bigrams = new HashSet<string>();
            for (int i = 0; i < s.Length - 1; i++)
            {
                bigrams.Add(s.Substring(i, 2));
            }
            return bigrams;
        }
    }
}
