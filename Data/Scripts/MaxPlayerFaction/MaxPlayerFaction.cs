using Sandbox.ModAPI;
using VRage.Game;
using Sandbox.Game;
using VRageMath;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace Klime.MaxPlayerFaction
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class MaxPlayerFaction : MySessionComponentBase
    {
        private int maxplayers = 10; //EDIT THIS NUMBER TO WHAT YOU WANT MAX PLAYERS TO BE
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            base.Init(sessionComponent);
            MyAPIGateway.Session.Factions.FactionStateChanged += FactionsOnFactionStateChanged;
        }

        private void FactionsOnFactionStateChanged(MyFactionStateChange arg1, long arg2, long arg3, long arg4, long arg5)
        {
            var faction = MyAPIGateway.Session.Factions.TryGetFactionById(arg2);
            if (arg1 == MyFactionStateChange.FactionMemberAcceptJoin)
            {
                if (faction.Members.Count > maxplayers)
                {
                    if (MyAPIGateway.Session.Factions.TryGetPlayerFaction(arg4) == faction)
                    {
                        MyAPIGateway.Session.Factions.KickMember(arg2,arg4);
						MyVisualScriptLogicProvider.SendChatMessageColored("This Faction is full (10 Max)", Color.Green, "Server", arg4, "Green");
                    }
                }
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Session.Factions.FactionStateChanged -= FactionsOnFactionStateChanged;
        }
    }
}