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
using Sandbox.Game.Entities;
using VRage.ModAPI;
using System.Text;

namespace SERingAsteroids
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class SessionComponent : MySessionComponentBase
    {
        public const ushort NETWORK_ID = 0xe591;
        public const string MessageSenderName = "RingAsteroids";

        public static bool Unloading { get; private set; }
        
        public static int VoxelAddQueueLength => _VoxelsToAdd.Count;
        public static int VoxelDelQueueLength => _VoxelsToDelete.Count;

        private static readonly Queue<ProceduralVoxelDetails> _VoxelsToAdd = new Queue<ProceduralVoxelDetails>();
        private static readonly Queue<ProceduralVoxelDetails> _VoxelsToDelete = new Queue<ProceduralVoxelDetails>();
        private bool _IsInitialized;
        private static readonly Dictionary<string, RingConfig> _DrawRings = new Dictionary<string, RingConfig>();
        private static List<MyTuple<MyQuadD, Color>> _DrawnRingQuads = new List<MyTuple<MyQuadD, Color>>();
        private RingConfig _EditingRing = null;
        private RingZone _EditingZone = null;
        private MyPlanet _EditingRingPlanet = null;
        private string _RequestedRing = null;

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

                _DrawRings[config.PlanetName] = new RingConfig
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
            _DrawRings.Remove(name);
            UpdateDrawnRingQuads();
        }

        private static void UpdateDrawnRingQuads()
        {
            var quads = new List<MyTuple<MyQuadD, Color>>();

            foreach (var config in _DrawRings.Values)
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

            _DrawnRingQuads = quads;
        }

        public override void Draw()
        {
            base.Draw();

            var drawnQuads = _DrawnRingQuads;

            var quadMaterial = MyStringId.GetOrCompute("ContainerBorder");
            var lineMaterial = MyStringId.GetOrCompute("GizmoDrawLine");

            var lines = new HashSet<MyTuple<Vector3D, Vector3D, Vector4>>();

            foreach (var q in drawnQuads)
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

        public static void EnqueueVoxelAdd(ProceduralVoxelDetails voxelDetails)
        {
            if (!voxelDetails.AddPending)
            {
                lock (((ICollection)_VoxelsToAdd).SyncRoot)
                {
                    if (voxelDetails.VoxelMap?.Closed != false)
                    {
                        voxelDetails.AddPending = true;
                        _VoxelsToAdd.Enqueue(voxelDetails);
                    }
                }
            }
        }

        public static void EnqueueVoxelDelete(ProceduralVoxelDetails voxelDetails)
        {
            if (!voxelDetails.DeletePending)
            {
                lock (((ICollection)_VoxelsToDelete).SyncRoot)
                {
                    voxelDetails.DeletePending = true;
                    _VoxelsToDelete.Enqueue(voxelDetails);
                }
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (!_IsInitialized)
            {
                MyAPIGateway.Utilities.MessageEnteredSender += Utilities_MessageEnteredSender;
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NETWORK_ID, MessageHandler);
                _IsInitialized = true;
            }

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                ProceduralVoxelDetails voxelDetails;
                int voxelCountProcessed = 0;

                while (voxelCountProcessed < 5 && _VoxelsToAdd.TryDequeueSync(out voxelDetails))
                {
                    if (voxelDetails.AddPending && !voxelDetails.DeletePending)
                    {
                        voxelDetails.ExecuteAdd();
                        voxelDetails.AddPending = false;
                    }

                    voxelCountProcessed++;
                }

                while (voxelCountProcessed < 5 && _VoxelsToDelete.TryDequeueSync(out voxelDetails))
                {
                    if (voxelDetails.DeletePending && !voxelDetails.AddPending && voxelDetails.VoxelMap != null && !voxelDetails.VoxelMap.Closed)
                    {
                        voxelDetails.ExecuteDelete();
                    }

                    voxelDetails.AddPending = false;
                    voxelDetails.DeletePending = false;

                    voxelCountProcessed++;
                }
            }
        }

        private void MessageHandler(ushort handlerId, byte[] msgdata, ulong steamId, bool fromServer)
        {
            if (msgdata.Length < 12)
                return;

            var msgtype = new string(msgdata.Take(8).Select(c => (char)c).ToArray());
            var msglen = BitConverter.ToInt32(msgdata, 8);

            if (msgdata.Length < msglen + 12 || msglen < 0)
                return;

            var reqdata = new byte[msglen];
            Array.Copy(msgdata, 12, reqdata, 0, msglen);
            reqdata = MyCompression.Decompress(reqdata);

            if (fromServer == false && MyAPIGateway.Multiplayer.IsServer)
            {
                if (msgtype == "RINGREQ\0")
                {
                    var req = MyAPIGateway.Utilities.SerializeFromBinary<RingRequest>(reqdata);
                    ProcessRingRequest(req, steamId);
                }
                else if (msgtype == "RINGUPD\0")
                {
                    List<IMyPlayer> players = new List<IMyPlayer>();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(players, p => p.SteamUserId == steamId);

                    if (players.Count == 1)
                    {
                        var player = players[0];
                        if (player.PromoteLevel >= MyPromoteLevel.SpaceMaster)
                        {
                            var config = MyAPIGateway.Utilities.SerializeFromBinary<RingConfig>(reqdata);
                            RingConfig.CommitRingConfig(config);
                        }
                    }
                }
            }
            else if (fromServer == true)
            {
                if (msgtype == "RINGDATA")
                {
                    var config = MyAPIGateway.Utilities.SerializeFromBinary<RingConfig>(reqdata);

                    if (config.PlanetName == _RequestedRing)
                    {
                        _EditingRing = config;
                    }
                }
            }
        }

        private static void ProcessRingRequest(RingRequest req, ulong senderid)
        {
            RingConfig config = null;

            if (req.PlanetName == "@defaults")
            {
                config = RingConfig.SBCStoredDefaultConfig;
            }
            else
            {
                var voxelMaps = new List<IMyVoxelBase>();

                MyAPIGateway.Session.VoxelMaps.GetInstances(voxelMaps);
                var planets = voxelMaps.OfType<MyPlanet>().ToList();
                var planet = planets.FirstOrDefault(e => e.StorageName == req.PlanetName);

                if (planet != null)
                {
                    config = RingConfig.GetRingConfig(planet, null);
                }
            }

            if (config != null)
            {
                var ringdata = MyAPIGateway.Utilities.SerializeToBinary(config);
                var reqdata = MyCompression.Compress(ringdata);
                var msgdata = new byte[reqdata.Length + 12];
                Array.Copy(Encoding.ASCII.GetBytes("RINGDATA"), msgdata, 8);
                Array.Copy(BitConverter.GetBytes(reqdata.Length), 0, msgdata, 8, 4);
                Array.Copy(reqdata, 0, msgdata, 12, reqdata.Length);
                MyAPIGateway.Multiplayer.SendMessageTo(NETWORK_ID, msgdata, senderid);
            }
        }

        private void RequestRingFromServer(string arg)
        {
            var player = MyAPIGateway.Session.LocalHumanPlayer;
            var camera = MyAPIGateway.Session.Camera;
            string planetName = null;
            MyPlanet planet = null;
            var voxelMaps = new List<IMyVoxelBase>();

            MyAPIGateway.Session.VoxelMaps.GetInstances(voxelMaps);
            var planets = voxelMaps.OfType<MyPlanet>().ToList();

            if (arg == "@lookat" || arg == "@nearest")
            {
                var camerapos = camera.Position;
                var planetsByDistance = new SortedList<double, MyPlanet>();
                var lookvector = camera.ProjectionMatrix.Forward;
                var nearestDist = double.MaxValue;

                foreach (var p in planets)
                {
                    var relpos = p.PositionComp.GetPosition() - camerapos;
                    var surfacedist = relpos.Length() - p.AtmosphereRadius;

                    if (surfacedist > nearestDist)
                    {
                        continue;
                    }

                    if (arg == "@lookat")
                    {
                        var dot = Vector3D.Dot(relpos, lookvector);

                        if (dot < surfacedist)
                        {
                            continue;
                        }

                        var r = Math.Sqrt(relpos.LengthSquared() - dot * dot);

                        if (r > p.AtmosphereRadius)
                        {
                            continue;
                        }
                    }

                    planet = p;
                    planetName = p.StorageName;
                    nearestDist = surfacedist;
                }

                if (planetName == null)
                {
                    MyAPIGateway.Utilities.ShowMessage(MessageSenderName, "No matching planets");
                    return;
                }
            }
            else if (arg == "@defaults")
            {
                planetName = "@defaults";
            }
            else
            {
                var nameMatches = planets.Where(e => e.StorageName.StartsWith(arg, StringComparison.OrdinalIgnoreCase)).ToList();

                if (nameMatches.Count == 1)
                {
                    planet = nameMatches[0];
                    planetName = nameMatches[0].StorageName;
                }
                else if (nameMatches.Count == 0)
                {
                    MyAPIGateway.Utilities.ShowMessage(MessageSenderName, "No matching planets");
                    return;
                }
                else
                {
                    MyAPIGateway.Utilities.ShowMessage(MessageSenderName, "Multiple matching planets");

                    foreach (var p in nameMatches)
                    {
                        MyAPIGateway.Utilities.ShowMessage(MessageSenderName, p.StorageName);
                    }

                    return;
                }
            }

            _RequestedRing = planetName;
            _EditingRingPlanet = planet;

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (planetName == "@default")
                {
                    _EditingRing = RingConfig.SBCStoredDefaultConfig.Clone();
                }
                else
                {
                    _EditingRing = RingConfig.GetRingConfig(planet, null);
                }
            }
            else
            {
                var ringreq = new RingRequest { PlanetName = planetName };
                var reqdata = MyAPIGateway.Utilities.SerializeToBinary(ringreq);
                reqdata = MyCompression.Compress(reqdata);
                var msgdata = new byte[reqdata.Length + 12];
                Array.Copy(Encoding.ASCII.GetBytes("RINGREQ\0"), 0, msgdata, 0, 8);
                Array.Copy(BitConverter.GetBytes(reqdata.Length), 0, msgdata, 8, 4);
                Array.Copy(reqdata, 0, msgdata, 12, reqdata.Length);
                MyAPIGateway.Multiplayer.SendMessageToServer(NETWORK_ID, msgdata);
            }
        }

        private void CommitRingToServer()
        {
            var ring = _EditingRing;

            if (ring == null)
                return;

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                RingConfig.CommitRingConfig(ring);
                return;
            }

            var ringdata = MyAPIGateway.Utilities.SerializeToBinary(ring);
            var reqdata = MyCompression.Compress(ringdata);
            var msgdata = new byte[reqdata.Length + 12];
            Array.Copy(Encoding.ASCII.GetBytes("RINGUPD\0"), 0, msgdata, 0, 8);
            Array.Copy(BitConverter.GetBytes(reqdata.Length), 0, msgdata, 8, 4);
            Array.Copy(reqdata, 0, msgdata, 12, reqdata.Length);
            MyAPIGateway.Multiplayer.SendMessageToServer(NETWORK_ID, msgdata);
        }

        private void DisplayHelp()
        {
            // TODO
        }

        private void AddRingZone(string arg)
        {
            // TODO
        }

        private void SelectRingZone(string arg)
        {
            // TODO
        }

        private void Utilities_MessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (MyAPIGateway.Session.LocalHumanPlayer == null)
            {
                return;
            }

            if (!messageText.StartsWith("/ringast"))
            {
                return;
            }

            sendToOthers = false;

            var msgparts = messageText.Split(new[] { ' ' }, 3);

            if (msgparts.Length < 2 || msgparts[1] == "help")
            {
                DisplayHelp();
                return;
            }

            var cmd = msgparts[1];
            var arg = msgparts.Length == 3 ? msgparts[2] : null;

            if (cmd == "select")
            {
                RequestRingFromServer(arg);
                return;
            }

            if (_EditingRing == null || _EditingRing.PlanetName == null || _RequestedRing == null)
            {
                MyAPIGateway.Utilities.ShowMessage("RingAsteroids", "No planet selected");
                MyAPIGateway.Utilities.ShowMessage("RingAsteroids", "Use /ringast select [PlanetName] to select a ring");
                return;
            }

            if (cmd == "addzone")
            {
                AddRingZone(arg);
            }
            else if (cmd == "selzone")
            {
                SelectRingZone(arg);
            }
            else if (cmd == "delzone" && _EditingZone != null)
            {
                _EditingRing.RingZones?.Remove(_EditingZone);
                _EditingZone = null;
            }
            else if (cmd == "commit")
            {
                CommitRingToServer();
            }

            RingConfig.UpdateConfig(_EditingRing, cmd, arg, _EditingRingPlanet, MyAPIGateway.Session.LocalHumanPlayer);

            _DrawRings.Clear();

            if (_EditingRing.DebugDrawRingBounds == true)
            {
                AddOrUpdateShownRing(_EditingRing);
            }
            else
            {
                _DrawnRingQuads = new List<MyTuple<MyQuadD, Color>>();
            }
        }
    }
}
