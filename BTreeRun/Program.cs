using System;
using System.Collections.Generic;
using System.Diagnostics;
using TomB.Util.Collections;

namespace BTreeRun
{
    class Program
    {

        private static void Compare<K,V>(BTreeSortedDictionary<K,V> tree,SortedDictionary<K,V> master)
        {
            if (tree.Count != master.Count)
                throw new Exception();
            var arrMaster = new KeyValuePair<K,V>[master.Count];
            master.CopyTo(arrMaster, 0);
            var arrTree = new KeyValuePair<K, V>[master.Count];
            var lstTree = tree.TraverseRecursive(false);
            if( lstTree.Count!=master.Count)
                throw new Exception();
            lstTree.CopyTo(arrTree, 0);
            for (int i = 0; i < master.Count; i++)
                if (!arrTree[i].Key.Equals(arrMaster[i].Key) || !arrTree[i].Value.Equals(arrMaster[i].Value))
                    throw new Exception();
            for(int i=0;i<master.Count;i++)
                if( !tree.ContainsKey(arrMaster[i].Key))
                    throw new Exception();

        }
        private static void Step(BTreeSortedDictionary<int,int> tree,SortedDictionary<int,int> master,int target, Random rnd )
        {
            if( target>tree.Count)
            {
                long t0 = DateTime.Now.Ticks;
                Console.Write(tree.Count + "->" + target + ": add " + (target - tree.Count)+"   ");
                while(tree.Count<target)
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
                Console.Write(tree.Count + "->" + target + ": remove " + (tree.Count-target)+"   ");
                var lstMaster = new List<KeyValuePair<int, int>>(master);
                int c = 0;
                while(target<tree.Count)
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



        static void Main(string[] args)
        {
            var tree = new BTreeSortedDictionary<int, int>(3);
            var master = new SortedDictionary<int, int>();
            var rnd = new Random(0);
            for (int i = 0; i < 100; i++)
            {
                Console.WriteLine("Iteration: " + i);
                Step(tree, master, 700000, rnd);
                Compare<int, int>(tree, master);
                Step(tree, master, 0, rnd);
                Compare<int, int>(tree, master);
                Step(tree, master, 350000, rnd);
                Compare<int, int>(tree, master);
                Step(tree, master, 2, rnd);
                Compare<int, int>(tree, master);
            }
            Console.WriteLine();
        }
    }
}
                                           