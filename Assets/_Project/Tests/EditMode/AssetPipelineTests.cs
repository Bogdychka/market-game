using Market.DebugTools.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Market.Tests
{
    public class AssetPipelineTests
    {
        [TestCase("Cube", true)]
        [TestCase("Cube.001", true)]
        [TestCase("Object_12", true)]
        [TestCase("MarketCrate", false)]
        [TestCase("WateringCan", false)]
        public void GenericObjectName_DetectsBlenderDefaults(string value, bool expected)
        {
            Assert.That(AssetPipelineRules.IsGenericObjectName(value), Is.EqualTo(expected));
        }

        [TestCase(-1f, 1f, 1f, true)]
        [TestCase(0f, 1f, 1f, true)]
        [TestCase(1f, 1f, 1f, false)]
        public void InvalidScale_DetectsZeroAndNegativeAxes(
            float x,
            float y,
            float z,
            bool expected)
        {
            Assert.That(
                AssetPipelineRules.HasInvalidScale(new Vector3(x, y, z)),
                Is.EqualTo(expected));
        }

        [TestCase(0.01f, true)]
        [TestCase(0.5f, false)]
        [TestCase(8f, true)]
        public void SuspiciousSize_UsesSelectedProfile(float size, bool expected)
        {
            MarketAssetProfile profile = MarketAssetProfile.Get(AssetPipelineProfileId.StaticProp);
            Assert.That(AssetPipelineRules.IsSuspiciousSize(size, profile), Is.EqualTo(expected));
        }

        [Test]
        public void ReportStatus_UsesHighestSeverity()
        {
            var report = new AssetPipelineReport();
            Assert.That(report.Status, Is.EqualTo(AssetPipelineStatus.Ready));

            report.Add(AssetPipelineSeverity.Warning, "Warning", "Warning detail");
            Assert.That(report.Status, Is.EqualTo(AssetPipelineStatus.Warning));

            report.Add(AssetPipelineSeverity.Error, "Error", "Error detail");
            Assert.That(report.Status, Is.EqualTo(AssetPipelineStatus.Blocked));
        }

        [Test]
        public void CharacterProfile_DoesNotAllowStaticPreset()
        {
            MarketAssetProfile profile = MarketAssetProfile.Get(AssetPipelineProfileId.Character);
            Assert.That(profile.IsStatic, Is.False);
            Assert.That(profile.MinimumSize, Is.EqualTo(1f));
            Assert.That(profile.MaximumSize, Is.EqualTo(3f));
        }
    }
}
