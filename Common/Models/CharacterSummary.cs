using System.IO;

namespace DreamWorld.Common.Models
{
    /// <summary>
    /// 로비에서 전달하는 캐릭터 기본 정보
    /// </summary>
    public class CharacterSummary
    {
        public int CharacterId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; } = 1;
        public int MaxHp { get; set; } = 100;

        public void WriteTo(BinaryWriter w)
        {
            w.Write(CharacterId);
            w.Write(Name ?? string.Empty);
            w.Write(Level);
            w.Write(MaxHp);
        }

        public static CharacterSummary ReadFrom(BinaryReader r)
        {
            return new CharacterSummary
            {
                CharacterId = r.ReadInt32(),
                Name = r.ReadString(),
                Level = r.ReadInt32(),
                MaxHp = r.ReadInt32()
            };
        }
    }
}
