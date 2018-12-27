using System;
using System.Collections.Generic;
using TomB.Util.Collections;
using Xunit;

namespace BTreeTest
{
    public class UnitTest1
    {
        private static void Compare<K, V>(BTreeSortedDictionary<K, V> tree, SortedDictionary<K, V> master)
        {
            if (tree.Count != master.Count)
                throw new Exception();
            var arrMaster = new KeyValuePair<K, V>[master.Count];
            master.CopyTo(arrMaster, 0);
            var arrTree = new KeyValuePair<K, V>[master.Count];
            tree.CopyTo(arrTree, 0);
            for (int i = 0; i < master.Count; i++)
                if (!arrTree[i].Key.Equals(arrMaster[i].Key) || !arrTree[i].Value.Equals(arrMaster[i].Value))
                    throw new Exception();
            for (int i = 0; i < master.Count; i++)
                if (!tree.ContainsKey(arrMaster[i].Key))
                    throw new Exception();

        }
        private static void Step(BTreeSortedDictionary<int, int> tree, SortedDictionary<int, int> master, int target, Random rnd)
        {
            if (target > tree.Count)
            {
                long t0 = DateTime.Now.Ticks;
                Console.Write(tree.Count + "->" + target + ": add " + (target - tree.Count) + "   ");
                while (tree.Count < target)
                {
                    int k;
                    do
                    {
                        k = rnd.Next(0, 999999999);
                    } while (master.ContainsKey(k));
                    tree.Add(k, -k);
                    master.Add(k, -k);
                }
                long t1 = DateTime.Now.Ticks;
                Console.WriteLine(TimeSpan.FromTicks(t1 - t0).TotalMilliseconds);
            }
            else
            {
                long t0 = DateTime.Now.Ticks;
                Console.Write(tree.Count + "->" + target + ": remove " + (tree.Count - target) + "   ");
                var lstMaster = new List<KeyValuePair<int, int>>(master);
                int c = 0;
                while (target < tree.Count)
                {
                    int idx = rnd.Next(lstMaster.Count);
                    var k = lstMaster[idx];
                    lstMaster.RemoveAt(idx);
                    master.Remove(k.Key);
                    tree.Remove(k.Key);
                    c++;
                }
                long t1 = DateTime.Now.Ticks;
                Console.WriteLine(TimeSpan.FromTicks(t1 - t0).TotalMilliseconds);
            }
        }


        [Fact]
        public void TestAddRemove()
        {
            var tree = new BTreeSortedDictionary<int, int>(3);
            var master = new SortedDictionary<int, int>();
            var rnd = new Random(0);
            Step(tree, master, 700000, rnd);
            Compare<int, int>(tree, master);
            Step(tree, master, 0, rnd);
            Compare<int, int>(tree, master);
        }

        private void Stress(int d,int elements,int loops)
        {
            var tree = new BTreeSortedDictionary<int, int>(d);
            var master = new SortedDictionary<int, int>();
            var rnd = new Random(0);
            for (int i = 0; i < loops; i++)
            {
                Step(tree, master, elements, rnd);
                Compare<int, int>(tree, master);
                Step(tree, master, 0, rnd);
                Compare<int, int>(tree, master);
            }

        }
        [Fact]
        public void TestStress3()
        {
            Stress(3, 500000, 20);
        }
        [Fact]
        public void TestStress13()
        {
            Stress(13, 500000, 20);
        }
        [Fact]
        public void TestStress1000()
        {
            Stress(1000, 500000, 20);
        }
        [Fact]
        public void TestEnumerators()
        {
            var tree = new BTreeSortedDictionary<int, int>(3);
            var master = new SortedDictionary<int, int>();
            var rnd = new Random(0);
            Step(tree, master, 100000, rnd);
            Compare<int, int>(tree, master);
            var arrMaster = new KeyValuePair<int,int>[master.Count];
            master.CopyTo(arrMaster, 0);
            int p = 0;
            foreach( var kv in tree )
            {
                Assert.True(kv.Key == arrMaster[p].Key && kv.Value == arrMaster[p].Value);
                p++;
            }
            Assert.True(p == master.Count);
            p = 0;
            foreach (var v in tree.Values)
            {
                Assert.True(v == arrMaster[p].Value );
                p++;
            }
            Assert.True(p == master.Count);
            p = 0;
            foreach (var k in tree.Keys)
            {
                Assert.True(k == arrMaster[p].Key);
                p++;
            }
            Assert.True(p == master.Count);
        }
        [Fact]
        public void TestEnum()
        {

            var tree = new BTreeSortedDictionary<int, int>(null, 3, null);
            for (int i = 2; i <= 1000; i += 2)  // all even numbers
                tree.Add(i, -i);

            int cmp;
            // less or equal
            for (int g = 1; g < 20; g++)
            {
                var leEnum = tree.GetEnumeratorLessOrEqual(g);
                cmp = 2;
                while (leEnum.MoveNext())
                {
                    int k = leEnum.Current.Key;
                    Assert.True(k == cmp);
                    cmp += 2;
                }

                Assert.False(cmp != g && cmp != g - 1 && (g == 1 && cmp != 2));
            }

            // greater or equal
            for (int g = 890; g <= 1000; g++)
            {
                var leEnum = tree.GetEnumeratorGreaterOrEqual(g);
                cmp = g;
                if ((g % 2) == 1)
                    cmp++;
                while (leEnum.MoveNext())
                {
                    int k = leEnum.Current.Key;
                    Assert.True(k == cmp);
                    cmp += 2;
                }
                Assert.True(cmp == 1002);
            }

            // range
            for (int g = 300; g <= 400; g++)
            {
                var rngEnum = tree.GetEnumeratorRange(g, g + 100);
                cmp = g;
                int last;
                if ((g % 2) == 1)
                {
                    cmp++;
                    last = cmp + 98;
                }
                else
                {
                    last = cmp + 100;
                }

                while (rngEnum.MoveNext())
                {
                    int k = rngEnum.Current.Key;
                    Assert.True(k == cmp);
                    cmp += 2;
                }
                Assert.True(cmp == last + 2);

            }
            cmp = 2;
            var allEnum = tree.GetEnumerator();
            while (allEnum.MoveNext())
            {
                int k = allEnum.Current.Key;
                Assert.True(k == cmp);
                cmp += 2;
            }
            Assert.True(cmp == 1002);
        }

        [Fact]
        public void TestGreatest()
        {
            var tree = new BTreeSortedDictionary<int, int>(null, 3, null);
            for (int i = 0; i < 1000; i++)
                tree.Add(i, -i);
            for (int i = 999; i >= 0; i--)
            {
                var peek = tree.GetGreatest();
                Assert.True(peek.Key == i);
                var removed = tree.RemoveGreatest();
                Assert.True(removed.Key == i);
            }
            Assert.True(tree.Count == 0);
        }
        [Fact]
        public void TestSmallest()
        {
            var tree = new BTreeSortedDictionary<int, int>(null, 3, null);
            for (int i = 0; i < 1000; i++)
                tree.Add(i, -i);
            for (int i = 0; i <= 999; i++)
            {
                var peek = tree.GetSmallest();
                Assert.True(peek.Key == i);
                var removed = tree.RemoveSmallest();
                Assert.True(removed.Key == i);
            }
            Assert.True(tree.Count == 0);
        }

    }
}
