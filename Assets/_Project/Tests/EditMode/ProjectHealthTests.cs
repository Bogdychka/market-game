using System.Collections.Generic;
using Market.DebugTools.Editor;
using NUnit.Framework;

namespace Market.Tests
{
    public class ProjectHealthTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void MissingStableId_RejectsBlankValues(string value)
        {
            Assert.That(ProjectHealthRules.IsMissingStableId(value), Is.True);
        }

        [TestCase("tomato_basic", true)]
        [TestCase("tomato2", true)]
        [TestCase("Item_Tomato", false)]
        [TestCase("tomato basic", false)]
        public void LowerSnakeCase_ValidatesExpectedFormat(string value, bool expected)
        {
            Assert.That(ProjectHealthRules.IsLowerSnakeCase(value), Is.EqualTo(expected));
        }

        [Test]
        public void DuplicateKeys_ReturnsOnlyRepeatedIds()
        {
            HashSet<string> duplicates = ProjectHealthRules.FindDuplicateKeys(
                new[] { "apple", "bread", "apple", "carrot", "bread" });

            Assert.That(duplicates, Is.EquivalentTo(new[] { "apple", "bread" }));
        }

        [Test]
        public void ReportStatus_UsesHighestSeverity()
        {
            var report = new ProjectHealthReport();
            Assert.That(report.Status, Is.EqualTo(ProjectHealthStatus.Green));

            report.Add(Issue(ProjectHealthSeverity.Warning));
            Assert.That(report.Status, Is.EqualTo(ProjectHealthStatus.Yellow));

            report.Add(Issue(ProjectHealthSeverity.Error));
            Assert.That(report.Status, Is.EqualTo(ProjectHealthStatus.Red));
        }

        [TestCase(-0.01f, false)]
        [TestCase(0f, true)]
        [TestCase(1f, true)]
        public void NonNegative_RejectsNegativePrices(float value, bool expected)
        {
            Assert.That(ProjectHealthRules.IsNonNegative(value), Is.EqualTo(expected));
        }

        [Test]
        public void NullReferenceList_DetectsEmptyEntry()
        {
            Assert.That(
                ProjectHealthRules.HasNullReference(new object[] { new(), null }),
                Is.True);
        }

        [TestCase("Assets/_Project/Data/Item.asset", true)]
        [TestCase("Assets/ThirdParty/Item.asset", false)]
        [TestCase("Packages/com.example/file.cs", false)]
        public void ProjectAssetPath_StaysInsideOwnedRoot(string path, bool expected)
        {
            Assert.That(ProjectHealthRules.IsProjectAssetPath(path), Is.EqualTo(expected));
        }

        [Test]
        public void SerializedComponentSetting_DoesNotLeakAcrossYamlComponents()
        {
            const string yaml = "Terrain:\n  m_DrawInstanced: 1\n--- !u!4 &2\nTransform:\n  m_DrawInstanced: 0\n";

            Assert.That(
                ProjectHealthRules.SerializedComponentHasSetting(
                    yaml,
                    "Terrain",
                    "m_DrawInstanced",
                    "0"),
                Is.False);
        }

        [Test]
        public void SerializedComponentSetting_FindsTerrainRegression()
        {
            const string yaml = "Terrain:\n  m_ShadowCastingMode: 2\n--- !u!4 &2\nTransform:\n";

            Assert.That(
                ProjectHealthRules.SerializedComponentHasSetting(
                    yaml,
                    "Terrain",
                    "m_ShadowCastingMode",
                    "2"),
                Is.True);
        }

        [Test]
        public void SerializedComponentSetting_ChecksEveryTerrainComponent()
        {
            const string yaml = "Terrain:\n  m_DrawInstanced: 1\n--- !u!218 &2\nTerrain:\n  m_DrawInstanced: 0\n";

            Assert.That(
                ProjectHealthRules.SerializedComponentHasSetting(
                    yaml,
                    "Terrain",
                    "m_DrawInstanced",
                    "0"),
                Is.True);
        }

        [TestCase(5f, true)]
        [TestCase(10f, false)]
        [TestCase(15f, false)]
        public void SerializedComponentFloatBelow_EnforcesTerrainLodFloor(float value, bool expected)
        {
            string yaml = $"Terrain:\n  m_HeightmapPixelError: {value}\n";

            Assert.That(
                ProjectHealthRules.SerializedComponentFloatBelow(
                    yaml,
                    "Terrain",
                    "m_HeightmapPixelError",
                    10f),
                Is.EqualTo(expected));
        }

        private static ProjectHealthIssue Issue(ProjectHealthSeverity severity)
        {
            return new ProjectHealthIssue(
                severity,
                ProjectHealthCategory.ProjectRules,
                "Test",
                "Test issue",
                "Assets/_Project/Test.asset");
        }
    }
}
