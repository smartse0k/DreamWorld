namespace DreamWorld.Common
{
    /// <summary>
    /// 캐릭터 및 몬스터가 바라보는 4방향
    /// </summary>
    public enum Direction : byte
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    /// <summary>
    /// 타일맵 지형 속성
    /// </summary>
    public enum TileType : byte
    {
        Walkable = 0, // 이동 가능 평지
        Wall = 1,     // 이동 불가 + 원거리/마법 차단
        Water = 2,    // 이동 불가 + 원거리/마법 통과 가능
        Portal = 3    // 이동 가능 + 접촉(밟기) 시 지정된 목적지 맵/좌표로 워프
    }

    /// <summary>
    /// 평타 및 스킬의 타겟 판정 방식
    /// </summary>
    public enum AttackTargetingType : byte
    {
        /// <summary>
        /// 바라보는 방향 앞 1~N 타일 직선 판정 (기본 공격은 TileRange = 1)
        /// </summary>
        FrontTile = 0,

        /// <summary>
        /// 특정 엔티티(TargetEntityId)를 지정하여 사거리 내 타격하는 스킬
        /// </summary>
        SingleTarget = 1,

        /// <summary>
        /// 특정 타일 또는 시전자 주변 범위 타격 스킬
        /// </summary>
        AreaOfEffect = 2
    }

    /// <summary>
    /// 클라이언트(C_)와 서버(S_) 간 통신 패킷 ID
    /// </summary>
    public enum PacketId : ushort
    {
        // Lobby Server (100 ~ 199)
        C_LoginRequest = 101,
        S_LoginResponse = 102,

        // World Server - 진입 및 스폰 (200 ~ 299)
        C_EnterWorld = 201,
        S_EnterWorldSuccess = 202,
        S_EntitySpawn = 203,
        S_EntityDespawn = 204,

        // World Server - 자유 이동 + 4방향 동기화 (300 ~ 399)
        C_Move = 301,
        S_MoveBroadcast = 302,

        // World Server - 전투 및 스킬 (400 ~ 499)
        C_Attack = 401,
        S_AttackResult = 402
    }
}
