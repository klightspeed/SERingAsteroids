using System.Collections;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game;
using System.Linq;
using VRageMath;
using VRage;
using VRage.Utils;

namespace SERingAsteroids
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class SessionComponent : MySessionComponentBase
    {
        public const ushort NETWORK_ID = 0xe591;

        public static bool Unloading { get; private set; }
        private static readonly Queue<AddVoxelDetails> VoxelsToAdd = new Queue<AddVoxelDetails>();
        private bool IsInitialized;
        private static readonly Dictionary<string, RingConfig> DrawRings = new Dictionary<string, RingConfig>();
        private static List<MyTuple<MyQuadD, Color>> DrawnRingQuads = new List<MyTuple<MyQuadD, Color>>();

        public override void SaveData()
        {
            RingConfig.SaveConfigs();
        }

        protected override void UnloadData()
        {
            Unloading = true;
        }

        public static void AddOrUpdateShownRing(RingConfig config)
        {
            if (config.RingInnerRadius != null &&
                config.RingOuterRadius != null &&
                config.RingHeight != null &&
                config.RingCentre != null &&
                config.SectorSize != null &&
                config.RingZones != null &&
                config.PlanetName != null &&
                config.DebugDrawRingBounds == true)
            {
                var sectorSize = config.SectorSize.Value;
                var ringInnerRadius = Math.Floor(config.RingInnerRadius.Value / sectorSize + 0.5) * sectorSize;
                var ringOuterRadius = Math.Floor(config.RingOuterRadius.Value / sectorSize + 0.5) * sectorSize;
                var ringHeight = config.RingHeight.Value;
                var ringCentre = config.RingCentre.Value;
                var ringZones = config.RingZones.OrderBy(e => e.InnerRadius).ToList();
                var taperEdges = config.TaperRingEdge ?? true;
                var ringSections = new List<RingZone>();

                var rad = ringInnerRadius;

                if (taperEdges && ringOuterRadius >= ringInnerRadius + sectorSize * 3)
                {
                    ringSections.Add(new RingZone
                    {
                        InnerRadius = ringInnerRadius,
                        OuterRadius = ringInnerRadius + sectorSize,
                        InnerRingHeight = 0,
                        OuterRingHeight = ringHeight
                    });

                    ringSections.Add(new RingZone
                    {
                        InnerRadius = ringOuterRadius - sectorSize,
                        OuterRadius = ringOuterRadius,
                        InnerRingHeight = ringHeight,
                        OuterRingHeight = 0
                    });

                    ringInnerRadius += sectorSize;
                    ringOuterRadius -= sectorSize;
                }

                for (int i = 0; i <= ringZones.Count; i++)
                {
                    var nextZone = i >= ringZones.Count ? null : ringZones[i];
                    var prevZone = i <= 0 ? null : ringZones[i - 1];

                    var sectionInnerRadius = prevZone?.OuterRadius ?? ringInnerRadius;
                    var sectionOuterRadius = nextZone?.InnerRadius ?? ringOuterRadius;

                    if (sectionInnerRadius < ringInnerRadius)
                        sectionInnerRadius = ringInnerRadius;

                    if (sectionOuterRadius > ringOuterRadius)
                        sectionOuterRadius = ringOuterRadius;

                    if (sectionOuterRadius > sectionInnerRadius)
                    {
                        ringSections.Add(new RingZone
                        {
                            InnerRadius = prevZone?.OuterRadius ?? ringInnerRadius,
                            OuterRadius = nextZone?.InnerRadius ?? ringOuterRadius,
                            InnerRingHeight = ringHeight,
                            OuterRingHeight = ringHeight
                        });
                    }

                    if (nextZone != null)
                    {
                        sectionInnerRadius = nextZone.InnerRadius;
                        sectionOuterRadius = nextZone.OuterRadius;

                        if (sectionInnerRadius < ringInnerRadius)
                            sectionInnerRadius = ringInnerRadius;

                        if (sectionOuterRadius > ringOuterRadius)
                            sectionOuterRadius = ringOuterRadius;

                        var innerRingHeight = nextZone.InnerRingHeight ?? nextZone.RingHeight ?? ringHeight;
                        var outerRingHeight = nextZone.OuterRingHeight ?? nextZone.RingHeight ?? ringHeight;
                        var sectionSize = sectionOuterRadius - sectionInnerRadius;
                        var maxAsteroidsPerSector = nextZone.MaxAsteroidsPerSector ?? config.MaxAsteroidsPerSector ?? 50;

                        if (sectionOuterRadius > sectionInnerRadius && maxAsteroidsPerSector != 0)
                        {
                            if (nextZone.TaperEdges == true && sectionOuterRadius >= sectionInnerRadius + sectorSize * 3)
                            {
                                var zoneInnerHeight = innerRingHeight * (1 - sectorSize / sectionSize) + outerRingHeight * (sectorSize / sectionSize);
                                var zoneOuterHeight = outerRingHeight * (1 - sectorSize / sectionSize) + innerRingHeight * (sectorSize / sectionSize);

                                ringSections.Add(new RingZone
                                {
                                    InnerRadius = sectionInnerRadius,
                                    OuterRadius = sectionInnerRadius + sectorSize,
                                    InnerRingHeight = ringHeight,
                                    OuterRingHeight = zoneInnerHeight
                                });

                                ringSections.Add(new RingZone
                                {
                                    InnerRadius = ringOuterRadius - sectorSize,
                                    OuterRadius = ringOuterRadius,
                                    InnerRingHeight = zoneOuterHeight,
                                    OuterRingHeight = ringHeight
                                });

                                innerRingHeight = zoneInnerHeight;
                                outerRingHeight = zoneOuterHeight;
                                sectionInnerRadius += sectorSize;
                                sectionOuterRadius -= sectorSize;
                            }

                            ringSections.Add(new RingZone
                            {
                                InnerRadius = sectionInnerRadius,
                                OuterRadius = sectionOuterRadius,
                                InnerRingHeight = innerRingHeight,
                                OuterRingHeight = outerRingHeight
                            });
                        }
                    }
                }

                DrawRings[config.PlanetName] = new RingConfig
                {
                    PlanetName = config.PlanetName,
                    SectorSize = config.SectorSize,
                    RingCentre = config.RingCentre,
                    RingInclination = config.RingInclination,
                    RingInnerRadius = config.RingInnerRadius,
                    RingOuterRadius = config.RingOuterRadius,
                    RingHeight = config.RingHeight,
                    RingLongitudeAscendingNode = config.RingLongitudeAscendingNode,
                    Enabled = config.Enabled ?? false,
                    RingZones = ringSections.OrderBy(e => e.InnerRadius).ToList()
                };

                UpdateDrawnRingQuads();
            }
        }

        public static void RemoveShownRing(string name)
        {
            DrawRings.Remove(name);
            UpdateDrawnRingQuads();
        }

        private static void UpdateDrawnRingQuads()
        {
            var quads = new List<MyTuple<MyQuadD, Color>>();

            foreach (var config in DrawRings.Values)
            {
                if (config.RingCentre != null &&
                    config.RingZones != null &&
                    config.RingInnerRadius != null &&
                    config.RingOuterRadius != null &&
                    config.RingInclination != null &&
                    config.RingLongitudeAscendingNode != null &&
                    config.RingHeight != null)
                {
                    var colour = config.Enabled == true ? Color.Aqua : Color.Orange;
                    var ringMatrix = config.GetRingMatrix();
                    var ziMatrix = MatrixD.CreateRotationY(config.RingLongitudeAscendingNode.Value * Math.PI / 180) * MatrixD.CreateTranslation(config.RingCentre.Value);
                    var riMatrix = MatrixD.CreateRotationX(Math.PI / 2) * ziMatrix;
                    var r2Matrix = MatrixD.CreateRotationX(Math.PI / 2) * MatrixD.CreateRotationY(Math.PI / 2) * ziMatrix;

                    for (int i = 0; i < 48; i++)
                    {
                        var a0 = i * Math.PI / 24;
                        var a1 = a0 + Math.PI / 24;
                        var x0 = Math.Cos(a0);
                        var y0 = Math.Sin(a0);
                        var x1 = Math.Cos(a1);
                        var y1 = Math.Sin(a1);

                        foreach (var zone in config.RingZones)
                        {
                            quads.Add(new MyTuple<MyQuadD, Color>(new MyQuadD
                            {
                                Point0 = Vector3D.Transform(new Vector3D { X = x0 * zone.InnerRadius, Y = zone.InnerRingHeight.Value, Z = y0 * zone.InnerRadius }, ringMatrix),
                                Point1 = Vector3D.Transform(new Vector3D { X = x1 * zone.InnerRadius, Y = zone.InnerRingHeight.Value, Z = y1 * zone.InnerRadius }, ringMatrix),
                                Point2 = Vector3D.Transform(new Vector3D { X = x1 * zone.OuterRadius, Y = zone.OuterRingHeight.Value, Z = y1 * zone.OuterRadius }, ringMatrix),
                                Point3 = Vector3D.Transform(new Vector3D { X = x0 * zone.OuterRadius, Y = zone.OuterRingHeight.Value, Z = y0 * zone.OuterRadius }, ringMatrix)
                            }, colour));
                            quads.Add(new MyTuple<MyQuadD, Color>(new MyQuadD
                            {
                                Point0 = Vector3D.Transform(new Vector3D { X = x0 * zone.InnerRadius, Y = -zone.InnerRingHeight.Value, Z = y0 * zone.InnerRadius }, ringMatrix),
                                Point1 = Vector3D.Transform(new Vector3D { X = x1 * zone.InnerRadius, Y = -zone.InnerRingHeight.Value, Z = y1 * zone.InnerRadius }, ringMatrix),
                                Point2 = Vector3D.Transform(new Vector3D { X = x1 * zone.OuterRadius, Y = -zone.OuterRingHeight.Value, Z = y1 * zone.OuterRadius }, ringMatrix),
                                Point3 = Vector3D.Transform(new Vector3D { X = x0 * zone.OuterRadius, Y = -zone.OuterRingHeight.Value, Z = y0 * zone.OuterRadius }, ringMatrix)
                            }, colour));
                        }

                        quads.Add(new MyTuple<MyQuadD, Color>(new MyQuadD
                        {
                            Point0 = Vector3D.Transform(new Vector3D { X = x0 * config.RingInnerRadius.Value, Y = 0, Z = y0 * config.RingInnerRadius.Value }, ziMatrix),
                            Point1 = Vector3D.Transform(new Vector3D { X = x1 * config.RingInnerRadius.Value, Y = 0, Z = y1 * config.RingInnerRadius.Value }, ziMatrix),
                            Point2 = Vector3D.Transform(new Vector3D { X = x1 * config.RingOuterRadius.Value, Y = 0, Z = y1 * config.RingOuterRadius.Value }, ziMatrix),
                            Point3 = Vector3D.Transform(new Vector3D { X = x0 * config.RingOuterRadius.Value, Y = 0, Z = y0 * config.RingOuterRadius.Value }, ziMatrix)
                        }, Color.DarkBlue));

                        quads.Add(new MyTuple<MyQuadD, Color>(new MyQuadD
                        {
                            Point0 = Vector3D.Transform(new Vector3D { X = x0 * config.RingInnerRadius.Value, Y = 0, Z = y0 * config.RingInnerRadius.Value }, riMatrix),
                            Point1 = Vector3D.Transform(new Vector3D { X = x1 * config.RingInnerRadius.Value, Y = 0, Z = y1 * config.RingInnerRadius.Value }, riMatrix),
                            Point2 = Vector3D.Transform(new Vector3D { X = x1 * config.RingOuterRadius.Value, Y = 0, Z = y1 * config.RingOuterRadius.Value }, riMatrix),
                            Point3 = Vector3D.Transform(new Vector3D { X = x0 * config.RingOuterRadius.Value, Y = 0, Z = y0 * config.RingOuterRadius.Value }, riMatrix)
                        }, Color.DarkRed));

                        quads.Add(new MyTuple<MyQuadD, Color>(new MyQuadD
                        {
                            Point0 = Vector3D.Transform(new Vector3D { X = x0 * config.RingInnerRadius.Value, Y = 0, Z = y0 * config.RingInnerRadius.Value }, r2Matrix),
                            Point1 = Vector3D.Transform(new Vector3D { X = x1 * config.RingInnerRadius.Value, Y = 0, Z = y1 * config.RingInnerRadius.Value }, r2Matrix),
                            Point2 = Vector3D.Transform(new Vector3D { X = x1 * config.RingOuterRadius.Value, Y = 0, Z = y1 * config.RingOuterRadius.Value }, r2Matrix),
                            Point3 = Vector3D.Transform(new Vector3D { X = x0 * config.RingOuterRadius.Value, Y = 0, Z = y0 * config.RingOuterRadius.Value }, r2Matrix)
                        }, Color.DarkGreen));
                    }
                }
            }

            DrawnRingQuads = quads;
        }

        public override void Draw()
        {
            base.Draw();

            var quadMaterial = MyStringId.GetOrCompute("ContainerBorder");
            var lineMaterial = MyStringId.GetOrCompute("GizmoDrawLine");

            var lines = new HashSet<MyTuple<Vector3D, Vector3D, Vector4>>();

            foreach (var q in DrawnRingQuads)
            {
                var quad = q.Item1;
                var colour = q.Item2.ToVector4();
                var cpos = (q.Item1.Point0 + q.Item1.Point1 + q.Item1.Point2 + q.Item1.Point3) / 4;
                MyTransparentGeometry.AddQuad(quadMaterial, ref quad, colour, ref cpos, blendType: VRageRender.MyBillboard.BlendTypeEnum.AdditiveTop);

                var quadLines = new[]
                {
                    new MyTuple<Vector3D, Vector3D, Vector4>(quad.Point0, quad.Point1, colour),
                    new MyTuple<Vector3D, Vector3D, Vector4>(quad.Point1, quad.Point2, colour),
                    new MyTuple<Vector3D, Vector3D, Vector4>(quad.Point2, quad.Point3, colour),
                    new MyTuple<Vector3D, Vector3D, Vector4>(quad.Point3, quad.Point0, colour),
                };

                foreach (var line in quadLines)
                {
                    var revline = new MyTuple<Vector3D, Vector3D, Vector4>(line.Item2, line.Item1, line.Item3);

                    if (!lines.Contains(line) && !lines.Contains(revline))
                    {
                        lines.Add(line);
                    }
                }
            }

            foreach (var line in lines)
            {
                var colour = line.Item3;
                MySimpleObjectDraw.DrawLine(line.Item1, line.Item2, lineMaterial, ref colour, 100.0f);
            }
        }

        public static void EnqueueVoxelAdd(AddVoxelDetails voxelDetails)
        {
            lock (((ICollection)VoxelsToAdd).SyncRoot)
            {
                VoxelsToAdd.Enqueue(voxelDetails);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (!IsInitialized)
            {
                //MyAPIGateway.Utilities.MessageEnteredSender += Utilities_MessageEnteredSender;
                //MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NETWORK_ID, MessageHandler);
                IsInitialized = true;
            }

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                AddVoxelDetails addVoxelDetails;
                int voxelCountAdded = 0;

                while (VoxelsToAdd.TryDequeueSync(out addVoxelDetails))
                {
                    addVoxelDetails.Execute();

                    voxelCountAdded++;

                    if (voxelCountAdded > 5)
                    {
                        break;
                    }
                }
            }
        }

        private void MessageHandler(ushort handlerId, byte[] msgdata, ulong steamId, bool fromServer)
        {
            if (fromServer == false && MyAPIGateway.Multiplayer.IsServer)
            {
                List<IMyPlayer> players = new List<IMyPlayer>();
                MyAPIGateway.Multiplayer.Players.GetPlayers(players, p => p.SteamUserId == steamId);
                if (players.Count == 1)
                {
                    var player = players[0];
                    if (player.PromoteLevel >= MyPromoteLevel.Admin)
                    {

                    }
                }
            }
        }

        private void Utilities_MessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (messageText.StartsWith("/ringast"))
            {
                sendToOthers = false;

                var msgparts = messageText.Split(' ');

                //MyAPIGateway.Multiplayer.SendMessageToServer(NETWORK_ID, null);
            }
        }
    }
}
