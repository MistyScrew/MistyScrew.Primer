using MistyScrew.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MistyScrew.QThinking
{
    public static class QFunctions
    {
        public static bool Eq(object v1, object v2)
        {
            return object.Equals(v1, v2);
        }
        public static bool Eq(double v1, double v2)
        {
            return (v1 - v2) < 1e-6;
        }
        public static bool Eq(IEnumerable<int> v1, IEnumerable<int> v2)
        {
            return v1.SequenceEqual(v2);
        }
        public static Set<int> Set(IEnumerable<int> items)
        {
            return items.Select(i => FromNumber(i)).Or();
        }
        public static double Add(double v1, double v2)
        {
            return v1 + v2;
        }
        public static double Subtract(double v1, double v2)
        {
            return v1 - v2;
        }
        public static double Div(double v1, double v2)
        {
            return v1 / v2;
        }
        public static int IDiv(int v1, int v2)
        {
            return v1 / v2;
        }
        public static int Mod(int v1, int v2)
        {
            return v1 % v2;
        }
        public static double Mul(double v1, double v2)
        {
            return v1 * v2;
        }
        public static int? ToInt(string s)
        {
            return ConvertHlp.ToInt(s);
        }
        public static double? ToDouble(string s)
        {
            return ConvertHlp.ToDouble(s);
        }
        public static double ToDouble(int v)
        {
            return v;
        }

        public static Set<int> FromNumber(int i)
        {
            return new Set<int>(ToF<int>(x => x == i));
        }
        public static Set<int> Делитель(int i)
        {
            return new Set<int>(new[] { new[] { ToF<int>(x => x >= 1), ToF<int>(x => x <= i), ToF<int>(x => i % x == 0) } });
        }
        public static Set<int> Кратный(int i)
        {
            return Кратный(i, 0);
        }
        public static Set<int> Кратный(int i, int tail = 0)
        {
            if (i == 0)
                return new Set<int>(new Functor<int>[][] { });
            return new Set<int>(new[] { new[] { ToF<int>(x => x >= 1), ToF<int>(x => x % i == tail) } });
        }
        public static Set<int> N_Значный(int i)
        {
            return new Set<int>(new[] { new[] { ToF<int>(x => x >= 0), ToF<int>(x => x.ToString().Length == i) } });

        }
        public static Set<int> Кроме(int i)
        {
            return new Set<int>(ToF<int>(x => x != i));
        }
        public static Set<int> Меньше(int i)
        {
            return new Set<int>(ToF<int>(x => x < i));
        }
        public static Set<int> Больше(int i)
        {
            return new Set<int>(ToF<int>(x => x > i));
        }
        public static Set<int> Четный()
        {
            return Кратный(2);
        }
        public static Set<int> Нечетный()
        {
            return Кратный(2, 1);
        }

        public static int? Min(IEnumerable<int> items)
        {
            return items.Select(i => i.AsNullable()).Min();
        }
        public static int? Max(IEnumerable<int> items)
        {
            return items.Select(i => i.AsNullable()).Max();
        }
        public static int Count(IEnumerable<int> items)
        {
            return items.Count();
        }
        public static int Sum(IEnumerable<int> items)
        {
            return items.Sum();
        }
        public static IEnumerable<int> Take(IEnumerable<int> items, int count)
        {
            return items.Take(count);
        }

        public static bool Is(int v, Set<int> items)
        {
            return items.Is(v);
        }

        public static Fraction Дробь(int numenator, int denominator)
        {
            return new Fraction(numenator, denominator);
        }
        public static bool ПравильностьДроби(Fraction fraction)
        {
            return fraction.Numenator > 0 && fraction.Denominator > 0 && fraction.Numenator < fraction.Denominator;
        }
        public static Set<Fraction> ПравильнаяДробь()
        {
            return new Set<Fraction>(new[] { new[] { ToF<Fraction>(fraction => ПравильностьДроби(fraction)) } });
        }
        public static Set<Fraction> НеправильнаяДробь()
        {
            return new Set<Fraction>(new[] { new[] { ToF<Fraction>(fraction => !ПравильностьДроби(fraction)) } });
        }

        static Functor<T> ToF<T>(System.Linq.Expressions.Expression<Func<T, bool>> expression)
        {
            return new Functor<T>(expression);
        }

    }
}