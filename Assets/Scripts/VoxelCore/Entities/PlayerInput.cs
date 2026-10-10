using UnityEngine;

namespace VoxelCore.Entities
{
    /// <summary>
    /// Неизменяемый пакет пользовательского ввода игрока за 1 тик.
    /// Передается от контроллера ввода клиента к авторитарному серверу.
    /// </summary>
    public readonly struct PlayerInput
    {
        /// <summary>
        /// Вектор горизонтального ввода: X = стрейф (A/D: -1..1), Y = движение вперед (W/S: -1..1).
        /// </summary>
        public readonly Vector2 Movement;

        /// <summary>
        /// Нажата ли клавиша прыжка (Space).
        /// </summary>
        public readonly bool Jump;

        /// <summary>
        /// Зажат ли режим крадущейся походки (Shift).
        /// </summary>
        public readonly bool Sneak;

        /// <summary>
        /// Зажат ли режим спринта (Ctrl или двойное нажатие W).
        /// </summary>
        public readonly bool Sprint;

        /// <summary>
        /// Угол обзора по горизонтали (градусы).
        /// </summary>
        public readonly float Yaw;

        /// <summary>
        /// Угол обзора по вертикали (градусы).
        /// </summary>
        public readonly float Pitch;

        public PlayerInput(Vector2 movement, bool jump, bool sneak, bool sprint, float yaw, float pitch)
        {
            Movement = movement;
            Jump = jump;
            Sneak = sneak;
            Sprint = sprint;
            Yaw = yaw;
            Pitch = pitch;
        }
    }
}
