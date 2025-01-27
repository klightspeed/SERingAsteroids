﻿using System.Collections;
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
using Sandbox.Engine.Utils;
using System.IO;

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

        public static readonly char[] DisallowedPlanetNameCharacters = "/\\:<>\"|?*".ToCharArray();

        private static readonly Queue<ProceduralVoxelDetails> _VoxelsToAdd = new Queue<ProceduralVoxelDetails>();
        private static readonly Queue<ProceduralVoxelDetails> _VoxelsToDelete = new Queue<ProceduralVoxelDetails>();
        private bool _IsInitialized;
        private bool _ShowHelpRequested;
        private static readonly Dictionary<string, RingConfig> _DrawRings = new Dictionary<string, RingConfig>();
        private static List<MyTuple<MyQuadD, Color>> _DrawnRingQuads = new List<MyTuple<MyQuadD, Color>>();
        private RingConfig _EditingRing = null;
        private RingZone _EditingZone = null;
        private MyPlanet _EditingRingPlanet = null;
        private string _RequestedRing = null;
        private TextWriter _logfile;
        private string _logfilename;

        private readonly object _loggerLock = new object();

        private void Log(string str)
        {
            lock (_loggerLock)
            {
                if (_logfile == null)
                {
                    _logfilename = $"{typeof(SessionComponent).Name}-{DateTime.Now:yyyyMMddHHmmss}.log";
                    _logfile = MyAPIGateway.Utilities.WriteFileInLocalStorage(_logfilename, typeof(SessionComponent));
                }

                _logfile.WriteLine(str);
                _logfile.Flush();
            }
        }


        public override void SaveData()
        {
            RingConfig.SaveConfigs();
        }

        protected override void UnloadData()
        {
            Unloading = true;
            MyAPIGateway.Utilities.MessageEnteredSender -= Utilities_MessageEnteredSender;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(NETWORK_ID, MessageHandler);
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
                config.PlanetName != "@defaults")
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

            if (_ShowHelpRequested)
            {
                _ShowHelpRequested = false;
                DisplayHelp();
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
                    RingConfig config;

                    try
                    {
                        config = MyAPIGateway.Utilities.SerializeFromBinary<RingConfig>(reqdata);
                    }
                    catch (Exception ex)
                    {
                        Log($"##MOD: RingAsteroids: Error deserializing ring config from player with SteamID {steamId}: {ex}");
                        MyLog.Default.WriteLineAndConsole($"##MOD: RingAsteroids: Error deserializing ring config from player with SteamID {steamId}: {ex}");
                        return;
                    }

                    Log($"##MOD: RingAsteroids: Received RINGUPD from player with SteamID {steamId} for planet {config.PlanetName}");
                    MyLog.Default.WriteLineAndConsole($"##MOD: RingAsteroids: Received RINGUPD from player with SteamID {steamId} for planet {config.PlanetName}");

                    List<IMyPlayer> players = new List<IMyPlayer>();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(players, p => p.SteamUserId == steamId);

                    if (players.Count == 1)
                    {
                        var player = players[0];
                        if (player.PromoteLevel >= MyPromoteLevel.SpaceMaster)
                        {
                            RingConfig.CommitRingConfig(config);
                        }
                        else
                        {
                            Log($"##MOD: RingAsteroids: Player with SteamID {steamId} is not space master; current rank: {player.PromoteLevel}");
                            MyLog.Default.WriteLineAndConsole($"##MOD: RingAsteroids: Player with SteamID {steamId} is not space master; current rank: {player.PromoteLevel}");
                        }
                    }
                    else
                    {
                        Log($"##MOD: RingAsteroids: Player with SteamID {steamId} not found");
                        MyLog.Default.WriteLineAndConsole($"##MOD: RingAsteroids: Player with SteamID {steamId} not found");
                    }
                }
            }
            else if (fromServer == true)
            {
                if (msgtype == "RINGDATA")
                {
                    var config = MyAPIGateway.Utilities.SerializeFromBinary<RingConfig>(reqdata);
                    config.RingZones = config.RingZones ?? new List<RingZone>();

                    if (config.PlanetName == _RequestedRing)
                    {
                        UpdateEditingRing(config);

                        if (_RequestedRing == "@defaults")
                        {
                            MyAPIGateway.Utilities.ShowMessage(MessageSenderName, "Editing ring config defaults");
                        }
                        else
                        {
                            MyAPIGateway.Utilities.ShowMessage(MessageSenderName, $"Editing ring config for planet {_RequestedRing}");
                        }
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
                config.PlanetName = "@defaults";
                config.DebugDrawRingBounds = false;
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
                    config.PlanetName = req.PlanetName;
                    config.DebugDrawRingBounds = false;
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
            if (arg == null)
                return;

            var player = MyAPIGateway.Session.LocalHumanPlayer;
            var camera = MyAPIGateway.Session.Camera;
            string planetName = null;
            MyPlanet planet = null;
            var voxelMaps = new List<IMyVoxelBase>();

            MyAPIGateway.Session.VoxelMaps.GetInstances(voxelMaps);
            var planets = voxelMaps.OfType<MyPlanet>().ToList();

            if (arg.StartsWith("@l") || arg.StartsWith("@n"))
            {
                var camerapos = camera.Position;
                var planetsByDistance = new SortedList<double, MyPlanet>();
                var lookvector = camera.WorldMatrix.Forward;
                var nearestDist = double.MaxValue;
                var nearestToCentre = double.MaxValue;

                foreach (var p in planets)
                {
                    var relpos = p.PositionComp.GetPosition() - camerapos;
                    var surfacedist = relpos.Length() - p.AtmosphereRadius;

                    if (arg.StartsWith("@l"))
                    {
                        var dot = Vector3D.Dot(relpos, lookvector);

                        if (dot < surfacedist)
                        {
                            continue;
                        }

                        var r = Math.Sqrt(relpos.LengthSquared() - dot * dot);

                        if (r / p.AverageRadius > nearestToCentre || (surfacedist > nearestDist && nearestToCentre < 1))
                        {
                            continue;
                        }

                        nearestToCentre = r / p.AtmosphereRadius;
                    }
                    else if (surfacedist > nearestDist)
                    {
                        continue;
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
            else if (arg.StartsWith("@d"))
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
                RingConfig config;

                if (planetName == "@defaults")
                {
                    MyAPIGateway.Utilities.ShowMessage(MessageSenderName, "Editing ring defaults");
                    config = RingConfig.SBCStoredDefaultConfig.Clone();
                    config.PlanetName = "@defaults";
                    config.DebugDrawRingBounds = false;
                }
                else
                {
                    MyAPIGateway.Utilities.ShowMessage(MessageSenderName, $"Editing ring data for planet {planetName}");
                    config = RingConfig.GetRingConfig(planet, null);
                    config.PlanetName = planetName;
                    config.DebugDrawRingBounds = false;
                }

                UpdateEditingRing(config);
            }
            else
            {
                MyAPIGateway.Utilities.ShowMessage(MessageSenderName, $"Requesting ring data for planet {planetName} from server");

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
            MyAPIGateway.Utilities.ShowMissionScreen(
                "Ring Asteroid Commands",
                "Help: ",
                "Available commands",
                "/ringast select @defaults\n" +
                "    Select the ring defaults for editing\n" +
                "\n" +
                "/ringast select @nearest\n" +
                "    Select the ring of the nearest planet to the player or spectator camera for editing\n" +
                "\n" +
                "/ringast select @lookat\n" +
                "    Select the ring of the planet in the crosshairs of the player or specatator camera for editing\n" +
                "\n" +
                "/ringast select [PlanetName]\n" +
                "    Select the ring of the planet with the given PlanetName for editing\n" +
                "\n" +
                "/ringast deselect\n" +
                "/ringast close\n" +
                "    Deselect the ring\n" +
                "\n" +
                "/ringast commit {deselect|close}\n" +
                "/ringast save {deselect|close}\n" +
                "    Commit the changes made to the selected ring.  Optionally follow with deselect or close to deselect the ring.\n" +
                "\n" +
                "/ringast loadlocal\n" +
                "/ringast ll\n" +
                "    Loads the ring configuration from the .xml.editing file for the ring being edited from the mod world storage on the local computer.\n" +
                "\n" +
                "/ringast taperringedge {true|false|yes|no}\n" +
                "/ringast notaperringedge\n" +
                "/ringast taper {true|false|yes|no}\n" +
                "/ringast notaper\n" +
                "    Enables or disables tapering the ring edge\n" +
                "\n" +
                "/ringast includeplanetnameinrandomseed {true|false|yes|no}\n" +
                "/ringast noincludeplanetnameinrandomseed\n" +
                "/ringast pnseed {true|false|yes|no}\n" +
                "/ringast nopnseed\n" +
                "    Include the planet name in the sector seed used to generate asteroids\n" +
                "\n" +
                "/ringast enabled {true|false|yes|no}\n" +
                "/ringast noenabled\n" +
                "/ringast disable\n" +
                "    Enable or disable asteroid generation for this ring\n" +
                "\n" +
                "/ringast logdebug {true|false|yes|no}\n" +
                "/ringast nologdebug\n" +
                "/ringast ld {true|false|yes|no}\n" +
                "/ringast nold\n" +
                "    Enable or disable debug logging for this ring\n" +
                "\n" +
                "/ringast ringinnerradius [radius]\n" +
                "/ringast ir [radius]\n" +
                "    Ring inner radius in metres. Accepts k or km suffix for kilometres\n" +
                "\n" +
                "/ringast ringouterradius [radius]\n" +
                "/ringast or [radius]\n" +
                "    Ring outer radius in metres. Accepts k or km suffix for kilometres\n" +
                "\n" +
                "/ringast ringheight [height]\n" +
                "/ringast ht [height]\n" +
                "    Distance between ring plane and upper / lower limit of ring. Accepts k or km suffix for kilometres\n" +
                "\n" +
                "/ringast ringlongitudeascendingnode [degrees]\n" +
                "/ringast lan [degrees]\n" +
                "    Longitude of ascending node (where the ring crosses the planet's equator going northwards)\n" +
                "\n" +
                "/ringast ringinclination [degrees]\n" +
                "/ringast inc [degrees]\n" +
                "    Inclination of ring to planet's equator\n" +
                "\n" +
                "/ringast sectorsize [size]\n" +
                "/ringast secsz [size]\n" +
                "    Ring sector size in metres. Accepts k or km suffix for kilometres\n" +
                "\n" +
                "/ringast maxasteroidspersector [count]\n" +
                "/ringast maxpersec\n" +
                "    Maximum asteroids per ring sector\n" +
                "\n" +
                "/ringast minasteroidsize [size]\n" +
                "/ringast minsz [size]\n" +
                "    Minimum asteroid size in metres\n" +
                "\n" +
                "/ringast maxasteroidsize [size]\n" +
                "/ringast maxsz [size]\n" +
                "    Maximum asteroid size in metres\n" +
                "\n" +
                "/ringast entitymovementthreshold [dist]\n" +
                "/ringast entmov [dist]\n" +
                "    Distance any grid or player needs to move before new sectors are considered for population with asteroids\n" +
                "\n" +
                "/ringast sizeexponent [exponent]\n" +
                "/ringast szexp [exponent]\n" +
                "    Size weighting exponent. Values larger than 1 prefer smaller sizes, while values smaller than 1 prefer larger sizes\n" +
                "\n" +
                "/ringast exclusionzonesize [size]\n" +
                "/ringast xzsiz [size]\n" +
                "    Minimum space around asteroid in metres to exclude other asteroids\n" +
                "\n" +
                "/ringast exclusionzonesizemult [mult]\n" +
                "/ringast xzmul [mult]\n" +
                "    Minimum space around asteroid as a multiple of its size to exclude other asteroids\n" +
                "\n" +
                "/ringast allowasteroidsinatmosphere {true|false|yes|no}\n" +
                "/ringast noallowasteroidsinatmosphere\n" +
                "    Allow or disallow asteroids inside planet atmosphere for this ring\n" +
                "\n" +
                "All of the above commands are case insensitive, and most also accept the shortest unique prefix."
            );
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
                _ShowHelpRequested = true;
                return;
            }

            var cmd = msgparts[1];
            var arg = msgparts.Length == 3 ? msgparts[2] : null;

            var config = _EditingRing;

            if (cmd.StartsWith("sel"))
            {
                DeselectRing();
                RequestRingFromServer(arg);
                return;
            }
            else if (cmd.StartsWith("des") || cmd.StartsWith("cl"))
            {
                MyAPIGateway.Utilities.ShowNotification("De-selecting ring");
                DeselectRing();
                return;
            }
            else if (config != null && config.PlanetName != null && DisallowedPlanetNameCharacters.Any(c => config.PlanetName.Contains(c)))
            {
                MyAPIGateway.Utilities.ShowNotification("Disallowed character in ring planet name - de-selecting ring");
                DeselectRing();
                return;
            }

            if (config == null)
            {
                MyAPIGateway.Utilities.ShowMessage(MessageSenderName, "No ring data selected");
                MyAPIGateway.Utilities.ShowMessage(MessageSenderName, "Use /ringast select [PlanetName] to select a ring");
                return;
            }

            if (cmd == "zoneadd" || cmd.StartsWith("za"))
            {
                AddRingZone(arg);
            }
            else if (cmd.StartsWith("zonesel") || cmd.StartsWith("zsel"))
            {
                SelectRingZone(arg);
            }
            else if ((cmd.StartsWith("zonedel") || cmd.StartsWith("zd")) && _EditingZone != null)
            {
                config.RingZones?.Remove(_EditingZone);
                _EditingZone = null;
            }
            else if (cmd.StartsWith("com") || cmd.StartsWith("sav"))
            {
                MyAPIGateway.Utilities.ShowNotification("Committing ring settings");
                CommitRingToServer();

                if (arg != null && (arg.StartsWith("d") || arg.StartsWith("c")))
                {
                    DeselectRing();
                    return;
                }
            }
            else if (cmd.StartsWith("loadl") || cmd.StartsWith("ll"))
            {
                var filename = $"{(config.PlanetName == "@defaults" ? "ringDefaults" : config.PlanetName)}.xml.editing";

                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(filename, typeof(RingAsteroidsComponent)))
                {
                    MyAPIGateway.Utilities.ShowNotification("Loading ring settings from local world storage");

                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(filename, typeof(RingAsteroidsComponent)))
                    {
                        config = MyAPIGateway.Utilities.SerializeFromXML<RingConfig>(reader.ReadToEnd());
                    }
                }
            }
            else
            {
                RingConfig.UpdateConfig(config, cmd, arg, _EditingRingPlanet, MyAPIGateway.Session.LocalHumanPlayer);
            }

            UpdateEditingRing(config);
        }

        private void UpdateEditingRing(RingConfig config)
        {
            if (DisallowedPlanetNameCharacters.Any(c => config.PlanetName.Contains(c)))
            {
                return;
            }

            _EditingRing = config;

            var filename = $"{(config.PlanetName == "@defaults" ? "ringDefaults" : config.PlanetName)}.xml.editing";

            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(filename, typeof(RingAsteroidsComponent)))
            {
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(config));
            }

            AddOrUpdateShownRing(config);
        }

        private void DeselectRing()
        {
            var config = _EditingRing;

            if (config != null && config.PlanetName != null)
            {
                config.DebugDrawRingBounds = false;

                RemoveShownRing(config.PlanetName);

                if (!DisallowedPlanetNameCharacters.Any(c => config.PlanetName.Contains(c)))
                {
                    var filename = $"{(config.PlanetName == "@defaults" ? "ringDefaults" : config.PlanetName)}.xml.editing";

                    if (MyAPIGateway.Utilities.FileExistsInWorldStorage(filename, typeof(RingAsteroidsComponent)))
                    {
                        MyAPIGateway.Utilities.DeleteFileInWorldStorage(filename, typeof(RingAsteroidsComponent));
                    }
                }
            }

            _EditingRing = null;
            _EditingRingPlanet = null;
            _EditingZone = null;
        }
    }
}
