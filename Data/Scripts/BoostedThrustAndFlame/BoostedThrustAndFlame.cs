using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;


namespace Klime.PlanePropThrustNerf
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class PlanePropThrustNerf : MySessionComponentBase
    {

        public override void LoadData()
        {
            foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyThrustDefinition thruster = def as MyThrustDefinition;
                if (thruster != null && thruster.Id.SubtypeName.ToLower().Contains("blade"))
                {
                    thruster.ForceMagnitude *= 1f;
                    thruster.MaxPowerConsumption *= 1f;
                }
            }
        }

        protected override void UnloadData()
        {
        
        }
    }
}