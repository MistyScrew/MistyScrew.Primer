using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MistyScrew.QThinking
{
    public class Fraction
    {
        public Fraction(int numenator, int denominator)
        {
            this.Numenator = numenator;
            this.Denominator = denominator;
        }
        public readonly int Numenator;
        public readonly int Denominator;
    }
}