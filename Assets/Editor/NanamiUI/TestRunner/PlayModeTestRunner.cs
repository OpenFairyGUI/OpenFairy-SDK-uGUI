using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace NanamiUI.EditorTools
{
    // 无头跑 PlayMode 测试并把结果写进 Temp/（不进 Assets、不触发导入）。
    // 回调经 [InitializeOnLoad] 每次域重载重新注册，故能跨"进 Play → 域重载 → 跑测试 → RunFinished"存活。
    // 用法：Tools/NanamiUI/Run PlayMode Tests（或 MCP ExecuteMenuItem），跑完读 Temp/NanamiUIPlayModeResults.txt。
    [InitializeOnLoad]
    public static class PlayModeTestRunner
    {
        public static readonly string ResultPath =
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "NanamiUIPlayModeResults.txt");

        static PlayModeTestRunner()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
        }

        [MenuItem("Tools/NanamiUI/Run PlayMode Tests")]
        public static void Run()
        {
            if (File.Exists(ResultPath))
                File.Delete(ResultPath);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode }));
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"OVERALL {result.TestStatus} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} duration={result.Duration:F1}s");
                Recurse(result, sb);
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
                File.WriteAllText(ResultPath, sb.ToString());
            }

            private static void Recurse(ITestResultAdaptor r, StringBuilder sb)
            {
                if (!r.HasChildren && !r.Test.IsSuite)
                    sb.AppendLine($"{r.TestStatus} {r.Test.FullName}{(r.TestStatus == TestStatus.Passed ? "" : " :: " + r.Message)}");
                if (r.Children != null)
                    foreach (var child in r.Children)
                        Recurse(child, sb);
            }
        }
    }
}
