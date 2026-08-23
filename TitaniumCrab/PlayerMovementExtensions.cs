using CodeStage.AntiCheat.ObscuredTypes;

namespace TitaniumCrab
{
    /// <summary>
    /// Extension methods for PlayerMovement that map obfuscated fields
    /// to readable names. Based on CodeName-Anti's TranslationsExtensions.
    /// </summary>
    internal static class PlayerMovementExtensions
    {
        // --- Sprinting (prop_Boolean_2 in unhollowed assembly) ---
        public static bool IsSprinting(this PlayerMovement movement)
            => movement.prop_Boolean_2;

        public static void SetSprinting(this PlayerMovement movement, bool sprinting)
            => movement.prop_Boolean_2 = sprinting;
        // --- Jump Force ---
        public static float GetJumpForce(this PlayerMovement movement)
            => movement.field_Private_Single_3;

        public static void SetJumpForce(this PlayerMovement movement, float jumpForce)
            => movement.field_Private_Single_3 = jumpForce;

        // --- Max Run Speed (sprint speed) ---
        public static float GetMaxRunSpeed(this PlayerMovement movement)
            => movement.field_Private_ObscuredFloat_5.hiddenValue;

        public static void SetMaxRunSpeed(this PlayerMovement movement, float maxRunSpeed)
            => movement.field_Private_ObscuredFloat_5 = new ObscuredFloat(maxRunSpeed);

        // --- Max Speed (walk speed) ---
        public static float GetMaxSpeed(this PlayerMovement movement)
            => movement.field_Private_ObscuredFloat_6.hiddenValue;

        public static void SetMaxSpeed(this PlayerMovement movement, float maxSpeed)
            => movement.field_Private_ObscuredFloat_6 = new ObscuredFloat(maxSpeed);

        // --- Move Speed ---
        public static float GetMoveSpeed(this PlayerMovement movement)
            => movement.field_Private_ObscuredFloat_0.hiddenValue;

        public static void SetMoveSpeed(this PlayerMovement movement, float moveSpeed)
            => movement.field_Private_ObscuredFloat_0 = new ObscuredFloat(moveSpeed);
    }
}
