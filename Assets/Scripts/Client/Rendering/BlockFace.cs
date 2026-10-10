namespace Client.Rendering
{
    /// <summary>
    /// 6 направлений граней куба вокселя.
    /// </summary>
    public enum BlockFace : byte
    {
        Up = 0,    // +Y (Верх)
        Down = 1,  // -Y (Низ)
        North = 2, // +Z (Север)
        South = 3, // -Z (Юг)
        East = 4,  // +X (Восток)
        West = 5   // -X (Запад)
    }
}
