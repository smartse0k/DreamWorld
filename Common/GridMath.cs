using System;
using System.Collections.Generic;

namespace DreamWorld.Common
{
    /// <summary>
    /// 월드 좌표와 타일 그리드 간 변환 및 충돌 판정을 계산하는 유틸리티.
    /// </summary>
    public static class GridMath
    {
        /// <summary>
        /// 캐릭터 기본 충돌 반지름.
        /// </summary>
        public const float DefaultEntityRadius = 0.35f;

        /// <summary>
        /// 전방 타일 타격 판정 허용 반경.
        /// </summary>
        public const float FrontTileHitTolerance = 0.85f;

        /// <summary>
        /// 월드 좌표를 타일 인덱스로 변환합니다.
        /// </summary>
        public static (int tileX, int tileY) WorldToTile(float worldX, float worldY)
        {
            return ((int)Math.Floor(worldX), (int)Math.Floor(worldY));
        }

        /// <summary>
        /// 타일 인덱스를 타일 중심의 월드 좌표로 변환합니다.
        /// </summary>
        public static (float worldX, float worldY) TileToWorldCenter(int tileX, int tileY)
        {
            return (tileX + 0.5f, tileY + 0.5f);
        }

        /// <summary>
        /// 방향에 해당하는 타일 오프셋을 반환합니다.
        /// </summary>
        public static (int dx, int dy) GetDirectionOffset(Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:    return (0, 1);
                case Direction.Down:  return (0, -1);
                case Direction.Left:  return (-1, 0);
                case Direction.Right: return (1, 0);
                default:              return (0, -1);
            }
        }

        /// <summary>
        /// 이동 입력 벡터로부터 바라보는 방향을 계산합니다.
        /// </summary>
        public static Direction VectorToDirection(float moveX, float moveY, Direction fallback)
        {
            if (Math.Abs(moveX) < 0.001f && Math.Abs(moveY) < 0.001f)
            {
                return fallback;
            }

            if (Math.Abs(moveX) >= Math.Abs(moveY))
            {
                return moveX > 0 ? Direction.Right : Direction.Left;
            }
            else
            {
                return moveY > 0 ? Direction.Up : Direction.Down;
            }
        }

        /// <summary>
        /// 두 월드 좌표 사이의 거리를 반환합니다.
        /// </summary>
        public static float Distance(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 지정된 월드 좌표에 엔티티가 위치할 수 있는지 검사합니다.
        /// </summary>
        public static bool CanStandAt(float worldX, float worldY, float radius, Func<int, int, bool> isTileWalkable)
        {
            int minTileX = (int)Math.Floor(worldX - radius);
            int maxTileX = (int)Math.Floor(worldX + radius);
            int minTileY = (int)Math.Floor(worldY - radius);
            int maxTileY = (int)Math.Floor(worldY + radius);

            for (int ty = minTileY; ty <= maxTileY; ty++)
            {
                for (int tx = minTileX; tx <= maxTileX; tx++)
                {
                    if (!isTileWalkable(tx, ty))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 시전자의 전방 타격 대상 타일 목록을 계산합니다.
        /// </summary>
        public static List<(int tileX, int tileY)> GetFrontTargetTiles(
            float attackerX,
            float attackerY,
            Direction dir,
            int tileRange,
            Func<int, int, bool>? isWallTile = null)
        {
            var result = new List<(int tileX, int tileY)>(tileRange);
            var (originTx, originTy) = WorldToTile(attackerX, attackerY);
            var (dx, dy) = GetDirectionOffset(dir);

            for (int step = 1; step <= tileRange; step++)
            {
                int targetTx = originTx + dx * step;
                int targetTy = originTy + dy * step;

                if (isWallTile != null && isWallTile(targetTx, targetTy))
                {
                    // 벽에 막히면 관통 중단
                    break;
                }

                result.Add((targetTx, targetTy));
            }

            return result;
        }
    }
}
