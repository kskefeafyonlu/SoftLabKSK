namespace _Project.Blacksmithing.Foundry
{
    [System.Serializable]
    public struct MetalStats
    {
        public float Workability;
        public float Sharpenability;
        public float Toughness;
        public float Density;
        public float Arcana;

        // Adds 'other' into 'thisSum' scaled by weight w (0..1). Kept very basic.
        public static MetalStats WeightedAdd(MetalStats thisSum, MetalStats other, float w)
        {
            thisSum.Workability    += other.Workability    * w;
            thisSum.Sharpenability += other.Sharpenability * w;
            thisSum.Toughness      += other.Toughness      * w;
            thisSum.Density        += other.Density        * w;
            thisSum.Arcana         += other.Arcana         * w;
            return thisSum;
        }
    }
}
