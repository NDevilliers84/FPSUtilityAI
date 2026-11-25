namespace ZombieFPS
{
    internal static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Enemy = 8;
        public const int Player = 9;

        public const int EnvironmentMask = 1 << Default;
        public const int ShootMask = (1 << Default) | (1 << Enemy);
        public const int DamageMask = (1 << Enemy) | (1 << Player);
    }
}
