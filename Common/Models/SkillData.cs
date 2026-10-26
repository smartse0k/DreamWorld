namespace DreamWorld.Common.Models
{
    /// <summary>
    /// 평타 및 스킬 정의 데이터
    /// </summary>
    public class SkillData
    {
        public int SkillId { get; set; }
        public string Name { get; set; } = string.Empty;
        public AttackTargetingType TargetingType { get; set; }
        public int TileRange { get; set; } = 1;
        public float WorldRange { get; set; } = 1.5f;
        public int Damage { get; set; } = 15;
        public int CooldownMs { get; set; } = 500;
        public int MaxTargets { get; set; } = 1;
    }
}
