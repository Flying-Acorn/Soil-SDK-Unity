using System;

namespace FlyingAcorn.Soil.Leaderboard.Models
{
    [System.Serializable]
    public class ScientificScore
    {
        public long mantissa;
        public long exponent;
        
        public double ToDouble()
        {
            return mantissa * Math.Pow(10, exponent);
        }

        public override string ToString()
        {
            return $"{mantissa}e{exponent}";
        }
    }
}