using System;
using System.Diagnostics;

namespace CadAutoCloudNote.Tests
{
    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("==================================================================");
            Console.WriteLine(" CAD Auto CloudNote (CAD 云线批注) - 核心功能与 UI/UX 自动化测试套件");
            Console.WriteLine("==================================================================");

            int totalSuites = 0;
            int passedSuites = 0;
            int failedSuites = 0;

            Stopwatch totalSw = Stopwatch.StartNew();

            Action[] testSuites = new Action[]
            {
                Test_LandingLengthGeometry.RunTests,
                Test_RapidPhraseService.RunTests,
                Test_ProjectTemplateService.RunTests,
                Test_KnowledgeBaseService.RunTests,
                Test_ConfigAndSettings.RunTests,
                Test_UI_UX_Consistency.RunTests
            };

            foreach (var testSuite in testSuites)
            {
                totalSuites++;
                Console.WriteLine();
                try
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    testSuite.Invoke();
                    sw.Stop();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(string.Format("--> 套件执行成功! 耗时: {0} ms", sw.ElapsedMilliseconds));
                    Console.ResetColor();
                    passedSuites++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(string.Format("--> [FAIL] 测试套件执行失败: {0}", ex.Message));
                    Console.WriteLine(ex.StackTrace);
                    Console.ResetColor();
                    failedSuites++;
                }
            }

            totalSw.Stop();
            Console.WriteLine();
            Console.WriteLine("==================================================================");
            Console.WriteLine(string.Format(" 测试汇总: 总套件 {0} 个, 成功 {1} 个, 失败 {2} 个, 总耗时 {3} ms",
                totalSuites, passedSuites, failedSuites, totalSw.ElapsedMilliseconds));

            if (failedSuites == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" 所有测试全部通过! (ALL TESTS PASSED)");
                Console.ResetColor();
                Console.WriteLine("==================================================================");
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" 存在未通过的测试用例，请检查上述错误日志!");
                Console.ResetColor();
                Console.WriteLine("==================================================================");
                return 1;
            }
        }
    }
}
