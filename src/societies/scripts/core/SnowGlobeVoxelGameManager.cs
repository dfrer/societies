namespace Societies.Core
{
    /// <summary>Dedicated scene bootstrap for the finite SG-VX-01 catalog scenario.</summary>
    public partial class SnowGlobeVoxelGameManager : GameManager
    {
        public override void _Ready()
        {
            ConfigureScenarioStartup("snow_globe_voxel");
            base._Ready();
        }
    }
}
