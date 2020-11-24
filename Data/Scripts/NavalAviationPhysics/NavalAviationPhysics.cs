using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using Jakaria;
using System;
using Sandbox.Game.Entities;
using Draygo.API;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using System.Text;
using VRage.ModAPI;
using Sandbox.Definitions;

namespace Klime.NavalAviationPhysics
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class NavalAviationPhysics : MySessionComponentBase
    {
        WaterModAPI api = new WaterModAPI();
        List<Water> all_water = new List<Water>();
        List<IMyPlayer> all_players = new List<IMyPlayer>();
        IMyShipController reuse_cockpit;
        IMyCharacter reuse_character;
        MyPlanet closest_planet;
        bool is_in_planet = false;
        bool valid_cockpit = false;

        HudAPIv2 HUD_Base;
        HudAPIv2.HUDMessage water_alt_message;
        StringBuilder water_alt_sb = new StringBuilder("");
        HudAPIv2.HUDMessage max_speed_message;
        StringBuilder max_speed_sb = new StringBuilder("");

        List<MyCubeGrid> all_grids = new List<MyCubeGrid>();
        MyCubeGrid reuse_cubegrid;
        List<IMyCubeGrid> reuse_gridgroup = new List<IMyCubeGrid>();
        Vector3 reuse_finalspeed = Vector3.Zero;
        Vector3 reuse_initialspeed = Vector3.Zero;

        float minimum_mass = 1000;
        float maximum_mass = 1500000;
        float base_speed = 0.7f; //Multiplied by world max
        float max_additional_speed = 0.3f; //Multiplied by world max

        float max_angular_world = 2;
        float min_angular_world = 0.2f;
        float max_additional_ang = 1.8f;

        Vector3 reuse_initialang = Vector3.Zero;
        Vector3 reuse_finalang = Vector3.Zero;
        Vector3 reuse_visual_speed = Vector3.Zero;


        ushort netid = 21194;
        int timer = 0;

        ActionEnt reuse_actionent;
        List<ActionEnt> all_actionents = new List<ActionEnt>();
        List<ActionEnt> clear_actionents = new List<ActionEnt>();
        public class ActionEnt
        {
            public MyCubeGrid grid;
            public int action_time;

            public ActionEnt(MyCubeGrid grid, int action_time)
            {
                this.grid = grid;
                this.action_time = action_time;
            }
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                api.Register("NavalAviationPhysics");
                api.RecievedData += Api_RecievedData;
                MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
                MyAPIGateway.Entities.OnEntityRemove += Entities_OnEntityRemove;
            }
            MyAPIGateway.Multiplayer.RegisterMessageHandler(netid, water_handler);
        }

        private void Entities_OnEntityRemove(IMyEntity obj)
        {
            reuse_cubegrid = obj as MyCubeGrid;
            if (reuse_cubegrid != null && reuse_cubegrid.Physics != null && reuse_cubegrid.GridSizeEnum == MyCubeSize.Small)
            {
                all_grids.Remove(reuse_cubegrid);
            }
        }

        private void Entities_OnEntityAdd(IMyEntity obj)
        {
            reuse_cubegrid = obj as MyCubeGrid;
            if (reuse_cubegrid != null && reuse_cubegrid.Physics != null && reuse_cubegrid.GridSizeEnum == MyCubeSize.Small)
            {
                ActionEnt actionent = new ActionEnt(reuse_cubegrid, timer + 5);
                all_actionents.Add(actionent);
            }
        }

        public override void BeforeStart()
        {
            HUD_Base = new HudAPIv2(HUD_Init_Complete);
            base_speed *= MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
            max_additional_speed *= MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
        }

        private void HUD_Init_Complete()
        {
            water_alt_message = new HudAPIv2.HUDMessage(water_alt_sb, new Vector2D(0, 0), null,-1, 1, true, false, null, BlendTypeEnum.PostPP, "white");
            water_alt_message.InitialColor = Color.Orange;
            water_alt_message.Scale = 1;
            water_alt_message.Origin = new Vector2D(0, -0.08);

            max_speed_message = new HudAPIv2.HUDMessage(max_speed_sb, new Vector2D(0, 0), null, -1, 1, true, false, null, BlendTypeEnum.PostPP, "white");
            max_speed_message.InitialColor = Color.Red;
            max_speed_message.Scale = 1;
            max_speed_message.Origin = new Vector2D(-0.688,-0.715);
        }

        private void Api_RecievedData()
        {
            if (api.Registered)
            {
                all_water = api.Waters;
                if (all_water != null)
                {
                    all_players.Clear();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(all_players);
                    foreach (var player in all_players)
                    {
                        MyAPIGateway.Multiplayer.SendMessageTo(netid, MyAPIGateway.Utilities.SerializeToBinary(all_water), player.SteamUserId);
                    }
                }
            }
        }

        private void water_handler(byte[] obj)
        {
            if (!MyAPIGateway.Session.IsServer)
            {
                all_water = MyAPIGateway.Utilities.SerializeFromBinary<List<Water>>(obj);
            }
        }

        private void GetMaxSpeed(ref float current_mass)
        {
            float max = base_speed + MathHelper.Lerp(max_additional_speed,0, (MathHelper.Clamp(current_mass,minimum_mass,maximum_mass) - minimum_mass)/(maximum_mass-minimum_mass));
            reuse_visual_speed = Vector3.Normalize(reuse_initialspeed) * max;
            if (reuse_initialspeed.Length() >= max)
            {
                reuse_finalspeed = Vector3.Normalize(reuse_initialspeed) * max;
            }
            else
            {
                reuse_finalspeed = reuse_initialspeed;
            }
        }

        private void GetMaxAng(ref float current_mass)
        {
            float ang = min_angular_world;
            if (reuse_finalspeed.Length() <= base_speed)
            {
                ang = MathHelper.Lerp(0, max_additional_ang, reuse_finalspeed.Length() / base_speed);
                ang = min_angular_world + MathHelper.Lerp(ang, 0, (MathHelper.Clamp(current_mass, minimum_mass, maximum_mass) - minimum_mass) / (maximum_mass - minimum_mass));
            }
            else
            {
                ang = MathHelper.Lerp(max_additional_ang, 0, (reuse_finalspeed.Length() - base_speed)/ max_additional_speed);
                ang = min_angular_world + MathHelper.Lerp(ang, 0, (MathHelper.Clamp(current_mass, minimum_mass, maximum_mass) - minimum_mass) / (maximum_mass - minimum_mass));
            }
            //MyAPIGateway.Utilities.ShowNotification(ang.ToString(), 1, "White");
            if (reuse_initialang.Length() >= ang)
            {
                reuse_finalang = Vector3.Normalize(reuse_initialang) * ang;
            }
            else
            {
                reuse_finalang = reuse_initialang;
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                if (timer % 120 == 0 && all_water.Count > 0)
                {
                    all_players.Clear();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(all_players);
                    foreach (var player in all_players)
                    {
                        MyAPIGateway.Multiplayer.SendMessageTo(netid, MyAPIGateway.Utilities.SerializeToBinary(all_water), player.SteamUserId);
                    }
                }

                clear_actionents.Clear();
                foreach (var ent in all_actionents)
                {
                    if (ent.grid != null && !ent.grid.MarkedForClose && ent.grid.Physics != null)
                    {
                        if (ent.action_time == timer)
                        {
                            reuse_gridgroup.Clear();
                            reuse_gridgroup = MyAPIGateway.GridGroups.GetGroup(ent.grid, GridLinkTypeEnum.Physical);
                            bool valid_grid = true;
                            foreach (var grid in reuse_gridgroup)
                            {
                                if (grid.GridSizeEnum == MyCubeSize.Large)
                                {
                                    valid_grid = false;
                                    break;
                                }
                            }

                            if (valid_grid && ent.grid.BlocksCount > 10)
                            {
                                all_grids.Add(ent.grid);
                            }
                            clear_actionents.Add(ent);
                        }
                    }
                    else
                    {
                        clear_actionents.Add(ent);
                    }
                }
                foreach (var clear_ent in clear_actionents)
                {
                    all_actionents.Remove(clear_ent);
                }

                foreach (var grid in all_grids)
                {
                    if (grid.Physics != null && !grid.MarkedForClose)
                    {
                        reuse_initialspeed = grid.Physics.LinearVelocity;
                        reuse_initialang = grid.Physics.AngularVelocity;
                        float current_mass = grid.GetCurrentMass();
                        GetMaxSpeed(ref current_mass);
                        GetMaxAng(ref current_mass);

                        grid.Physics.SetSpeeds(reuse_finalspeed, reuse_finalang);
                        //MyAPIGateway.Utilities.ShowNotification(reuse_finalang.Length().ToString(), 1, "White");
                    }
                }
            }

            if (!MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session?.Player?.Character != null)
            {
                double alt = 0;
                reuse_character = MyAPIGateway.Session.Player?.Character;
                if (timer % 120 == 0 && reuse_character != null)
                {
                    is_in_planet = MyVisualScriptLogicProvider.IsPlanetNearby(reuse_character.WorldMatrix.Translation);
                    closest_planet = MyGamePruningStructure.GetClosestPlanet(reuse_character.WorldMatrix.Translation);
                }

                if (reuse_character != null && is_in_planet && closest_planet != null)
                {
                    foreach (var water in all_water)
                    {
                        if (water.planetID == closest_planet.EntityId)
                        {
                            alt = Math.Round(Vector3D.Distance(closest_planet.PositionComp.GetPosition(), reuse_character.WorldMatrix.Translation) - water.radius);
                            break;
                        }
                    }

                    if (alt <= -15)
                    {
                        if (reuse_character.EnabledHelmet)
                        {
                            reuse_character.SwitchHelmet();
                        }
                    }
                }

                reuse_cockpit = MyAPIGateway.Session.ControlledObject?.Entity as IMyShipController;
                if (reuse_cockpit != null && reuse_cockpit.IsWorking && reuse_cockpit.CubeGrid.Physics != null)
                {
                    if (MyAPIGateway.Session.CameraController != null && MyAPIGateway.Session.CameraController.IsInFirstPersonView)
                    {
                        valid_cockpit = true;
                        if (is_in_planet && closest_planet != null && !closest_planet.MarkedForClose && all_water.Count > 0)
                        {
                            foreach (var water in all_water)
                            {
                                if (water.planetID == closest_planet.EntityId)
                                {
                                    if (alt > 0)
                                    {
                                        if (water_alt_message != null)
                                        {
                                            water_alt_message.Visible = true;
                                            water_alt_sb.Clear();
                                            water_alt_sb.Append(alt.ToString() + " m");
                                        }
                                    }
                                    else
                                    {
                                        if (water_alt_message != null)
                                        {
                                            water_alt_message.Visible = false;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (water_alt_message != null)
                            {
                                water_alt_message.Visible = false;
                            }
                        }
                    }
                    else
                    {
                        if (water_alt_message != null)
                        {
                            water_alt_message.Visible = false;
                        }
                    }

                    if (!MyAPIGateway.Session.IsServer)
                    {
                        reuse_initialspeed = reuse_cockpit.CubeGrid.Physics.LinearVelocity;
                        reuse_initialang = reuse_cockpit.CubeGrid.Physics.AngularVelocity;
                        reuse_cubegrid = reuse_cockpit.CubeGrid as MyCubeGrid;
                        float current_mass = reuse_cubegrid.GetCurrentMass();
                        GetMaxSpeed(ref current_mass);
                        GetMaxAng(ref current_mass);

                        reuse_cockpit.CubeGrid.Physics.SetSpeeds(reuse_finalspeed, reuse_finalang);
                    }

                    if (reuse_finalspeed.Length() >= base_speed - 2)
                    {
                        if (max_speed_message != null)
                        {
                            max_speed_sb.Clear();
                            max_speed_sb.Append("Max: " + Math.Round(reuse_visual_speed.Length()).ToString() + " m/s");
                            max_speed_message.Visible = true;
                        }
                    }
                    else
                    {
                        if (max_speed_message != null)
                        {
                            max_speed_message.Visible = false;
                        }
                    }

                }
                else
                {
                    if (water_alt_message != null)
                    {
                        water_alt_message.Visible = false;
                    }
                    if (max_speed_message != null)
                    {
                        max_speed_message.Visible = false;
                    }
                    valid_cockpit = false;
                }
            }
            timer += 1;
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                api.RecievedData -= Api_RecievedData;
                api.Unregister();
                MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;
                MyAPIGateway.Entities.OnEntityRemove -= Entities_OnEntityRemove;
            }
            if (HUD_Base != null)
            {
                HUD_Base.Unload();
            }
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(netid, water_handler);
        }
    }
}