using System;
using System.Collections.Generic;
using System.IO;
using DreamWorld.Common.Models;

namespace DreamWorld.Common.Protocol
{
    public class C_EnterWorldPacket : IPacket
    {
        public PacketId PacketId => PacketId.C_EnterWorld;

        public string AuthToken { get; set; } = string.Empty;
        public int CharacterId { get; set; }
        public string CharacterName { get; set; } = string.Empty;

        public void Write(BinaryWriter w)
        {
            w.Write(AuthToken ?? string.Empty);
            w.Write(CharacterId);
            w.Write(CharacterName ?? "Player");
        }

        public void Read(BinaryReader r)
        {
            AuthToken = r.ReadString();
            CharacterId = r.ReadInt32();
            CharacterName = r.ReadString();
        }
    }

    /// <summary>
    /// 월드 진입 및 맵 이동 결과를 전달하는 패킷.
    /// </summary>
    public class S_EnterWorldSuccessPacket : IPacket
    {
        public PacketId PacketId => PacketId.S_EnterWorldSuccess;

        public int MyEntityId { get; set; }
        public int MapId { get; set; } = 1;
        public string MapName { get; set; } = string.Empty;
        public int MapWidth { get; set; } = 16;
        public int MapHeight { get; set; } = 16;
        public byte[] MapTiles { get; set; } = Array.Empty<byte>();
        public float SpawnX { get; set; }
        public float SpawnY { get; set; }
        public List<EntitySnapshot> Entities { get; set; } = new List<EntitySnapshot>();

        public void Write(BinaryWriter w)
        {
            w.Write(MyEntityId);
            w.Write(MapId);
            w.Write(MapName ?? string.Empty);
            w.Write(MapWidth);
            w.Write(MapHeight);
            w.Write(MapTiles.Length);
            if (MapTiles.Length > 0)
            {
                w.Write(MapTiles);
            }
            w.Write(SpawnX);
            w.Write(SpawnY);
            w.Write(Entities.Count);
            foreach (var entity in Entities)
            {
                entity.WriteTo(w);
            }
        }

        public void Read(BinaryReader r)
        {
            MyEntityId = r.ReadInt32();
            MapId = r.ReadInt32();
            MapName = r.ReadString();
            MapWidth = r.ReadInt32();
            MapHeight = r.ReadInt32();
            int tileLen = r.ReadInt32();
            MapTiles = tileLen > 0 ? r.ReadBytes(tileLen) : Array.Empty<byte>();
            SpawnX = r.ReadSingle();
            SpawnY = r.ReadSingle();
            int count = r.ReadInt32();
            Entities = new List<EntitySnapshot>(count);
            for (int i = 0; i < count; i++)
            {
                Entities.Add(EntitySnapshot.ReadFrom(r));
            }
        }
    }

    public class S_EntityDespawnPacket : IPacket
    {
        public PacketId PacketId => PacketId.S_EntityDespawn;

        public int EntityId { get; set; }

        public void Write(BinaryWriter w)
        {
            w.Write(EntityId);
        }

        public void Read(BinaryReader r)
        {
            EntityId = r.ReadInt32();
        }
    }
}
