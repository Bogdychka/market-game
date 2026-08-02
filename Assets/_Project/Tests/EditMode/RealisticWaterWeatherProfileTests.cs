using Market.DebugTools;
using NUnit.Framework;

namespace Market.Tests
{
    public sealed class RealisticWaterWeatherProfileTests
    {
        [Test]
        public void Profiles_IncreaseWaveAndFoamEnergyTowardStorm()
        {
            RealisticWaterWeatherProfile calm =
                RealisticWaterWeatherProfiles.Get(RealisticWaterWeather.Calm);
            RealisticWaterWeatherProfile breeze =
                RealisticWaterWeatherProfiles.Get(RealisticWaterWeather.Breeze);
            RealisticWaterWeatherProfile windy =
                RealisticWaterWeatherProfiles.Get(RealisticWaterWeather.Windy);
            RealisticWaterWeatherProfile storm =
                RealisticWaterWeatherProfiles.Get(RealisticWaterWeather.Storm);

            Assert.That(calm.Wave1Params.z, Is.LessThan(breeze.Wave1Params.z));
            Assert.That(breeze.Wave1Params.z, Is.LessThan(windy.Wave1Params.z));
            Assert.That(windy.Wave1Params.z, Is.LessThan(storm.Wave1Params.z));
            Assert.That(calm.FoamCrestStrength, Is.LessThan(breeze.FoamCrestStrength));
            Assert.That(breeze.FoamCrestStrength, Is.LessThan(windy.FoamCrestStrength));
            Assert.That(windy.FoamCrestStrength, Is.LessThan(storm.FoamCrestStrength));
        }

        [Test]
        public void Profiles_ReduceCausticsTowardStorm()
        {
            RealisticWaterWeatherProfile calm =
                RealisticWaterWeatherProfiles.Get(RealisticWaterWeather.Calm);
            RealisticWaterWeatherProfile storm =
                RealisticWaterWeatherProfiles.Get(RealisticWaterWeather.Storm);

            Assert.That(
                storm.SurfaceCausticIntensity,
                Is.LessThan(calm.SurfaceCausticIntensity));
            Assert.That(
                storm.ProjectedCausticIntensity,
                Is.LessThan(calm.ProjectedCausticIntensity));
        }

        [Test]
        public void Lerp_HalfwayBlendsCoordinatedProperties()
        {
            RealisticWaterWeatherProfile calm =
                RealisticWaterWeatherProfiles.Get(RealisticWaterWeather.Calm);
            RealisticWaterWeatherProfile storm =
                RealisticWaterWeatherProfiles.Get(RealisticWaterWeather.Storm);

            RealisticWaterWeatherProfile halfway =
                RealisticWaterWeatherProfile.Lerp(calm, storm, 0.5f);

            Assert.That(
                halfway.Wave1Params.z,
                Is.EqualTo((calm.Wave1Params.z + storm.Wave1Params.z) * 0.5f)
                    .Within(0.0001f));
            Assert.That(
                halfway.ProjectedCausticSpeeds.x,
                Is.EqualTo(
                    (calm.ProjectedCausticSpeeds.x +
                        storm.ProjectedCausticSpeeds.x) * 0.5f)
                    .Within(0.0001f));
        }
    }
}
