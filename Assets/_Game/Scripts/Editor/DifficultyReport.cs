using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BepJam.EditorTools
{
    public static class DifficultyReport
    {
        [MenuItem("BepJam/Report Difficulty")]
        public static void Run()
        {
            var report = new StringBuilder();
            foreach (string line in LevelGen.ReportHeader) report.AppendLine(line);
            int inBand = 0, shortfalls = 0;
            double maxGeneration = 0;
            foreach (var level in GameData.Levels)
            {
                var layout = LevelGen.Build(level);
                report.AppendLine(LevelGen.ReportLine(level, layout));
                if (LevelGen.InTargetBand(level, layout.randomFailRate)) inBand++;
                else if (LevelGen.AcceptableDifficulty(level, layout.randomFailRate)) shortfalls++;
                if (layout.generationMilliseconds > maxGeneration) maxGeneration = layout.generationMilliseconds;
            }
            report.AppendLine(LevelGen.ReportSummary(GameData.Levels.Count, inBand, shortfalls, maxGeneration));
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/difficulty_report.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
            Debug.Log("[BepJam] Difficulty report written to " + path + "\n" + report);
        }
    }
}
