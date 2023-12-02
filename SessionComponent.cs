using VRage.Game.Components;

namespace SERingAsteroids
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class SessionComponent : MySessionComponentBase
    {
        public override void SaveData()
        {
            RingConfig.SaveConfigs();
        }
    }
}
